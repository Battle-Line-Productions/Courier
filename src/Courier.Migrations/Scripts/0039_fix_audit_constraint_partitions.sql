-- 0039_fix_audit_constraint_partitions.sql
-- Safety net: ensure ck_audit_entity_type constraint is correct on partitioned audit_log_entries.
-- Earlier migrations were fixed to use CASCADE, but this ensures existing DBs are consistent.

ALTER TABLE audit_log_entries DROP CONSTRAINT IF EXISTS ck_audit_entity_type CASCADE;

ALTER TABLE audit_log_entries
    ADD CONSTRAINT ck_audit_entity_type
    CHECK (entity_type IN (
        'job', 'job_execution', 'step_execution', 'connection',
        'pgp_key', 'ssh_key', 'file_monitor', 'tag', 'chain',
        'chain_execution', 'notification_rule', 'user', 'known_host',
        'auth_provider'
    ));
