Add-Type -AssemblyName System.IO.Compression.FileSystem
$z = [System.IO.Compression.ZipFile]::OpenRead("SecureDeviceControl-Release.zip")
Write-Host "Total files:" $z.Entries.Count
foreach ($e in $z.Entries) {
    if ($e.Name -match '\.(exe|cmd|ps1|json|dll)$') {
        Write-Host $e.FullName
    }
}
$z.Dispose()
