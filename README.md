# Secure Device Control

Secure Device Control is a native Windows 10/11 application for PIN-gated USB-storage and MTP-device control. It has two parts:

- A WPF desktop interface for registration, PIN validation, status, and local activity logs.
- A Windows Service that runs as LocalSystem and is the only component allowed to apply device policy.

The desktop application never creates or changes the Windows service. An administrator installs the service explicitly through `scripts\install-service.ps1`.

## Release workflow

1. Build and test:

   ```powershell
   dotnet test SecureDeviceControl.sln -c Release
   ```

2. Publish and package:

   ```powershell
   powershell -ExecutionPolicy Bypass -File .\scripts\package-zip.ps1
   ```

3. On each target PC, open PowerShell as Administrator in the extracted release folder:

   ```powershell
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-service.ps1
   sc.exe query SecureDeviceControl
   ```

For a legacy installation that created the wrong service name or reports `SQLite Error 14`, use:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-service.ps1 -MigrateLegacyService -ResetLocalData
```

This preserves the prior database under `C:\ProgramData\SecureDeviceControl\recovery` and requires PIN setup again.

## Documentation

Open [documentation.html](docs/documentation.html) for the installation workflow, permissions, recovery commands, testing checklist, and current product boundary.

## Security boundary

This release is local-first and does not package cloud credentials. Remote policy, remote password operations, website/email enforcement, VPN blocking, and fleet update delivery are not enabled for production deployment. They require a separate authenticated management service and platform-specific controls.

Standard users should not be able to change the protected service or its data. An unrestricted local Windows administrator can still remove or alter third-party software; this project does not claim otherwise.
