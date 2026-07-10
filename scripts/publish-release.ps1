param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnet = $env:DOTNET_EXE
if ([string]::IsNullOrWhiteSpace($dotnet)) {
    $dotnet = "dotnet"
}

$artifacts = Join-Path $repoRoot "artifacts\$Runtime"
$serviceOutput = Join-Path $artifacts "service"
$desktopOutput = Join-Path $artifacts "desktop"

New-Item -ItemType Directory -Force -Path $serviceOutput | Out-Null
New-Item -ItemType Directory -Force -Path $desktopOutput | Out-Null

& $dotnet publish (Join-Path $repoRoot "src\SecureDeviceControl.Service\SecureDeviceControl.Service.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $serviceOutput

& $dotnet publish (Join-Path $repoRoot "src\SecureDeviceControl.Desktop\SecureDeviceControl.Desktop.csproj") `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $desktopOutput

Write-Host "Published Secure Device Control artifacts to $artifacts"
Write-Host "Service: $serviceOutput\SecureDeviceControl.Service.exe"
Write-Host "Desktop: $desktopOutput\SecureDeviceControl.Desktop.exe"
