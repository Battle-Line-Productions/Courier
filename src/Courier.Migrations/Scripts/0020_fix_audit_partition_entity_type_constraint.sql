-- 0020_fix_audit_partition_entity_type_constraint.sql
-- Re-apply entity_type check constraint with CASCADE to handle partitions.

ALTER TABLE audit_log_entries DROP CONSTRAINT IF EXISTS ck_audit_entity_type CASCADE;

ALTER TABLE audit_log_entries
    ADD CONSTRAINT ck_audit_entity_type
    CHECK (entity_type IN (
        'job','job_execution','step_execution','connection',
        'pgp_key','ssh_key','file_monitor','tag',
        'chain','chain_execution','notification_rule','user'
    ));
