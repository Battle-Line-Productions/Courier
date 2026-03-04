CREATE TABLE notification_rules (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name            TEXT NOT NULL,
    description     TEXT,
    entity_type     TEXT NOT NULL,
    entity_id       UUID,
    event_types     JSONB NOT NULL DEFAULT '[]',
    channel         TEXT NOT NULL,
    channel_config  JSONB NOT NULL DEFAULT '{}',
    is_enabled      BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted      BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at      TIMESTAMPTZ,
    CONSTRAINT ck_notification_rules_entity_type CHECK (entity_type IN ('job','monitor','chain')),
    CONSTRAINT ck_notification_rules_channel CHECK (channel IN ('email','webhook'))
);
CREATE UNIQUE INDEX ix_notification_rules_name ON notification_rules (LOWER(name)) WHERE NOT is_deleted;
CREATE INDEX ix_notification_rules_entity ON notification_rules (entity_type, entity_id) WHERE NOT is_deleted AND is_enabled;

CREATE TABLE notification_logs (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    notification_rule_id  UUID NOT NULL REFERENCES notification_rules(id) ON DELETE CASCADE,
    event_type            TEXT NOT NULL,
    entity_type           TEXT NOT NULL,
    entity_id             UUID NOT NULL,
    channel               TEXT NOT NULL,
    recipient             TEXT NOT NULL,
    payload               JSONB NOT NULL DEFAULT '{}',
    success               BOOLEAN NOT NULL,
    error_message         TEXT,
    sent_at               TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE INDEX ix_notification_logs_rule ON notification_logs (notification_rule_id);
CREATE INDEX ix_notification_logs_entity ON notification_logs (entity_type, entity_id);
CREATE INDEX ix_notification_logs_sent ON notification_logs (sent_at DESC);
