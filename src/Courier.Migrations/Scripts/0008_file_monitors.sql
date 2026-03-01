-- ============================================================
-- FILE MONITORS
-- ============================================================
CREATE TABLE file_monitors (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                        TEXT NOT NULL,
    description                 TEXT,
    watch_target                JSONB NOT NULL,
    trigger_events              INT NOT NULL,
    file_patterns               JSONB,
    polling_interval_sec        INT NOT NULL DEFAULT 60,
    stability_window_sec        INT NOT NULL DEFAULT 0,
    batch_mode                  BOOLEAN NOT NULL DEFAULT FALSE,
    max_consecutive_failures    INT NOT NULL DEFAULT 5,
    consecutive_failure_count   INT NOT NULL DEFAULT 0,
    state                       TEXT NOT NULL DEFAULT 'active',
    last_polled_at              TIMESTAMPTZ,
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted                  BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at                  TIMESTAMPTZ,

    CONSTRAINT ck_file_monitors_state CHECK (state IN ('active', 'paused', 'disabled', 'error')),
    CONSTRAINT ck_file_monitors_polling CHECK (polling_interval_sec >= 30),
    CONSTRAINT ck_file_monitors_trigger CHECK (trigger_events > 0)
);

CREATE INDEX ix_file_monitors_name ON file_monitors (name) WHERE NOT is_deleted;
CREATE INDEX ix_file_monitors_state ON file_monitors (state) WHERE NOT is_deleted;
CREATE INDEX ix_file_monitors_polling ON file_monitors (state, last_polled_at) WHERE NOT is_deleted AND state = 'active';

-- ============================================================
-- MONITOR JOB BINDINGS
-- ============================================================
CREATE TABLE monitor_job_bindings (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    monitor_id      UUID NOT NULL,
    job_id          UUID NOT NULL,

    CONSTRAINT fk_monitor_job_bindings_monitor FOREIGN KEY (monitor_id)
        REFERENCES file_monitors (id) ON DELETE CASCADE,
    CONSTRAINT fk_monitor_job_bindings_job FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_monitor_job_bindings_unique ON monitor_job_bindings (monitor_id, job_id);
CREATE INDEX ix_monitor_job_bindings_job ON monitor_job_bindings (job_id);

-- ============================================================
-- MONITOR FILE LOGS
-- ============================================================
CREATE TABLE monitor_file_logs (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    monitor_id      UUID NOT NULL,
    file_path       TEXT NOT NULL,
    file_size       BIGINT NOT NULL DEFAULT 0,
    file_hash       TEXT,
    last_modified   TIMESTAMPTZ NOT NULL,
    triggered_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    execution_id    UUID,

    CONSTRAINT fk_monitor_file_logs_monitor FOREIGN KEY (monitor_id)
        REFERENCES file_monitors (id) ON DELETE CASCADE
);

CREATE INDEX ix_monitor_file_logs_monitor ON monitor_file_logs (monitor_id);
CREATE INDEX ix_monitor_file_logs_monitor_path ON monitor_file_logs (monitor_id, file_path);
CREATE INDEX ix_monitor_file_logs_triggered ON monitor_file_logs (triggered_at DESC);
