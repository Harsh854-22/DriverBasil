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

# Copy ALL published service files (EXE + DLLs + configs + appsettings.json)
Write-Host "Copying service binaries..."
Copy-Item -Path (Join-Path $repoRoot "artifacts\win-x64\service\*") -Destination $tempFolder -Recurse -Force

# Copy ALL published desktop files (EXE + DLLs + configs)
Write-Host "Copying desktop binaries..."
Copy-Item -Path (Join-Path $repoRoot "artifacts\win-x64\desktop\*") -Destination $tempFolder -Recurse -Force

# Copy installer and helper scripts
Copy-Item (Join-Path $PSScriptRoot "install-service.ps1") $tempFolder -Force
Copy-Item (Join-Path $PSScriptRoot "uninstall-service.ps1") $tempFolder -Force
if (Test-Path (Join-Path $repoRoot "src\SecureDeviceControl.Desktop\Install-Service.cmd")) {
    Copy-Item (Join-Path $repoRoot "src\SecureDeviceControl.Desktop\Install-Service.cmd") $tempFolder -Force
}
if (Test-Path (Join-Path $repoRoot "update-service.cmd")) {
    Copy-Item (Join-Path $repoRoot "update-service.cmd") $tempFolder -Force
}

# Remove PDB debug files and documentation from ZIP
Get-ChildItem -Path $tempFolder -Filter "*.pdb" -Recurse | Remove-Item -Force
Get-ChildItem -Path $tempFolder -Filter "documentation.html" -Recurse | Remove-Item -Force

# Compress to ZIP at repository root
$zipPath = Join-Path $repoRoot "SecureDeviceControl-Release.zip"
Write-Host "Creating ZIP archive at $zipPath..."
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

Compress-Archive -Path "$tempFolder\*" -DestinationPath $zipPath

# Clean up
Remove-Item $tempFolder -Recurse -Force

Write-Host "Successfully generated ZIP archive: SecureDeviceControl-Release.zip"

