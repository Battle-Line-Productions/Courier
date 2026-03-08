-- Create job_versions table for storing job configuration history
CREATE TABLE job_versions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id UUID NOT NULL REFERENCES jobs(id) ON DELETE CASCADE,
    version_number INTEGER NOT NULL,
    config_snapshot JSONB NOT NULL DEFAULT '{}',
    created_by TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index for querying versions by job
CREATE INDEX idx_job_versions_job_id ON job_versions (job_id, version_number);

-- Unique constraint on job_id + version_number
ALTER TABLE job_versions ADD CONSTRAINT uq_job_versions_job_version UNIQUE (job_id, version_number);
