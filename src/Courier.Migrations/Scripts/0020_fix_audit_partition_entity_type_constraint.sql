-- 0020_fix_audit_partition_entity_type_constraint.sql
-- Fix check constraint on audit_log_entries partitions that may have stale entity_type lists.
-- PostgreSQL may give inherited constraints different names on partitions.
-- This drops ALL entity_type check constraints across parent + partitions and re-adds correctly.

DO $$
DECLARE
    r RECORD;
BEGIN
    -- Find and drop any entity_type check constraints on audit_log_entries and its partitions
    FOR r IN
        SELECT conrelid::regclass::text AS table_name, conname
        FROM pg_constraint
        WHERE contype = 'c'
          AND conrelid::regclass::text LIKE 'audit_log_entries%'
          AND conname LIKE '%entity_type%'
    LOOP
        EXECUTE format('ALTER TABLE %s DROP CONSTRAINT IF EXISTS %I', r.table_name, r.conname);
    END LOOP;
END $$;

-- Re-add the correct constraint on the parent (cascades to all partitions)
ALTER TABLE audit_log_entries
    ADD CONSTRAINT ck_audit_entity_type
    CHECK (entity_type IN (
        'job','job_execution','step_execution','connection',
        'pgp_key','ssh_key','file_monitor','tag',
        'chain','chain_execution','notification_rule','user'
    ));
