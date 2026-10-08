@echo off
setlocal
title Secure Device Control - One-Click Install
cd /d "%~dp0"

:: Step 1: self-elevate to administrator (one UAC prompt, then the real work starts).
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Requesting administrator rights - click Yes on the UAC prompt...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '%~1' -Verb RunAs"
    exit /b 0
)

:: Step 2: Email ID for this PC. Optional first argument: Install-ZeroTouch.cmd user@company.com
set "EMAIL=%~1"
:emailloop
echo "%EMAIL%" | findstr /r "@.*\." >nul
if %errorlevel% equ 0 goto emailok
if not "%EMAIL%"=="" echo   That does not look like an Email ID. Please try again.
set "EMAIL="
set /p EMAIL="Email ID for this PC (unique per PC): "
goto emailloop
:emailok

echo.
echo Unblocking installer files...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -Path './*'"

echo.
echo Installing and provisioning for %EMAIL% ...
echo This takes about a minute. Do not close this window.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-service.ps1" -AutoProvision -UserEmail "%EMAIL%" -NonInteractive
set "INSTALL_EXIT=%errorlevel%"

:: Step 3: collect real diagnostics (service state, version, pipe, local
:: registration) into a result file. No PINs or secrets are printed anywhere.
set "RESULT=%TEMP%\SDC-Install-Result.txt"
(
    echo ===== Secure Device Control install result =====
    echo Date: %date% %time%
    echo Email: %EMAIL%
    echo Installer exit code: %INSTALL_EXIT%
    echo.
    echo ----- Service -----
    sc.exe query SecureDeviceControl
    echo.
    echo ----- Installed version -----
    powershell -NoProfile -Command "(Get-Item 'C:\Program Files\SecureDeviceControl\SecureDeviceControl.Service.exe' -ErrorAction SilentlyContinue).VersionInfo.FileVersion"
    echo.
    echo ----- IPC pipe -----
    powershell -NoProfile -Command "[System.IO.Directory]::GetFiles('\\.\pipe\') | Select-String SecureDeviceControl.v1"
    echo.
    echo ----- Local registration -----
    powershell -NoProfile -Command "& 'C:\Program Files\SecureDeviceControl\SecureDeviceControl.Service.exe' provision --status"
) > "%RESULT%" 2>&1

echo.
echo Result saved to: %RESULT%
if "%INSTALL_EXIT%"=="0" (
    echo INSTALL FINISHED. Checks are above and in the result file.
    echo If the service shows STATE: 4 RUNNING, the PC is protected.
) else (
    echo INSTALL FAILED with exit code %INSTALL_EXIT%.
    echo Send the result file to support for diagnosis.
)
echo.
pause
endlocal
