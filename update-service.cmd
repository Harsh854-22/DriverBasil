@echo off
echo ========================================================
echo Secure Device Control - Service Update Tool
echo ========================================================
echo.

:: Stop the service
echo Stopping service...
sc stop "Secure Device Control"
timeout /t 4 /nobreak >nul

:: Restart the service with the updated binaries in this folder
echo Starting service with updated binaries...
sc start "Secure Device Control"
timeout /t 3 /nobreak >nul

echo.
echo ========================================================
echo Service Status:
echo ========================================================
sc query "Secure Device Control"
echo.
echo Done! Service restarted with the latest binaries.
pause
