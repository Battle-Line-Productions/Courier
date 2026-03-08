-- Seed job.concurrency_limit system setting (default 5) if not already present
-- Note: this setting is already seeded in 0002_job_engine_tables.sql; this is a safety net.
INSERT INTO system_settings (key, value, description, updated_at, updated_by)
SELECT 'job.concurrency_limit', '5', 'Maximum concurrent job executions', NOW(), 'system'
WHERE NOT EXISTS (SELECT 1 FROM system_settings WHERE key = 'job.concurrency_limit');
