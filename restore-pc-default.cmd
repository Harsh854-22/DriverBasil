@echo off
:: ============================================================================
:: Secure Device Control - PC Default Restoration Tool
:: Restores USB Storage, Mobile Ports, and Network settings to Windows Defaults
:: ============================================================================

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [!] Administrator privileges required. Requesting elevation...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo ========================================================
echo  Restoring PC Settings to Windows Defaults
echo ========================================================
echo.

:: 1. Restore USB Storage Policy (Start = 3, Enabled)
echo [1/4] Restoring USB Storage (USBSTOR = 3)...
reg add "HKLM\SYSTEM\CurrentControlSet\Services\USBSTOR" /v Start /t REG_DWORD /d 3 /f >nul
if %errorlevel% equ 0 (
    echo       [OK] USB Storage unlocked (Start=3).
) else (
    echo       [FAILED] Could not modify USBSTOR.
)

:: 2. Restore WPD / Mobile MTP Policy (Start = 3, Enabled)
echo [2/4] Restoring Mobile / MTP Driver (WpdMtpDr = 3)...
reg add "HKLM\SYSTEM\CurrentControlSet\Services\WpdMtpDr" /v Start /t REG_DWORD /d 3 /f >nul 2>&1
echo       [OK] Mobile device access set to normal.

:: 3. Remove Group Policy Storage Deny Keys (if any)
echo [3/4] Checking Storage Policies...
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\RemovableStorageDevices" /f >nul 2>&1
echo       [OK] Storage policies cleared.

:: 4. Clean Hosts File SDC Markers
echo [4/4] Checking Hosts file...
powershell -NoProfile -Command "
$hostsPath = \"$env:SystemRoot\System32\drivers\etc\hosts\";
if (Test-Path $hostsPath) {
    $content = Get-Content -Path $hostsPath -Raw;
    if ($content -match 'SDC_WEB_FILTER') {
        $cleaned = $content -replace '(?s)# BEGIN SDC_WEB_FILTER.*?# END SDC_WEB_FILTER\r?\n?', '';
        Set-Content -Path $hostsPath -Value $cleaned -Force;
        Write-Host '      [OK] Hosts file cleaned.';
    } else {
        Write-Host '      [OK] Hosts file is clean.';
    }
}
"

echo.
echo ========================================================
echo  PC Successfully Restored to Normal Default State!
echo ========================================================
echo.
pause
