-- 0030_partition_execution_tables.sql
-- Convert job_executions and step_executions to monthly range-partitioned tables.
-- PostgreSQL does not support ALTER TABLE ... PARTITION BY on existing tables,
-- so we must recreate them.

-- ============================================================
-- 1. Rename existing tables
-- ============================================================
ALTER TABLE step_executions RENAME TO step_executions_old;
ALTER TABLE job_executions RENAME TO job_executions_old;

-- Drop old indexes (they were renamed with the table)
DROP INDEX IF EXISTS ix_step_executions_job_execution;
DROP INDEX IF EXISTS ix_step_executions_state;
DROP INDEX IF EXISTS ix_job_executions_job_id;
DROP INDEX IF EXISTS ix_job_executions_state;
DROP INDEX IF EXISTS ix_job_executions_queued;
DROP INDEX IF EXISTS ix_job_executions_running;
DROP INDEX IF EXISTS ix_job_executions_chain;

-- ============================================================
-- 2. Create partitioned job_executions
-- ============================================================
CREATE TABLE job_executions (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    job_version_number  INT NOT NULL DEFAULT 1,
    triggered_by        TEXT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'created',
    queued_at           TIMESTAMPTZ,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    context_snapshot    JSONB DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    paused_at           TIMESTAMPTZ,
    paused_by           TEXT,
    cancelled_at        TIMESTAMPTZ,
    cancelled_by        TEXT,
    cancel_reason       TEXT,
    requested_state     TEXT,
    retry_attempt       INTEGER,
    chain_execution_id  UUID,

    -- No PK constraint: PostgreSQL requires partition key in all unique constraints,
    -- but EF Core uses HasKey(e => e.Id) which expects a single-column key.
    -- UUIDv7 IDs are globally unique, so DB-level uniqueness is guaranteed de facto.
    CONSTRAINT fk_job_executions_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_executions_state CHECK (
        state IN ('created', 'queued', 'running', 'paused', 'completed', 'failed', 'cancelled')
    )
) PARTITION BY RANGE (created_at);

CREATE INDEX ix_job_executions_id ON job_executions (id);
CREATE INDEX ix_job_executions_job_id ON job_executions (job_id, created_at DESC);
CREATE INDEX ix_job_executions_state ON job_executions (state, created_at DESC);
CREATE INDEX ix_job_executions_queued ON job_executions (queued_at) WHERE state = 'queued';
CREATE INDEX ix_job_executions_running ON job_executions (id) WHERE state = 'running';
CREATE INDEX ix_job_executions_chain ON job_executions (chain_execution_id, created_at DESC)
    WHERE chain_execution_id IS NOT NULL;

-- ============================================================
-- 3. Create partitioned step_executions
-- ============================================================
CREATE TABLE step_executions (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
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
    iteration_index     INT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- No PK constraint for partition compatibility with EF Core single-column keys.
    CONSTRAINT fk_step_executions_job_steps FOREIGN KEY (job_step_id)
        REFERENCES job_steps (id) ON DELETE CASCADE,
    CONSTRAINT ck_step_executions_state CHECK (
        state IN ('pending', 'running', 'completed', 'failed', 'skipped')
    )
) PARTITION BY RANGE (created_at);

CREATE INDEX ix_step_executions_id ON step_executions (id);
CREATE INDEX ix_step_executions_job_execution ON step_executions (job_execution_id, step_order);
CREATE INDEX ix_step_executions_state ON step_executions (state, created_at DESC);

-- ============================================================
-- 4. Create partition function for execution tables
-- ============================================================
CREATE OR REPLACE FUNCTION create_execution_monthly_partitions(target_date DATE)
RETURNS VOID AS $$
DECLARE
    partition_start DATE;
    partition_end DATE;
    je_partition TEXT;
    se_partition TEXT;
BEGIN
    partition_start := date_trunc('month', target_date)::DATE;
    partition_end := (partition_start + INTERVAL '1 month')::DATE;

    je_partition := 'job_executions_' || to_char(partition_start, 'YYYY_MM');
    se_partition := 'step_executions_' || to_char(partition_start, 'YYYY_MM');

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF job_executions FOR VALUES FROM (%L) TO (%L)',
        je_partition, partition_start, partition_end
    );

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF step_executions FOR VALUES FROM (%L) TO (%L)',
        se_partition, partition_start, partition_end
    );
END;
$$ LANGUAGE plpgsql;

-- ============================================================
-- 5. Create partitions for past data + current + future months
--    Going back 12 months to cover any existing data, plus 3 months ahead
-- ============================================================
DO $$
DECLARE
    i INT;
    target DATE;
BEGIN
    -- Past 12 months + current + next 2 months = 15 iterations
    FOR i IN -12..2 LOOP
        target := (CURRENT_DATE + (i || ' months')::INTERVAL)::DATE;
        PERFORM create_execution_monthly_partitions(target);
    END LOOP;
END $$;

-- Also create a default partition for any data outside the range
CREATE TABLE IF NOT EXISTS job_executions_default PARTITION OF job_executions DEFAULT;
CREATE TABLE IF NOT EXISTS step_executions_default PARTITION OF step_executions DEFAULT;

-- ============================================================
-- 6. Migrate data from old tables
-- ============================================================
INSERT INTO job_executions (
    id, job_id, job_version_number, triggered_by, state,
    queued_at, started_at, completed_at, context_snapshot, created_at,
    paused_at, paused_by, cancelled_at, cancelled_by, cancel_reason,
    requested_state, retry_attempt, chain_execution_id
)
SELECT
    id, job_id, job_version_number, triggered_by, state,
    queued_at, started_at, completed_at, context_snapshot, created_at,
    paused_at, paused_by, cancelled_at, cancelled_by, cancel_reason,
    requested_state, retry_attempt, chain_execution_id
FROM job_executions_old;

INSERT INTO step_executions (
    id, job_execution_id, job_step_id, step_order, state,
    started_at, completed_at, duration_ms, bytes_processed,
    output_data, error_message, error_stack_trace, retry_attempt, iteration_index, created_at
)
SELECT
    id, job_execution_id, job_step_id, step_order, state,
    started_at, completed_at, duration_ms, bytes_processed,
    output_data, error_message, error_stack_trace, retry_attempt, iteration_index, created_at
FROM step_executions_old;

-- ============================================================
-- 7. Drop old tables
-- ============================================================
DROP TABLE step_executions_old;
DROP TABLE job_executions_old;

-- ============================================================
-- 8. Update any FK references from other tables
--    monitor_file_logs.execution_id references job_executions
-- ============================================================
-- The FK from monitor_file_logs to job_executions (if it exists) needs
-- to reference the unique index on id rather than the composite PK.
-- Since monitor_file_logs was created without an explicit FK constraint
-- to job_executions, no action is needed here.

-- ============================================================
-- 9. Add retention setting for execution partitions
-- ============================================================
INSERT INTO system_settings (key, value, description, updated_by)
VALUES ('execution.partition_retention_months', '12', 'Months to retain execution data before archival', 'system')
ON CONFLICT (key) DO NOTHING;
