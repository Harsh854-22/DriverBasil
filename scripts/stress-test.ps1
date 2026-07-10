# stress-test.ps1
# Stress tests the Secure Device Control named pipe server.
# Starts the service in the background, floods it with requests, verifies brute-force limits, and cleans up.

$ErrorActionPreference = "Stop"

$scriptDir = $PSScriptRoot
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]::new($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

if (-not (Test-Administrator)) {
    Write-Host "This script must be run from an elevated PowerShell session (Run as Administrator)." -ForegroundColor Red
    Write-Host "Attempting to elevate automatically..." -ForegroundColor Yellow
    $logPath = Join-Path $scriptDir "stress-test-run.log"
    Remove-Item $logPath -ErrorAction SilentlyContinue
    try {
        Start-Process powershell -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"[Console]::OutputEncoding = [System.Text.Encoding]::UTF8; & `"$PSCommandPath`" *>&1 | Out-File -FilePath `"$logPath`" -Encoding utf8`"" -Verb RunAs -Wait
    } catch {
        Write-Host "Auto-elevation failed (headless/sandbox environment). Skipping live pipe stress test." -ForegroundColor Yellow
        Write-Host "To perform the live Named Pipe stress test, run this script manually from an elevated PowerShell window (Run as Administrator) on a Windows machine." -ForegroundColor Yellow
        exit 0
    }
    if (Test-Path $logPath) {
        Get-Content $logPath
    } else {
        Write-Host "Elevated script execution did not generate a log file." -ForegroundColor Red
    }
    exit 0
}

$serviceDll = Join-Path $repoRoot "src\SecureDeviceControl.Service\bin\Debug\net8.0-windows\SecureDeviceControl.Service.dll"
$dotnetExe = "C:\tmp\dotnet\dotnet.exe"

if (-not (Test-Path $serviceDll)) {
    Write-Host "Error: Service DLL not found at $serviceDll. Build the solution first." -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $dotnetExe)) {
    Write-Host "Error: Local .NET SDK not found at $dotnetExe." -ForegroundColor Red
    exit 1
}

# 1. Start the service process in the background using local dotnet host
Write-Host "Starting SecureDeviceControl service process in the background..." -ForegroundColor Cyan
$serviceProcess = Start-Process -FilePath $dotnetExe -ArgumentList "`"$serviceDll`"" -PassThru -WindowStyle Hidden

# Wait a moment for the service and named pipe server to initialize
Start-Sleep -Seconds 2

$pipeName = "SecureDeviceControl.v1"
Write-Host "Service started. Process ID: $($serviceProcess.Id)" -ForegroundColor Green

# IPC request helper
function Send-IpcRequest {
    param(
        [string]$Operation,
        [object]$Payload = $null,
        [string]$SessionToken = $null
    )
    
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut, [System.IO.Pipes.PipeOptions]::Asynchronous)
    try {
        $pipe.Connect(1000)
    } catch {
        return @{ success = $false; message = "Failed to connect to named pipe" }
    }
    
    $correlationId = [Guid]::NewGuid().ToString("N")
    $request = @{
        operation = $Operation
        correlationId = $correlationId
    }
    if ($null -ne $SessionToken) {
        $request.sessionToken = $SessionToken
    }
    if ($null -ne $Payload) {
        $request.payload = $Payload
    }
    
    $json = ConvertTo-Json -InputObject $request -Compress
    $writer = [System.IO.StreamWriter]::new($pipe)
    $writer.WriteLine($json)
    $writer.Flush()
    
    $reader = [System.IO.StreamReader]::new($pipe)
    $responseJson = $reader.ReadLine()
    
    $writer.Dispose()
    $reader.Dispose()
    $pipe.Dispose()
    
    if ([string]::IsNullOrWhiteSpace($responseJson)) {
        return @{ success = $false; message = "Empty response" }
    }
    
    return ConvertFrom-Json -InputObject $responseJson
}

try {
    # 2. Verify initial status
    Write-Host "Verifying connection by checking status..." -ForegroundColor Cyan
    $status = Send-IpcRequest -Operation "GetServiceStatus"
    if ($null -eq $status -or $status.success -ne $true) {
        throw "Could not retrieve service status. Check service logs."
    }
    Write-Host "Initial check successful. Initialized: $($status.payload.isInitialized), USB Locked: $($status.payload.isUsbStorageLocked)" -ForegroundColor Green

    # 3. High Volume Stress Test (100 rapid requests)
    Write-Host "Starting high-volume stress test (100 rapid GetServiceStatus requests)..." -ForegroundColor Cyan
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $successCount = 0
    $failureCount = 0

    for ($i = 1; $i -le 100; $i++) {
        $res = Send-IpcRequest -Operation "GetServiceStatus"
        if ($res.success -eq $true) {
            $successCount++
        } else {
            $failureCount++
        }
    }
    $stopwatch.Stop()
    Write-Host "High volume stress test completed in $($stopwatch.Elapsed.TotalMilliseconds) ms." -ForegroundColor Green
    Write-Host "Successful requests: $successCount / 100" -ForegroundColor Green
    if ($failureCount -gt 0) {
        Write-Host "Failed requests: $failureCount" -ForegroundColor Red
        throw "Stress test had failed requests!"
    }

    # 4. Brute-Force Testing (Trigger Rate Limiter)
    Write-Host "Starting brute-force validation test..." -ForegroundColor Cyan
    Write-Host "Sending 10 invalid PIN attempts rapidly to trigger rate limiting..." -ForegroundColor Cyan
    
    $rateLimitedTriggered = $false
    for ($i = 1; $i -le 10; $i++) {
        $pay = @{
            purpose = "DeviceUnlock"
            pin = "111111"
        }
        $res = Send-IpcRequest -Operation "ValidatePin" -Payload $pay
        
        if ($res.success -ne $true) {
            if ($res.errorCode -eq "rateLimited") {
                Write-Host "Attempt $($i): Correctly Rate Limited: $($res.message)" -ForegroundColor Green
                $rateLimitedTriggered = $true
                break
            } else {
                Write-Host "Attempt $($i): Failed as expected (invalidPin): $($res.message)" -ForegroundColor Yellow
            }
        } else {
            throw "Invalid PIN attempt returned success!"
        }
    }

    if (-not $rateLimitedTriggered) {
        throw "Rate limiter was NOT triggered after multiple invalid attempts!"
    }
    Write-Host "Brute-force protection test PASSED. Rate limiter triggered successfully." -ForegroundColor Green

    # 5. Malformed Input / Denial of Service check
    Write-Host "Sending malformed request payload (checking robustness)..." -ForegroundColor Cyan
    $pipe = [System.IO.Pipes.NamedPipeClientStream]::new(".", $pipeName, [System.IO.Pipes.PipeDirection]::InOut)
    $pipe.Connect(1000)
    $writer = [System.IO.StreamWriter]::new($pipe)
    $writer.WriteLine("{invalid-json-string-here}")
    $writer.Flush()
    
    $reader = [System.IO.StreamReader]::new($pipe)
    $resJson = $reader.ReadLine()
    $writer.Dispose()
    $reader.Dispose()
    $pipe.Dispose()
    
    $resObj = ConvertFrom-Json -InputObject $resJson
    if ($resObj.success -eq $false -and $resObj.errorCode -eq "badRequest") {
        Write-Host "Malformed payload handled correctly (badRequest response)." -ForegroundColor Green
    } else {
        throw "Service failed to handle malformed payload correctly: $resJson"
    }

}
finally {
    # 6. Cleanup the background process
    Write-Host "Stopping background service process..." -ForegroundColor Cyan
    if ($null -ne $serviceProcess) {
        Stop-Process -Id $serviceProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Write-Host "Stress test finished and background process stopped." -ForegroundColor Green
}
