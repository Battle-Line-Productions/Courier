-- 0031_audit_log_add_known_host_entity_type.sql
-- Add 'known_host' to the audit_log_entries entity_type check constraint.

ALTER TABLE audit_log_entries DROP CONSTRAINT IF EXISTS ck_audit_entity_type CASCADE;

ALTER TABLE audit_log_entries
    ADD CONSTRAINT ck_audit_entity_type
    CHECK (entity_type IN (
        'job','job_execution','step_execution','connection',
        'pgp_key','ssh_key','file_monitor','tag',
        'chain','chain_execution','notification_rule','user',
        'known_host'
    ));
