-- Disable screen snapshots fleet-wide while keeping USB/MTP ports blocked.
-- Run once in Supabase Dashboard > SQL Editor. Safe to run repeatedly.
--
-- WHY THIS IS SAFE FOR PORTS:
--   * USB/MTP lock state (usb_storage_locked / mobile_port_locked) lives ONLY in each
--     PC's local SQLite database (C:\ProgramData\SecureDeviceControl) and is applied
--     by DeviceControlCoordinator.ApplyPolicyStatesAsync (defaults "true" = blocked).
--   * This script touches ONLY device_policies.snapshot_enabled, which the
--     SupabaseSyncWorker polls every ~30s and maps to local snapshot_enabled=false.
--   * No web/email/vpn filter, no PIN, no lock setting is changed here.

-- 1. Turn OFF snapshots for every PC that already has a policy row.
UPDATE public.device_policies
SET snapshot_enabled = false,
    updated_at = NOW();

-- 2. Make OFF the default for PCs that register in the future.
ALTER TABLE public.device_policies
    ALTER COLUMN snapshot_enabled SET DEFAULT FALSE;

-- 3. Verify: every row must be OFF, ports columns untouched (there are no
--    port columns in this table by design — ports are local-only).
SELECT email_id, machine_name, snapshot_enabled, snapshot_interval_minutes, updated_at
FROM public.device_policies
ORDER BY email_id;

-- Expected: snapshot_enabled = false on all rows.
-- On each PC within ~30s: local snapshot_enabled=false, uploads rejected with
-- "Screenshot monitoring is not enabled", USB storage + MTP stay locked.
