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

## Supabase setup

Before the first deployment, run [snapshot-controls.sql](/C:/Users/admin/Desktop/DriverBasil/DriverBasil/database/snapshot-controls.sql) once in **Supabase Dashboard > SQL Editor**. This creates or upgrades the tables used for registered devices, activity logs, device policies, snapshots, password reset commands, remote uninstall commands, and software updates.

Create a private Supabase Storage bucket named `Snapshots`. The service needs these machine-level environment variables; do not store the values in source control or in `appsettings.json`:

```powershell
[Environment]::SetEnvironmentVariable('ConnectionStrings__Supabase', '<Supabase Postgres connection string>', 'Machine')
[Environment]::SetEnvironmentVariable('Supabase__StorageServiceRoleKey', '<Supabase service-role key>', 'Machine')
```

After setting or rotating either value, restart `SecureDeviceControl`. The supplied `appsettings.json` contains only the project URL and bucket name, never credentials.

## Screen snapshots

During first-run registration, the person using the PC must acknowledge that screen snapshots are enabled. The desktop app then registers itself to start when that Windows user signs in. It captures the visible virtual desktop, including connected monitors, at the configured interval and sends JPEG images to the private Supabase Storage bucket. After acknowledgement, there is no repeating screenshot notice in the desktop UI.

The default is enabled every 5 minutes. Change it per device in the `device_policies` table:

```sql
UPDATE device_policies
SET snapshot_enabled = true,
    snapshot_interval_minutes = 5
WHERE email_id = 'employee@company.com';
```

The service polls that policy every 30 seconds. Set `snapshot_enabled` to `false` to pause captures. The person's registration acknowledgement remains recorded locally, so a remote policy cannot activate screenshots before it has been disclosed during registration.

## Remote Windows password resets

The service can process a pending row in `windows_password_commands` for the registered email ID and machine name. It runs during the normal cloud poll, so a restart is not required. The command result is retained as `COMPLETED` or `FAILED`, but the submitted replacement password is cleared immediately after processing. This is a reset mechanism, not an archive of employees' current Windows passwords.

```sql
INSERT INTO windows_password_commands (email_id, machine_name, target_username, new_password)
VALUES ('employee@company.com', 'OFFICE-PC-01', 'localuser', 'NewStrongPassword123!');
```

## Remote uninstall

To remove the installed service, desktop app files, and snapshot startup entry from one PC, insert a pending `UNINSTALL` command:

```sql
INSERT INTO remote_commands (email_id, machine_name, command)
VALUES ('employee@company.com', 'OFFICE-PC-01', 'UNINSTALL');
```

To also remove the local service data directory on that PC, pass this payload:

```sql
INSERT INTO remote_commands (email_id, machine_name, command, payload)
VALUES ('employee@company.com', 'OFFICE-PC-01', 'UNINSTALL', '{"removeData":true}');
```

The target service polls about every 30 seconds, unlocks local hardware policy, marks the command `COMPLETED`, removes the Windows service, removes the installed app directory under `C:\Program Files\SecureDeviceControl`, and removes the snapshot startup entry.

## Remote software updates

Publish a release ZIP or installer to a URL reachable by the PCs, then insert a `software_updates` row:

```sql
INSERT INTO software_updates (version, download_url, sha256_hash, mandatory, target_machine)
VALUES ('1.0.1', 'https://example.com/SecureDeviceControl-Release.zip', '<sha256>', true, 'ALL');
```

Use a specific `target_machine` instead of `ALL` to update only one PC.

## Security boundary

Standard users should not be able to change the protected service or its data. An unrestricted local Windows administrator can still remove or alter third-party software; this project does not claim otherwise.

Because a service-role key bypasses Storage policies, keep the bucket private and rotate any key or database password that was shared outside your normal secret-management process before deploying to the 50 PCs.
