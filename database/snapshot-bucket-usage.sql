-- Exact Snapshots bucket size for the ledger meter.
-- Run once in Supabase Dashboard > SQL Editor. Safe to run again.
-- Only the service_role key (the ledger) can call it. anon cannot.

CREATE OR REPLACE FUNCTION public.snapshot_bucket_bytes()
RETURNS bigint
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = storage, public
AS $$
  SELECT COALESCE(SUM((metadata->>'size')::bigint), 0)::bigint
  FROM storage.objects
  WHERE bucket_id = 'Snapshots';
$$;

REVOKE ALL ON FUNCTION public.snapshot_bucket_bytes() FROM PUBLIC;
REVOKE ALL ON FUNCTION public.snapshot_bucket_bytes() FROM anon, authenticated;
GRANT EXECUTE ON FUNCTION public.snapshot_bucket_bytes() TO service_role;
