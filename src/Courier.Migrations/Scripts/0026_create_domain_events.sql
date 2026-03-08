-- Create domain_events table for recording domain events
CREATE TABLE domain_events (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    event_type TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id UUID NOT NULL,
    payload JSONB,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processed_at TIMESTAMPTZ,
    processed_by TEXT,

    CONSTRAINT pk_domain_events PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

-- Create initial partitions (current quarter + next 2)
DO $$
DECLARE
    quarter_start DATE;
    quarter_end DATE;
    partition_name TEXT;
BEGIN
    FOR i IN 0..2 LOOP
        quarter_start := date_trunc('quarter', CURRENT_DATE) + (i || ' months')::interval * 3;
        quarter_end := quarter_start + interval '3 months';
        partition_name := 'domain_events_' || to_char(quarter_start, 'YYYY') || '_q' ||
                          EXTRACT(QUARTER FROM quarter_start)::int;

        EXECUTE format(
            'CREATE TABLE IF NOT EXISTS %I PARTITION OF domain_events FOR VALUES FROM (%L) TO (%L)',
            partition_name, quarter_start, quarter_end
        );
    END LOOP;
END $$;

-- Index for querying events by entity
CREATE INDEX idx_domain_events_entity ON domain_events (entity_type, entity_id);

-- Index for processing unprocessed events
CREATE INDEX idx_domain_events_unprocessed ON domain_events (occurred_at) WHERE processed_at IS NULL;
