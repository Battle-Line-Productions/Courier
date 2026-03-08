-- Add health metrics columns to file_monitors
ALTER TABLE file_monitors ADD COLUMN IF NOT EXISTS last_poll_duration_ms BIGINT;
ALTER TABLE file_monitors ADD COLUMN IF NOT EXISTS last_poll_file_count INTEGER;
ALTER TABLE file_monitors ADD COLUMN IF NOT EXISTS last_overflow_at TIMESTAMPTZ;
ALTER TABLE file_monitors ADD COLUMN IF NOT EXISTS overflow_count_24h INTEGER NOT NULL DEFAULT 0;
