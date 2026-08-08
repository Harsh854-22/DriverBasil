@echo off
setlocal
echo Installing Secure Device Control with administrator approval...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-service.ps1" -MigrateLegacyService
if errorlevel 1 (
  echo Installation failed. Review the error above before retrying.
  pause
  exit /b 1
)
echo Installation completed.
pause
