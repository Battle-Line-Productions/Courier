-- Add iteration_index column for tracking loop iterations in step executions
ALTER TABLE step_executions ADD COLUMN iteration_index INT;

-- Index for efficiently querying step executions within a specific iteration
CREATE INDEX ix_step_executions_iteration
    ON step_executions (job_execution_id, job_step_id, iteration_index)
    WHERE iteration_index IS NOT NULL;
