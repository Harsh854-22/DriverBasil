-- Lock down the Snapshots bucket + API surface after the release ZIP shipped
-- a service_role key and DB password. Run once in Supabase Dashboard > SQL Editor.
-- Safe to run repeatedly. Ports are unaffected (local-only policy on each PC).
--
-- WHAT THIS DOES
--   1. Forces bucket Snapshots to private (no public URL access).
--   2. Removes any permissive Storage policies on Snapshots and replaces them
--      with a single INSERT-only policy for anon/authenticated: release software
--      (which now ships only the public anon key) can UPLOAD JPEGs but can never
--      LIST, DOWNLOAD, UPDATE, or DELETE anyone's snapshots.
--   3. Enables RLS on the app tables so the anon key gets deny-by-default on
--      everything except log INSERTs. The service (Npgsql as table owner) and
--      the admin dashboard (service_role) bypass RLS and keep working.
--
-- WHAT THIS DOES NOT DO (do these in the Dashboard UI afterwards)
--   A. Rotate the leaked service_role key: Dashboard > Project Settings > API >
--      "Generate a new service_role key". The OLD key in old ZIPs dies instantly.
--      Then update ONLY the admin dashboard PC (Settings > service_role).
--   B. Reset the database password: Dashboard > Project Settings > Database >
--      "Reset database password". Then update the install-time credentials on PCs
--      (env var ConnectionStrings__Supabase or cloud-credentials.json) — the new
--      release never reads passwords from appsettings.json.
--   C. Delete old SecureDeviceControl-Release.zip / Dashboard.zip copies that
--      contain the baked key, and rebuild the ZIP (package-zip.ps1 now strips
--      secrets and fails the build if any remain).

-- 1. Bucket private.
UPDATE storage.buckets
SET public = false
WHERE id = 'Snapshots';

-- 2. Drop every existing Storage policy that touches the Snapshots bucket.
DO $$
DECLARE
    pol RECORD;
BEGIN
    FOR pol IN
        SELECT policyname
        FROM pg_policies
        WHERE schemaname = 'storage'
          AND tablename = 'objects'
          AND (qual LIKE '%Snapshots%' OR with_check LIKE '%Snapshots%')
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS %I ON storage.objects;', pol.policyname);
    END LOOP;
END
$$;

-- 3. INSERT-only for anon/authenticated: upload JPEGs, nothing else.
CREATE POLICY "Snapshots anon insert-only"
    ON storage.objects
    FOR INSERT
    TO anon, authenticated
    WITH CHECK (bucket_id = 'Snapshots');

-- No SELECT / UPDATE / DELETE policy for anon on this bucket: with RLS enabled
-- (Supabase default for storage.objects), anything without a policy is denied.
-- service_role bypasses RLS by design — that is why step A (rotation) matters.

-- 4. RLS on app tables: deny-by-default for anon; owner/service_role unaffected.
ALTER TABLE public.activity_logs ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.registered_devices ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.device_policies ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.windows_password_commands ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.remote_commands ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.software_updates ENABLE ROW LEVEL SECURITY;

-- Endpoints never read via PostgREST today, but allow log INSERTs for anon so a
-- future least-privilege switch stays possible. Nothing else is granted.
DROP POLICY IF EXISTS "activity_logs anon insert-only" ON public.activity_logs;
CREATE POLICY "activity_logs anon insert-only"
    ON public.activity_logs
    FOR INSERT
    TO anon, authenticated
    WITH CHECK (true);

-- 5. Verification (expect: private=t, exactly 1 insert policy, no select policies).
SELECT id AS bucket, public AS is_public,
       CASE WHEN public = false THEN 'PASS' ELSE 'FAIL' END AS status
FROM storage.buckets
WHERE id = 'Snapshots';

SELECT policyname, roles, cmd,
       CASE WHEN cmd = 'INSERT' THEN 'PASS (upload only)' ELSE 'REVIEW' END AS status
FROM pg_policies
WHERE schemaname = 'storage'
  AND tablename = 'objects'
  AND (qual LIKE '%Snapshots%' OR with_check LIKE '%Snapshots%')
ORDER BY cmd, policyname;

SELECT tablename,
       CASE WHEN rowsecurity THEN 'PASS (RLS on)' ELSE 'FAIL (RLS off)' END AS status
FROM pg_tables
WHERE schemaname = 'public'
  AND tablename IN ('activity_logs', 'registered_devices', 'device_policies',
                    'windows_password_commands', 'remote_commands', 'software_updates')
ORDER BY tablename;

-- 6. FUTURE HARDENING (optional, bigger change): stop giving PCs the postgres
--    superuser password entirely. In Dashboard > Database create a least-privilege
--    login role (e.g. sdc_agent), then GRANT only:
--      CONNECT + USAGE + SELECT on sequences,
--      INSERT on activity_logs,
--      SELECT, INSERT on registered_devices + device_policies,
--      SELECT on software_updates,
--      SELECT + UPDATE(status, error_message, executed_at) on
--        windows_password_commands + remote_commands,
--    put THAT connection string in the install-time credentials of each PC, and
--    keep the superuser password on the admin side only. The service code already
--    reads the connection string from env/file, so no rebuild is needed for this.
