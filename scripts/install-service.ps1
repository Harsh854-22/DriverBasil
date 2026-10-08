param(
    [string]$ServiceExePath = "",
    [string]$DesktopExePath = "",
    [switch]$MigrateLegacyService,
    [switch]$ResetLocalData,
    [switch]$AutoProvision,
    [string]$UserEmail = "",
    [string]$DeviceUnlockPin = "421301",
    [string]$UninstallPin = "121356",
    [bool]$AcknowledgeSnapshots = $false,
    [switch]$NonInteractive,
    [string]$CloudCredentialsFile = ""
)

$ErrorActionPreference = "Stop"

$serviceName = "SecureDeviceControl"
$displayName = "Secure Device Control"
$legacyServiceName = "Secure Device Control"
$programDataPath = Join-Path $env:ProgramData "SecureDeviceControl"
$installDirectory = Join-Path $env:ProgramFiles "SecureDeviceControl"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Set-RestrictedDirectoryAcl([string]$Path) {
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
    & icacls $Path /inheritance:r | Out-Null
    & icacls $Path /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to set protected permissions on '$Path'."
    }
}

function Set-ApplicationReadAcl([string]$Path) {
    & icacls $Path /grant "Authenticated Users:(OI)(CI)RX" | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to grant standard users read and execute access to '$Path'."
    }
}

function Remove-ServiceRegistration([string]$Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        return
    }

    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $Name -Force -ErrorAction Stop
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(20))
    }

    # sc.exe delete can fail transiently (SCM busy), when a previous install
    # hardened the service DACL, or when a deletion is already pending.
    # Retry, reset an admin-capable DACL before the last attempt, and never
    # swallow the real sc.exe error text. Native stderr must not terminate
    # the script under $ErrorActionPreference = "Stop".
    $previousActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $deleteOutput = ""
        $deleteExit = 1
        for ($attempt = 1; $attempt -le 3; $attempt++) {
            $deleteOutput = (& sc.exe delete $Name 2>&1 | Out-String).Trim()
            $deleteExit = $LASTEXITCODE
            if ($deleteExit -eq 0) {
                return
            }
            if ($deleteOutput -match "marked for deletion") {
                Write-Host "Service '$Name' is already marked for deletion; it disappears after a reboot. Continuing."
                return
            }
            if ($attempt -eq 2) {
                Write-Host "Retrying removal of '$Name' with a reset service DACL ..."
                & sc.exe sdset $Name "D:(A;;CCDCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRRC;;;BA)(A;;CCLCSWLOCRRC;;;AU)" 2>&1 | Out-Null
            }
            Start-Sleep -Seconds 3
        }
    }
    finally {
        $ErrorActionPreference = $previousActionPreference
    }

    if ($null -eq (Get-Service -Name $Name -ErrorAction SilentlyContinue)) {
        return
    }
    throw "Unable to remove service '$Name'. sc.exe reported: $deleteOutput Close services.msc if open, then re-run. If the service is marked for deletion, reboot once and re-run."
}

function Show-RecentServiceErrors {
    Get-WinEvent -FilterHashtable @{
        LogName = "Application"
        StartTime = (Get-Date).AddMinutes(-5)
    } -ErrorAction SilentlyContinue |
        Where-Object {
            $_.ProviderName -match "SecureDeviceControl|\.NET Runtime|Application Error" -or
            $_.Message -match "SecureDeviceControl|SQLite Error"
        } |
        Select-Object -First 10 TimeCreated, Id, LevelDisplayName, ProviderName, Message |
        Format-List
}

if (-not (Test-Administrator)) {
    throw "Run this script from an elevated PowerShell session using a Windows administrator account."
}

if ($AutoProvision) {
    foreach ($pinValue in @($DeviceUnlockPin, $UninstallPin)) {
        if ($pinValue -notmatch '^\d{6}$') {
            throw "AutoProvision PINs must each be exactly 6 digits."
        }
    }
    if ($DeviceUnlockPin -eq $UninstallPin) {
        throw "Device unlock PIN and uninstall PIN must be different."
    }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ServiceExePath)) {
    $flatPath = Join-Path $PSScriptRoot "SecureDeviceControl.Service.exe"
    if (Test-Path -LiteralPath $flatPath) {
        $ServiceExePath = $flatPath
    }
    else {
        $ServiceExePath = Join-Path $repoRoot "artifacts\win-x64\service\SecureDeviceControl.Service.exe"
    }
}

if (-not (Test-Path -LiteralPath $ServiceExePath -PathType Leaf)) {
    throw "Service executable was not found: $ServiceExePath"
}

if ([string]::IsNullOrWhiteSpace($DesktopExePath)) {
    $flatDesktopPath = Join-Path $PSScriptRoot "SecureDeviceControl.Desktop.exe"
    if (Test-Path -LiteralPath $flatDesktopPath) {
        $DesktopExePath = $flatDesktopPath
    }
    else {
        $DesktopExePath = Join-Path $repoRoot "artifacts\win-x64\desktop\SecureDeviceControl.Desktop.exe"
    }
}

if (-not (Test-Path -LiteralPath $DesktopExePath -PathType Leaf)) {
    throw "Desktop executable was not found: $DesktopExePath"
}

if ((Get-Service -Name $legacyServiceName -ErrorAction SilentlyContinue) -and -not $MigrateLegacyService -and -not $AutoProvision) {
    throw "A legacy service named '$legacyServiceName' exists. Re-run with -MigrateLegacyService to remove it before installing the corrected service."
}

$installVisibility = 0
if (Test-Path -LiteralPath $installDirectory) {
    $installItem = Get-Item -LiteralPath $installDirectory -Force
    $installVisibility = $installItem.Attributes -band ([IO.FileAttributes]::Hidden -bor [IO.FileAttributes]::System)
}

Set-RestrictedDirectoryAcl $programDataPath
Set-RestrictedDirectoryAcl $installDirectory
Set-ApplicationReadAcl $installDirectory

# Install-time cloud credentials (optional). The JSON file holds ConnectionString
# and/or StorageKey and is copied into the SYSTEM+Administrators-only program-data
# directory, so standard users on the PC cannot read it. Without it the service
# runs offline (ports still lock); cloud sync activates after a restart once set.
if (-not [string]::IsNullOrWhiteSpace($CloudCredentialsFile)) {
    if (-not (Test-Path -LiteralPath $CloudCredentialsFile -PathType Leaf)) {
        throw "Cloud credentials file was not found: $CloudCredentialsFile"
    }
    try {
        $creds = Get-Content -LiteralPath $CloudCredentialsFile -Raw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        throw "Cloud credentials file is not valid JSON: $CloudCredentialsFile"
    }
    $hasConn = ($null -ne $creds.PSObject.Properties["ConnectionString"]) -and ("$($creds.ConnectionString)" -ne "")
    $hasKey = ($null -ne $creds.PSObject.Properties["StorageKey"]) -and ("$($creds.StorageKey)" -ne "")
    if (-not $hasConn -and -not $hasKey) {
        throw "Cloud credentials file must contain ConnectionString and/or StorageKey: $CloudCredentialsFile"
    }
    Copy-Item -LiteralPath $CloudCredentialsFile -Destination (Join-Path $programDataPath "cloud-credentials.json") -Force
    Write-Host "Cloud credentials installed (SYSTEM+Administrators only)."
}

if ($MigrateLegacyService -or $AutoProvision) {
    Remove-ServiceRegistration $legacyServiceName
}

if ($ResetLocalData) {
    Remove-ServiceRegistration $serviceName

    $recoveryDirectory = Join-Path $programDataPath ("recovery\" + (Get-Date -Format "yyyyMMdd-HHmmss"))
    New-Item -ItemType Directory -Force -Path $recoveryDirectory | Out-Null
    Get-ChildItem -LiteralPath $programDataPath -Filter "secure-device-control.db*" -File -ErrorAction SilentlyContinue |
        Move-Item -Destination $recoveryDirectory -ErrorAction Stop
    Write-Host "Existing local database files were preserved in: $recoveryDirectory"
}

$sourceDirectory = Split-Path -Parent (Resolve-Path -LiteralPath $ServiceExePath)
$installedServiceExe = Join-Path $installDirectory "SecureDeviceControl.Service.exe"
$installedDesktopExe = Join-Path $installDirectory "SecureDeviceControl.Desktop.exe"
foreach ($hiddenTarget in @($installedServiceExe, $installedDesktopExe)) {
    if (Test-Path -LiteralPath $hiddenTarget -PathType Leaf) {
        attrib.exe -H -S -R $hiddenTarget | Out-Null
    }
}
Copy-Item -LiteralPath $ServiceExePath -Destination $installedServiceExe -Force
Copy-Item -LiteralPath $DesktopExePath -Destination $installedDesktopExe -Force
Get-ChildItem -LiteralPath $sourceDirectory -Filter "appsettings*.json" -File -Force -ErrorAction SilentlyContinue |
    Copy-Item -Destination $installDirectory -Force
if ($installVisibility -ne 0) {
    $installItem = Get-Item -LiteralPath $installDirectory -Force
    $installItem.Attributes = $installItem.Attributes -bor $installVisibility
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    New-Service `
        -Name $serviceName `
        -DisplayName $displayName `
        -BinaryPathName "`"$installedServiceExe`"" `
        -StartupType Automatic `
        -Description "Enforces Secure Device Control policy for this Windows device." | Out-Null
}
else {
    if ($existing.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $serviceName -Force -ErrorAction Stop
    }
    & sc.exe config $serviceName binPath= "`"$installedServiceExe`"" start= auto | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to update the service executable path."
    }
}

& sc.exe config $serviceName obj= LocalSystem | Out-Null
& sc.exe config $serviceName start= auto | Out-Null
& sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/15000/restart/30000 | Out-Null
& sc.exe failureflag $serviceName 1 | Out-Null
& sc.exe sdset $serviceName "D:(A;;CCDCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRRC;;;BA)(A;;CCLCSWLOCRRC;;;AU)" | Out-Null

if ($AutoProvision) {
    # The service is stopped at this point (freshly created, or stopped for
    # reconfiguration above), so the provision command owns the local database.
    $existingEmail = ""
    try {
        $statusOutput = & $installedServiceExe provision --status 2>$null | Out-String
        $status = $statusOutput | ConvertFrom-Json -ErrorAction Stop
        if ($null -ne $status.email) {
            $existingEmail = "$($status.email)".Trim()
        }
    }
    catch {
        $existingEmail = ""
    }

    if ([string]::IsNullOrWhiteSpace($UserEmail)) {
        if ($NonInteractive) {
            if ([string]::IsNullOrWhiteSpace($existingEmail)) {
                throw "AutoProvision needs -UserEmail on a fresh PC (or run interactively so it can be typed)."
            }
            $UserEmail = $existingEmail
        }
        else {
            $prompt = "Email ID for this PC (unique per PC)"
            if (-not [string]::IsNullOrWhiteSpace($existingEmail)) {
                $prompt += " [$existingEmail]"
            }
            $answer = Read-Host $prompt
            if ([string]::IsNullOrWhiteSpace($answer)) {
                $answer = $existingEmail
            }
            $attempts = 0
            while (($attempts -lt 3) -and
                ([string]::IsNullOrWhiteSpace($answer) -or $answer -notmatch '@' -or $answer -notmatch '\.')) {
                Write-Host "Enter a valid Email ID. One Email = one PC is enforced by cloud sync."
                $answer = Read-Host "Email ID for this PC"
                $attempts++
            }
            if ([string]::IsNullOrWhiteSpace($answer) -or $answer -notmatch '@' -or $answer -notmatch '\.') {
                throw "A valid Email ID is required for provisioning."
            }
            $UserEmail = $answer.Trim().ToLowerInvariant()
        }
    }

    if ($UserEmail -notmatch '@' -or $UserEmail -notmatch '\.') {
        throw "A valid Email ID is required for provisioning."
    }
    $UserEmail = $UserEmail.Trim().ToLowerInvariant()

    Write-Host "Provisioning device PINs for '$UserEmail' ..."
    # NOTE: --email is omitted (not passed empty) when keeping the stored Email,
    # because empty-string arguments do not survive native command invocation.
    $provisionArgs = @("provision", "--device-pin", $DeviceUnlockPin, "--uninstall-pin", $UninstallPin, "--snapshot-ack", "$AcknowledgeSnapshots")
    if (-not [string]::IsNullOrWhiteSpace($UserEmail)) {
        $provisionArgs += @("--email", $UserEmail)
    }
    # Native stderr must not terminate the script under $ErrorActionPreference=Stop;
    # the exit code below is the real verdict.
    $previousActionPreference = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & $installedServiceExe @provisionArgs 2>$null
    $provisionExit = $LASTEXITCODE
    $ErrorActionPreference = $previousActionPreference
    if ($provisionExit -eq 3) {
        Write-Host "Note: the previous local database was unreadable. It was preserved under C:\ProgramData\SecureDeviceControl\recovery and re-provisioned."
    }
    elseif ($provisionExit -ne 0) {
        throw "Provisioning failed with exit code $provisionExit. Fix the reported error and re-run the installer."
    }

    if ($AcknowledgeSnapshots) {
        Write-Host "Snapshot monitoring acknowledgement recorded. Only run this on company-owned PCs where employees have been informed."
    }
    else {
        Write-Host "Snapshot monitoring left unacknowledged: uploads will be rejected until re-registered with acknowledgement."
    }
}

Start-Service -Name $serviceName -ErrorAction Stop
Start-Sleep -Seconds 3

$installedService = Get-Service -Name $serviceName
if ($installedService.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
    Show-RecentServiceErrors
    throw "The service did not remain running. The existing database is preserved; re-run with -ResetLocalData only after reviewing the reported error."
}

Write-Host "$displayName is installed and running as LocalSystem."
if ($AutoProvision) {
    $installedVersion = (Get-Item -LiteralPath $installedServiceExe).VersionInfo.FileVersion
    Write-Host "Version: $installedVersion | Email: $UserEmail | Ports lock automatically (service applies policy on start)."
    Write-Host "PINs set via installer defaults (customize with -DeviceUnlockPin / -UninstallPin). Rotate per-PC with remote UPDATE_DEVICE_PIN / UPDATE_UNINSTALL_PIN commands once online."
    Write-Host "Cloud sync is pending: remote lock/unlock and PIN changes activate automatically when this PC reaches Supabase (no reinstall needed)."
    try {
        [System.IO.Directory]::GetFiles("\\.\pipe\") | Select-String SecureDeviceControl.v1 | Out-Null
        Write-Host "IPC pipe SecureDeviceControl.v1 is listening."
    }
    catch {
        Write-Host "Warning: IPC pipe check was inconclusive; open the desktop app and press Refresh to confirm."
    }
}
else {
    Write-Host "Open $installedDesktopExe to create the two PINs and acknowledge snapshot monitoring."
    Write-Host "Tip: re-run with -AutoProvision to set default PINs and Email without opening the desktop app."
}
