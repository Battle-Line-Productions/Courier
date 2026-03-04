ALTER TABLE job_executions
    ADD COLUMN paused_at       TIMESTAMPTZ,
    ADD COLUMN paused_by       TEXT,
    ADD COLUMN cancelled_at    TIMESTAMPTZ,
    ADD COLUMN cancelled_by    TEXT,
    ADD COLUMN cancel_reason   TEXT,
    ADD COLUMN requested_state TEXT;

CREATE INDEX ix_job_executions_running ON job_executions (id) WHERE state = 'running';
