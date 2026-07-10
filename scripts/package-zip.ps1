$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

# Find dotnet path
$dotnet = "C:\tmp\dotnet\dotnet.exe"
if (Test-Path $dotnet) {
    $env:DOTNET_EXE = $dotnet
    Write-Host "Using dotnet at: $dotnet"
}

# Run publish-release.ps1
Write-Host "Running publish-release.ps1..."
& (Join-Path $PSScriptRoot "publish-release.ps1")

# Create temporary package folder
$tempFolder = Join-Path $repoRoot "temp-package"
if (Test-Path $tempFolder) {
    Remove-Item $tempFolder -Recurse -Force
}
New-Item -ItemType Directory -Path $tempFolder | Out-Null

# Copy binaries
Copy-Item (Join-Path $repoRoot "artifacts\win-x64\service\SecureDeviceControl.Service.exe") $tempFolder
Copy-Item (Join-Path $repoRoot "artifacts\win-x64\desktop\SecureDeviceControl.Desktop.exe") $tempFolder

# Copy installer scripts (copied directly into the flat folder)
Copy-Item (Join-Path $PSScriptRoot "install-service.ps1") $tempFolder
Copy-Item (Join-Path $PSScriptRoot "uninstall-service.ps1") $tempFolder

# Compress to ZIP at repository root
$zipPath = Join-Path $repoRoot "SecureDeviceControl-Release.zip"
Write-Host "Creating ZIP archive at $zipPath..."
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

# Use Compress-Archive
Compress-Archive -Path "$tempFolder\*" -DestinationPath $zipPath

# Clean up
Remove-Item $tempFolder -Recurse -Force

Write-Host "Successfully generated ZIP archive: SecureDeviceControl-Release.zip"
