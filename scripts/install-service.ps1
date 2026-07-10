param(
    [string]$ServiceExePath = "",
    [string]$ServiceName = "SecureDeviceControl",
    [string]$DisplayName = "Secure Device Control"
)

$ErrorActionPreference = "Stop"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    throw "Run this script from an elevated PowerShell session using a Windows administrator account."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
if ([string]::IsNullOrWhiteSpace($ServiceExePath)) {
    $flatPath = Join-Path $PSScriptRoot "SecureDeviceControl.Service.exe"
    if (Test-Path $flatPath) {
        $ServiceExePath = $flatPath
    } else {
        $ServiceExePath = Join-Path $repoRoot "artifacts\win-x64\service\SecureDeviceControl.Service.exe"
    }
}

$resolvedServiceExe = Resolve-Path $ServiceExePath
$programDataPath = Join-Path $env:ProgramData "SecureDeviceControl"
New-Item -ItemType Directory -Force -Path $programDataPath | Out-Null

& icacls $programDataPath /inheritance:r | Out-Null
& icacls $programDataPath /grant "SYSTEM:(OI)(CI)F" "Administrators:(OI)(CI)F" | Out-Null

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -eq $existing) {
    New-Service `
        -Name $ServiceName `
        -DisplayName $DisplayName `
        -BinaryPathName "`"$resolvedServiceExe`"" `
        -StartupType Automatic `
        -Description "Enforces local pendrive access policy for Secure Device Control." | Out-Null
}
else {
    Stop-Service -Name $ServiceName -ErrorAction SilentlyContinue
    sc.exe config $ServiceName binPath= "`"$resolvedServiceExe`"" start= auto | Out-Null
}

sc.exe failure $ServiceName reset= 86400 actions= restart/60000/restart/60000/restart/60000 | Out-Null
Start-Service -Name $ServiceName

Write-Host "$DisplayName installed and started."
Write-Host "Open the desktop app and set the two first-run PINs."
