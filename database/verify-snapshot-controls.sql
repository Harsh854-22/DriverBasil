-- Run in Supabase Dashboard > SQL Editor after snapshot-controls.sql.
-- The script only creates a temporary table and rolls back its test rows.
-- Its final SELECT is the visible PASS/FAIL test report.

BEGIN;

CREATE TEMP TABLE snapshot_control_verification (
    test_name TEXT NOT NULL,
    status TEXT NOT NULL,
    details TEXT NOT NULL
) ON COMMIT DROP;

WITH expected_tables(table_name) AS (
    VALUES
        ('activity_logs'),
        ('registered_devices'),
        ('device_policies'),
        ('windows_password_commands'),
        ('software_updates'),
        ('remote_commands')
)
INSERT INTO snapshot_control_verification (test_name, status, details)
SELECT
    'table:' || expected_tables.table_name,
    CASE WHEN tables.table_name IS NOT NULL THEN 'PASS' ELSE 'FAIL' END,
    CASE WHEN tables.table_name IS NOT NULL THEN 'Table exists in public schema.' ELSE 'Table is missing from public schema.' END
FROM expected_tables
LEFT JOIN information_schema.tables AS tables
    ON tables.table_schema = 'public'
   AND tables.table_name = expected_tables.table_name;

WITH expected_columns(table_name, column_name, expected_type) AS (
    VALUES
        ('activity_logs', 'machine_name', 'text'),
        ('activity_logs', 'user_email', 'text'),
        ('device_policies', 'vpn_filter_mode', 'text'),
        ('device_policies', 'snapshot_enabled', 'boolean'),
        ('device_policies', 'snapshot_interval_minutes', 'integer')
)
INSERT INTO snapshot_control_verification (test_name, status, details)
SELECT
    'column:' || expected_columns.table_name || '.' || expected_columns.column_name,
    CASE WHEN columns.data_type = expected_columns.expected_type THEN 'PASS' ELSE 'FAIL' END,
    CASE
        WHEN columns.data_type = expected_columns.expected_type THEN 'Column has expected type ' || expected_columns.expected_type || '.'
        WHEN columns.data_type IS NULL THEN 'Column is missing.'
        ELSE 'Expected ' || expected_columns.expected_type || ', found ' || columns.data_type || '.'
    END
FROM expected_columns
LEFT JOIN information_schema.columns AS columns
    ON columns.table_schema = 'public'
   AND columns.table_name = expected_columns.table_name
   AND columns.column_name = expected_columns.column_name;

INSERT INTO snapshot_control_verification (test_name, status, details)
SELECT
    'constraint:device_policies.snapshot_interval_minutes',
    CASE
        WHEN EXISTS (
            SELECT 1
            FROM pg_constraint
            WHERE conrelid = to_regclass('public.device_policies')
              AND conname = 'device_policies_snapshot_interval_minutes_check'
              AND pg_get_constraintdef(oid) LIKE '%BETWEEN 1 AND 120%'
        ) THEN 'PASS'
        ELSE 'FAIL'
    END,
    'Requires snapshot_interval_minutes to be between 1 and 120.';

WITH expected_indexes(index_name) AS (
    VALUES
        ('idx_activity_logs_machine_time'),
        ('idx_windows_password_commands_pending'),
        ('idx_remote_commands_pending')
)
INSERT INTO snapshot_control_verification (test_name, status, details)
SELECT
    'index:' || expected_indexes.index_name,
    CASE WHEN indexes.indexname IS NOT NULL THEN 'PASS' ELSE 'FAIL' END,
    CASE WHEN indexes.indexname IS NOT NULL THEN 'Index exists.' ELSE 'Index is missing.' END
FROM expected_indexes
LEFT JOIN pg_indexes AS indexes
    ON indexes.schemaname = 'public'
   AND indexes.indexname = expected_indexes.index_name;

DO $$
DECLARE
    valid_email TEXT := '__snapshot_control_test_' || txid_current()::text || '@invalid.local';
BEGIN
    INSERT INTO public.device_policies (email_id, machine_name, snapshot_enabled, snapshot_interval_minutes)
    VALUES (valid_email, 'SQL-VERIFICATION', TRUE, 1);

    BEGIN
        INSERT INTO public.device_policies (email_id, machine_name, snapshot_enabled, snapshot_interval_minutes)
        VALUES ('__snapshot_control_invalid_' || txid_current()::text || '@invalid.local', 'SQL-VERIFICATION', TRUE, 121);

        INSERT INTO snapshot_control_verification (test_name, status, details)
        VALUES ('boundary:interval=121', 'FAIL', 'Out-of-range interval was accepted.');
    EXCEPTION WHEN check_violation THEN
        INSERT INTO snapshot_control_verification (test_name, status, details)
        VALUES ('boundary:interval=121', 'PASS', 'Out-of-range interval was rejected.');
    END;

    INSERT INTO snapshot_control_verification (test_name, status, details)
    VALUES ('boundary:interval=1', 'PASS', 'Minimum interval was accepted.');
END $$;

SELECT test_name, status, details
FROM snapshot_control_verification
ORDER BY status DESC, test_name;

ROLLBACK;
