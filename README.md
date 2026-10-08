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

## Replacing old offline-only PCs (no Supabase, port-locking only)

The current release works fully offline — locking, PINs, and logs are local; cloud sync just retries harmlessly until online. To upgrade an offline PC, copy the latest `SecureDeviceControl-Release.zip` via USB and install **in place** (keeps PINs, Email, logs — no re-registration):

```powershell
Set-Location "C:\Temp\SDC-Update\SecureDeviceControl-Release"
Unblock-File -Path .\*
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-service.ps1
Start-Sleep 10; sc.exe query SecureDeviceControl
```

Add `-MigrateLegacyService` only if a service literally named `Secure Device Control` (with a space) exists. Back up `C:\ProgramData\SecureDeviceControl\secure-device-control.db*` first. Full easy step-by-step with verification checks: open `docs/documentation.html` > **Replace Old Offline-Only PCs**.

## Zero-touch install (recommended for every PC)

Installs the service **and** sets the PINs in one command — no desktop app needed. Type the Email when prompted, hit Enter, ports lock:

```powershell
Set-Location "C:\Path\To\SecureDeviceControl-Release"
Unblock-File -Path .\*
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-service.ps1 -AutoProvision
```

Defaults: Device Unlock PIN `421301`, Uninstall PIN `121356` (override with `-DeviceUnlockPin` / `-UninstallPin`, snapshots stay OFF unless `-AcknowledgeSnapshots:$true`). Fully unattended: add `-UserEmail "pc-07@company.com" -NonInteractive`. Works on fresh PCs, legacy-service PCs, and any previous version (existing Email kept, PINs reset, corrupt DB quarantined to `recovery\` never deleted). Rotate the bootstrap PINs per-PC via remote `UPDATE_DEVICE_PIN` / `UPDATE_UNINSTALL_PIN` once online. Details: `docs/documentation.html` > **Zero-Touch Install**.

## Supabase setup

Before the first deployment, run [snapshot-controls.sql](/C:/Users/admin/Desktop/DriverBasil/DriverBasil/database/snapshot-controls.sql) once in **Supabase Dashboard > SQL Editor**. This creates or upgrades the tables used for registered devices, activity logs, device policies, snapshots, password reset commands, remote uninstall commands, and software updates.

Create a private Supabase Storage bucket named `Snapshots`, then run `database/secure-bucket-and-rls-lockdown.sql` once in **Supabase Dashboard > SQL Editor**. It forces the bucket private and replaces any permissive policy with an INSERT-only policy: release software can upload snapshots but can never list, download, or delete them.

The release ZIP is handed to every PC, so it must be treated as public: `appsettings.json` ships only the project URL, bucket name, and the public anon key (`StorageAnonKey` — safe to share by design). It never contains the DB password or the service-role key, and the service never reads secrets from it.

Give each PC its cloud credentials at install time (pick one — file is preferred):

```powershell
# Option A (preferred): restricted credentials file, SYSTEM+Administrators only.
# Keep this file OFF the repo/ZIP — carry it on the admin USB only.
@{ ConnectionString = '<Supabase Postgres connection string>'
   StorageKey = '<anon key, or omit if baked>' } |
  ConvertTo-Json | Set-Content .\cloud-credentials.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\install-service.ps1 -AutoProvision -CloudCredentialsFile .\cloud-credentials.json

# Option B: machine environment variables (kept for existing installs).
[Environment]::SetEnvironmentVariable('ConnectionStrings__Supabase', '<Supabase Postgres connection string>', 'Machine')
[Environment]::SetEnvironmentVariable('Supabase__StorageAnonKey', '<Supabase anon key>', 'Machine')
```

The service-role key lives ONLY on the admin dashboard PC (DPAPI-protected in its settings). Without credentials a PC runs offline: USB/MTP ports stay blocked, PINs and local logs keep working, cloud sync just retries.

After setting or rotating any credential, restart `SecureDeviceControl`.

## Screen snapshots (OFF by default — ports stay blocked)

Snapshots are disabled fleet-wide. To turn them off immediately on all PCs, run `database/disable-snapshots-keep-ports-blocked.sql` once in **Supabase Dashboard > SQL Editor** — every PC picks it up within ~30s. Port locks are local-only (`usb_storage_locked` / `mobile_port_locked`, default blocked) and are not touched by that script.

During first-run registration, the person using the PC must acknowledge that screen snapshots are enabled before any capture can happen. The desktop app then registers itself to start when that Windows user signs in. It captures the visible virtual desktop, including connected monitors, at the configured interval and sends JPEG images to the private Supabase Storage bucket. After acknowledgement, there is no repeating screenshot notice in the desktop UI.

The default is disabled. To opt a single device back in:

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

Standard users should not be able to change the protected service or its data. An unrestricted local Windows administrator can still remove or alter third-party software; this project does not claim otherwise. Local admins can also extract any credential stored on their PC — treat admin PCs as trusted, and rotate keys if one is compromised.

Because a service-role key bypasses Storage policies, it must never ship in the release ZIP (package-zip.ps1 strips it and fails the build if one is found). If any key or database password was ever baked into a ZIP, committed, or shared outside your normal secret-management process: rotate the service-role key (Supabase Dashboard > Project Settings > API), reset the database password (Project Settings > Database), delete the old ZIPs, and rebuild before deploying to the 50 PCs.
