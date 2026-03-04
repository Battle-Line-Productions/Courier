-- 0017_partition_maintenance.sql
-- Automated partition creation function for range-partitioned tables

CREATE OR REPLACE FUNCTION create_monthly_partitions(target_date DATE)
RETURNS VOID AS $$
DECLARE
    partition_start DATE;
    partition_end DATE;
    partition_name TEXT;
BEGIN
    partition_start := date_trunc('month', target_date)::DATE;
    partition_end := (partition_start + INTERVAL '1 month')::DATE;
    partition_name := 'audit_log_entries_' || to_char(partition_start, 'YYYY_MM');

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I PARTITION OF audit_log_entries FOR VALUES FROM (%L) TO (%L)',
        partition_name,
        partition_start,
        partition_end
    );
END;
$$ LANGUAGE plpgsql;

-- Seed partitions for current and next 2 months
SELECT create_monthly_partitions(CURRENT_DATE);
SELECT create_monthly_partitions((CURRENT_DATE + INTERVAL '1 month')::DATE);
SELECT create_monthly_partitions((CURRENT_DATE + INTERVAL '2 months')::DATE);

-- Retention setting (V1: informational only, no auto-drop)
INSERT INTO system_settings (key, value, description, updated_by)
VALUES ('audit.partition_retention_months', '12', 'Months to retain before archival', 'system')
ON CONFLICT (key) DO NOTHING;
