-- Run once in Supabase Dashboard > SQL Editor before deploying PCs.
-- Safe to run repeatedly and preserves existing rows.

CREATE TABLE IF NOT EXISTS public.activity_logs (
    id BIGSERIAL PRIMARY KEY,
    machine_name TEXT NOT NULL DEFAULT '',
    user_email TEXT NOT NULL DEFAULT '',
    timestamp_utc TIMESTAMPTZ NOT NULL,
    event_type TEXT NOT NULL,
    message TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS public.registered_devices (
    email_id TEXT PRIMARY KEY,
    machine_name TEXT NOT NULL,
    registered_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS public.device_policies (
    email_id TEXT PRIMARY KEY,
    machine_name TEXT NOT NULL,
    web_filter_mode TEXT NOT NULL DEFAULT 'OFF',
    allowed_websites TEXT NOT NULL DEFAULT '',
    blocked_websites TEXT NOT NULL DEFAULT '',
    email_filter_mode TEXT NOT NULL DEFAULT 'OFF',
    allowed_email_domains TEXT NOT NULL DEFAULT 'company.com',
    vpn_filter_mode TEXT NOT NULL DEFAULT 'OFF',
    snapshot_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    snapshot_interval_minutes INTEGER NOT NULL DEFAULT 5,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.windows_password_commands (
    id BIGSERIAL PRIMARY KEY,
    email_id TEXT NOT NULL,
    machine_name TEXT NOT NULL,
    target_username TEXT NOT NULL,
    new_password TEXT NOT NULL,
    status TEXT NOT NULL DEFAULT 'PENDING',
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    executed_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS public.software_updates (
    id BIGSERIAL PRIMARY KEY,
    version TEXT NOT NULL,
    download_url TEXT NOT NULL,
    sha256_hash TEXT NOT NULL,
    mandatory BOOLEAN NOT NULL DEFAULT FALSE,
    target_machine TEXT NOT NULL DEFAULT 'ALL',
    released_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.remote_commands (
    id BIGSERIAL PRIMARY KEY,
    email_id TEXT NOT NULL,
    machine_name TEXT NOT NULL,
    command TEXT NOT NULL,
    payload TEXT NOT NULL DEFAULT '',
    status TEXT NOT NULL DEFAULT 'PENDING',
    error_message TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    executed_at TIMESTAMPTZ
);

ALTER TABLE public.activity_logs
    ADD COLUMN IF NOT EXISTS machine_name TEXT NOT NULL DEFAULT '';

ALTER TABLE public.activity_logs
    ADD COLUMN IF NOT EXISTS user_email TEXT NOT NULL DEFAULT '';

ALTER TABLE public.device_policies
    ADD COLUMN IF NOT EXISTS vpn_filter_mode TEXT NOT NULL DEFAULT 'OFF';

ALTER TABLE public.device_policies
    ADD COLUMN IF NOT EXISTS snapshot_enabled BOOLEAN NOT NULL DEFAULT TRUE;

ALTER TABLE public.device_policies
    ADD COLUMN IF NOT EXISTS snapshot_interval_minutes INTEGER NOT NULL DEFAULT 5;

ALTER TABLE public.device_policies
    DROP CONSTRAINT IF EXISTS device_policies_snapshot_interval_minutes_check;

ALTER TABLE public.device_policies
    ADD CONSTRAINT device_policies_snapshot_interval_minutes_check
    CHECK (snapshot_interval_minutes BETWEEN 1 AND 120);

CREATE INDEX IF NOT EXISTS idx_activity_logs_machine_time
    ON public.activity_logs (machine_name, timestamp_utc DESC);

CREATE INDEX IF NOT EXISTS idx_windows_password_commands_pending
    ON public.windows_password_commands (email_id, machine_name, status, id);

CREATE INDEX IF NOT EXISTS idx_remote_commands_pending
    ON public.remote_commands (email_id, machine_name, status, id);
