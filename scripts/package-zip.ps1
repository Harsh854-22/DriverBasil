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
if ($LASTEXITCODE -ne 0) {
    throw "Release publish failed with exit code $LASTEXITCODE."
}

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
Copy-Item (Join-Path $PSScriptRoot "Install-ZeroTouch.cmd") $tempFolder -Force
if (Test-Path (Join-Path $repoRoot "src\SecureDeviceControl.Desktop\Install-Service.cmd")) {
    Copy-Item (Join-Path $repoRoot "src\SecureDeviceControl.Desktop\Install-Service.cmd") $tempFolder -Force
}
if (Test-Path (Join-Path $repoRoot "update-service.cmd")) {
    Copy-Item (Join-Path $repoRoot "update-service.cmd") $tempFolder -Force
}
Copy-Item (Join-Path $repoRoot "docs\documentation.html") (Join-Path $tempFolder "documentation.html") -Force

# Sanitize secrets: the release ZIP is handed to every PC and must be treated
# as public. A service_role key or DB password in the ZIP lets anyone with the
# release list/download the private Snapshots bucket and open the database.
Write-Host "Sanitizing secrets from package..."
foreach ($config in Get-ChildItem -Path $tempFolder -Filter "appsettings*.json" -Recurse) {
    try {
        $json = Get-Content -LiteralPath $config.FullName -Raw | ConvertFrom-Json
        $changed = $false
        if (($null -ne $json.Supabase) -and ($null -ne $json.Supabase.PSObject.Properties["StorageServiceRoleKey"])) {
            $roleValue = "$($json.Supabase.StorageServiceRoleKey)"
            if (($roleValue -ne "") -and ($roleValue -notlike "*[*")) {
                $json.Supabase.PSObject.Properties.Remove("StorageServiceRoleKey")
                $changed = $true
            }
        }
        if (($null -ne $json.ConnectionStrings) -and ($null -ne $json.ConnectionStrings.PSObject.Properties["Supabase"])) {
            $csValue = "$($json.ConnectionStrings.Supabase)"
            if (($csValue -ne "") -and ($csValue -notlike "*[*") -and ($csValue -notlike "*YOUR-PASSWORD*")) {
                $json.ConnectionStrings.Supabase = ""
                $changed = $true
            }
        }
        if ($changed) {
            $json | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $config.FullName -Encoding UTF8
            Write-Host "  sanitized: $($config.Name)"
        }
    }
    catch {
        throw "Failed to sanitize '$($config.FullName)': $($_.Exception.Message)"
    }
}
# Redact any leaked secret text from shipped docs.
foreach ($doc in Get-ChildItem -Path $tempFolder -Filter "*.html" -Recurse) {
    $text = Get-Content -LiteralPath $doc.FullName -Raw
    $redacted = $text -replace 'eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+', '[ROTATED-KEY]' -replace 'LessgobasilDriver@1', '[ROTATED-PASSWORD]'
    if ($redacted -ne $text) {
        Set-Content -LiteralPath $doc.FullName -Value $redacted -Encoding UTF8
        Write-Host "  redacted secrets in: $($doc.Name)"
    }
}
# Fail-safe: a service_role key or real DB password must never ship.
$leakHits = Get-ChildItem -Path $tempFolder -Include "*.json", "*.html", "*.ps1", "*.cmd" -Recurse |
    Select-String -Pattern '"StorageServiceRoleKey"\s*:\s*"eyJ|Password=[^";\s\[]' |
    Select-Object -ExpandProperty Path -Unique
if ($leakHits) {
    throw ("Secret leak detected in package files: " + ($leakHits -join ", ") + ". Remove secrets and re-run.")
}

# Remove PDB debug files from ZIP
Get-ChildItem -Path $tempFolder -Filter "*.pdb" -Recurse | Remove-Item -Force

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
