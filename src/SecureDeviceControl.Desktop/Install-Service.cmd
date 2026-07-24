@echo off
:: Secure Device Control Service Installer Batch File
echo ========================================================
echo Installing Secure Device Control Windows Service...
echo ========================================================
cd /d "%~dp0"

:: Create Service with NT AUTHORITY\SYSTEM Privileges
sc.exe create "Secure Device Control" binPath= "\"%~dp0SecureDeviceControl.Service.exe\"" start= auto
sc.exe config "Secure Device Control" obj= LocalSystem
sc.exe start "Secure Device Control"

echo.
echo ========================================================
echo Service Status Query:
echo ========================================================
sc.exe query "Secure Device Control"
echo.
echo Done! You can now launch SecureDeviceControl.Desktop.exe.
pause
