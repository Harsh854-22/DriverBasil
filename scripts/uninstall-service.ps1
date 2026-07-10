param(
    [Parameter(Mandatory = $true)]
    [string]$AuthorizationToken,

    [switch]$RemoveData,
    [string]$ServiceName = "SecureDeviceControl"
)

$ErrorActionPreference = "Stop"

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function ConvertTo-Base64Sha256([byte[]]$Bytes) {
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return [Convert]::ToBase64String($sha.ComputeHash($Bytes))
    }
    finally {
        $sha.Dispose()
    }
}

if (-not (Test-Administrator)) {
    throw "Run this script from an elevated PowerShell session using a Windows administrator account."
}

$programDataPath = Join-Path $env:ProgramData "SecureDeviceControl"
$authorizationPath = Join-Path $programDataPath "uninstall-authorization.token"
if (-not (Test-Path -LiteralPath $authorizationPath)) {
    throw "No uninstall authorization exists. Open the desktop app, enter the uninstall PIN, and create an authorization first."
}

Add-Type -AssemblyName System.Security
$entropy = [Text.Encoding]::UTF8.GetBytes("SecureDeviceControl.v1")
$protectedPayload = [IO.File]::ReadAllBytes($authorizationPath)
$payloadBytes = [Security.Cryptography.ProtectedData]::Unprotect(
    $protectedPayload,
    $entropy,
    [Security.Cryptography.DataProtectionScope]::LocalMachine)
$authorization = [Text.Encoding]::UTF8.GetString($payloadBytes) | ConvertFrom-Json

if ([DateTimeOffset]::Parse($authorization.expiresAt) -le [DateTimeOffset]::UtcNow) {
    throw "The uninstall authorization expired. Create a new authorization from the desktop app."
}

$providedHash = ConvertTo-Base64Sha256 ([Convert]::FromBase64String($AuthorizationToken))
if ($providedHash -ne $authorization.tokenHash) {
    throw "Invalid uninstall authorization token."
}

$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($null -ne $service) {
    if ($service.Status -ne "Stopped") {
        Stop-Service -Name $ServiceName -Force
    }
    sc.exe delete $ServiceName | Out-Null
}

Remove-Item -LiteralPath $authorizationPath -Force -ErrorAction SilentlyContinue
if ($RemoveData) {
    Remove-Item -LiteralPath $programDataPath -Recurse -Force
}

Write-Host "Secure Device Control service removed."
