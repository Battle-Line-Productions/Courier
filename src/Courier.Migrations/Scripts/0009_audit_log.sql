-- 0009_audit_log.sql
-- Append-only audit log with range partitioning on performed_at

CREATE TABLE audit_log_entries (
    id              UUID NOT NULL DEFAULT gen_random_uuid(),
    entity_type     TEXT NOT NULL,
    entity_id       UUID NOT NULL,
    operation       TEXT NOT NULL,
    performed_by    TEXT NOT NULL,
    performed_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    details         JSONB DEFAULT '{}',
    CONSTRAINT pk_audit_log PRIMARY KEY (id, performed_at),
    CONSTRAINT ck_audit_entity_type CHECK (
        entity_type IN ('job','job_execution','step_execution','chain',
                        'chain_execution','connection','pgp_key','ssh_key','file_monitor')
    )
) PARTITION BY RANGE (performed_at);

-- Indexes
CREATE INDEX ix_audit_entity ON audit_log_entries (entity_type, entity_id, performed_at DESC);
CREATE INDEX ix_audit_performed_at ON audit_log_entries (performed_at DESC);
CREATE INDEX ix_audit_performed_by ON audit_log_entries (performed_by, performed_at DESC);

-- Initial partitions
CREATE TABLE audit_log_entries_2026_03 PARTITION OF audit_log_entries
    FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
CREATE TABLE audit_log_entries_default PARTITION OF audit_log_entries DEFAULT;

-- Append-only protection
CREATE OR REPLACE FUNCTION prevent_audit_log_modification()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'audit_log_entries is append-only: % not permitted', TG_OP;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_audit_log_no_update
    BEFORE UPDATE ON audit_log_entries
    FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_modification();

CREATE TRIGGER trg_audit_log_no_delete
    BEFORE DELETE ON audit_log_entries
    FOR EACH ROW EXECUTE FUNCTION prevent_audit_log_modification();
