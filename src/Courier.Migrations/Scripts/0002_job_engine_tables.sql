-- ============================================================
-- JOB STEPS
-- ============================================================
CREATE TABLE job_steps (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    step_order          INT NOT NULL,
    name                TEXT NOT NULL,
    type_key            TEXT NOT NULL,
    configuration       JSONB NOT NULL DEFAULT '{}',
    timeout_seconds     INT NOT NULL DEFAULT 300,

    CONSTRAINT fk_job_steps_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_steps_order_positive CHECK (step_order >= 0),
    CONSTRAINT ck_job_steps_timeout_positive CHECK (timeout_seconds > 0)
);

CREATE UNIQUE INDEX ix_job_steps_job_order ON job_steps (job_id, step_order);
CREATE INDEX ix_job_steps_type_key ON job_steps (type_key);

-- ============================================================
-- JOB EXECUTIONS (non-partitioned for V1 dev)
-- ============================================================
CREATE TABLE job_executions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    job_version_number  INT NOT NULL DEFAULT 1,
    triggered_by        TEXT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'created',
    queued_at           TIMESTAMPTZ,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    context_snapshot    JSONB DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_job_executions_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_executions_state CHECK (
        state IN ('created', 'queued', 'running', 'paused', 'completed', 'failed', 'cancelled')
    )
);

CREATE INDEX ix_job_executions_job_id ON job_executions (job_id, created_at DESC);
CREATE INDEX ix_job_executions_state ON job_executions (state, created_at DESC);
CREATE INDEX ix_job_executions_queued ON job_executions (queued_at)
    WHERE state = 'queued';

-- ============================================================
-- STEP EXECUTIONS (non-partitioned for V1 dev)
-- ============================================================
CREATE TABLE step_executions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_execution_id    UUID NOT NULL,
    job_step_id         UUID NOT NULL,
    step_order          INT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'pending',
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    duration_ms         BIGINT,
    bytes_processed     BIGINT,
    output_data         JSONB,
    error_message       TEXT,
    error_stack_trace   TEXT,
    retry_attempt       INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_step_executions_job_executions FOREIGN KEY (job_execution_id)
        REFERENCES job_executions (id) ON DELETE CASCADE,
    CONSTRAINT fk_step_executions_job_steps FOREIGN KEY (job_step_id)
        REFERENCES job_steps (id) ON DELETE CASCADE,
    CONSTRAINT ck_step_executions_state CHECK (
        state IN ('pending', 'running', 'completed', 'failed', 'skipped')
    )
);

CREATE INDEX ix_step_executions_job_execution ON step_executions (job_execution_id, step_order);
CREATE INDEX ix_step_executions_state ON step_executions (state, created_at DESC);

-- ============================================================
-- SYSTEM SETTINGS
-- ============================================================
CREATE TABLE system_settings (
    key                 TEXT PRIMARY KEY,
    value               TEXT NOT NULL,
    description         TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          TEXT NOT NULL
);

INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('job.concurrency_limit', '5', 'Maximum concurrent job executions', 'system'),
    ('job.temp_cleanup_days', '7', 'Days before orphaned temp directories are purged', 'system');
