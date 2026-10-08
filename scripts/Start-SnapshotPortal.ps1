$ErrorActionPreference = "Stop"
$dotnet = "C:\tmp\dotnet\dotnet.exe"
if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }
$project = Join-Path $PSScriptRoot "..\src\SecureDeviceControl.SnapshotPortal\SecureDeviceControl.SnapshotPortal.csproj"
Write-Host "Snapshot Ledger: https://127.0.0.1:7443"
& $dotnet run --project $project
