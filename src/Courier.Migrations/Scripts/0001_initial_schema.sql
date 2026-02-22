-- ============================================================
-- JOBS
-- ============================================================
CREATE TABLE jobs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    description         TEXT,
    current_version     INT NOT NULL DEFAULT 1,
    failure_policy      JSONB NOT NULL DEFAULT '{"type":"stop","max_retries":3,"backoff_base_seconds":1,"backoff_max_seconds":60}',
    is_enabled          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ
);

CREATE INDEX ix_jobs_name ON jobs (name) WHERE NOT is_deleted;
CREATE INDEX ix_jobs_is_enabled ON jobs (is_enabled) WHERE NOT is_deleted;
