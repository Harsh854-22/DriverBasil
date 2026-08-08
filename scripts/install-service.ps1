param(
    [string]$ServiceExePath = "",
    [switch]$MigrateLegacyService,
    [switch]$ResetLocalData
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

function Remove-ServiceRegistration([string]$Name) {
    $service = Get-Service -Name $Name -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        return
    }

    if ($service.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Stopped) {
        Stop-Service -Name $Name -Force -ErrorAction Stop
        $service.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(20))
    }

    & sc.exe delete $Name | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to remove service '$Name'."
    }
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

if ((Get-Service -Name $legacyServiceName -ErrorAction SilentlyContinue) -and -not $MigrateLegacyService) {
    throw "A legacy service named '$legacyServiceName' exists. Re-run with -MigrateLegacyService to remove it before installing the corrected service."
}

Set-RestrictedDirectoryAcl $programDataPath
Set-RestrictedDirectoryAcl $installDirectory

if ($MigrateLegacyService) {
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
Copy-Item -LiteralPath $ServiceExePath -Destination $installedServiceExe -Force
Get-ChildItem -LiteralPath $sourceDirectory -Filter "appsettings*.json" -File -ErrorAction SilentlyContinue |
    Copy-Item -Destination $installDirectory -Force

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
& sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
& sc.exe sdset $serviceName "D:(A;;CCDCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRRC;;;BA)(A;;CCLCSWLOCRRC;;;AU)" | Out-Null

Start-Service -Name $serviceName -ErrorAction Stop
Start-Sleep -Seconds 3

$installedService = Get-Service -Name $serviceName
if ($installedService.Status -ne [System.ServiceProcess.ServiceControllerStatus]::Running) {
    Show-RecentServiceErrors
    throw "The service did not remain running. The existing database is preserved; re-run with -ResetLocalData only after reviewing the reported error."
}

Write-Host "$displayName is installed and running as LocalSystem."
Write-Host "Open SecureDeviceControl.Desktop.exe to create the two PINs."
