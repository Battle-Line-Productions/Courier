-- Add alias column to job_steps for stable context variable references
ALTER TABLE job_steps ADD COLUMN alias TEXT;

-- Unique alias per job (partial index: only non-null aliases)
CREATE UNIQUE INDEX ix_job_steps_job_alias ON job_steps (job_id, alias) WHERE alias IS NOT NULL;

-- Alias format: lowercase letter start, alphanumeric + underscore, max 50 chars
ALTER TABLE job_steps ADD CONSTRAINT ck_job_steps_alias_format
    CHECK (alias IS NULL OR alias ~ '^[a-z][a-z0-9_]{0,49}$');
