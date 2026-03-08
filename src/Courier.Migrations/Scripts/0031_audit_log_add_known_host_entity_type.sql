-- 0031_audit_log_add_known_host_entity_type.sql
-- Add 'known_host' to the audit_log_entries entity_type check constraint.

DO $$
DECLARE
    r RECORD;
BEGIN
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

ALTER TABLE audit_log_entries
    ADD CONSTRAINT ck_audit_entity_type
    CHECK (entity_type IN (
        'job','job_execution','step_execution','connection',
        'pgp_key','ssh_key','file_monitor','tag',
        'chain','chain_execution','notification_rule','user',
        'known_host'
    ));
