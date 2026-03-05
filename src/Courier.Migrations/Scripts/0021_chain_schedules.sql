CREATE TABLE chain_schedules (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id         UUID NOT NULL,
    schedule_type    TEXT NOT NULL,              -- 'cron' | 'one_shot'
    cron_expression  TEXT,
    run_at           TIMESTAMPTZ,
    is_enabled       BOOLEAN NOT NULL DEFAULT TRUE,
    last_fired_at    TIMESTAMPTZ,
    next_fire_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_chain_schedule_type CHECK (schedule_type IN ('cron', 'one_shot')),
    CONSTRAINT ck_chain_schedule_cron CHECK (
        (schedule_type = 'cron' AND cron_expression IS NOT NULL) OR
        (schedule_type = 'one_shot' AND run_at IS NOT NULL)
    ),
    CONSTRAINT fk_chain_schedules_chains FOREIGN KEY (chain_id)
        REFERENCES job_chains (id) ON DELETE CASCADE
);

CREATE INDEX ix_chain_schedules_chain_id ON chain_schedules (chain_id);
CREATE INDEX ix_chain_schedules_enabled ON chain_schedules (is_enabled, schedule_type);
