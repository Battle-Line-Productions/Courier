-- 0011_audit_log_add_tag_entity_type.sql
-- Add 'tag' to the audit_log_entries entity_type check constraint
-- Required after 0010_tags.sql introduced the Tag entity

ALTER TABLE audit_log_entries DROP CONSTRAINT ck_audit_entity_type;

ALTER TABLE audit_log_entries ADD CONSTRAINT ck_audit_entity_type CHECK (
    entity_type IN ('job','job_execution','step_execution','chain',
                    'chain_execution','connection','pgp_key','ssh_key','file_monitor','tag')
);
