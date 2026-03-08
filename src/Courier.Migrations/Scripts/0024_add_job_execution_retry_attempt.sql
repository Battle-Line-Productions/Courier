-- Add retry_attempt column to job_executions for RetryJob failure policy tracking
ALTER TABLE job_executions ADD COLUMN IF NOT EXISTS retry_attempt INTEGER;
