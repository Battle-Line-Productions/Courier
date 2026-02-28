CREATE TABLE job_schedules (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id           UUID NOT NULL,
    schedule_type    TEXT NOT NULL,              -- 'cron' | 'one_shot'
    cron_expression  TEXT,
    run_at           TIMESTAMPTZ,
    is_enabled       BOOLEAN NOT NULL DEFAULT TRUE,
    last_fired_at    TIMESTAMPTZ,
    next_fire_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_schedule_type CHECK (schedule_type IN ('cron', 'one_shot')),
    CONSTRAINT ck_schedule_cron CHECK (
        (schedule_type = 'cron' AND cron_expression IS NOT NULL) OR
        (schedule_type = 'one_shot' AND run_at IS NOT NULL)
    ),
    CONSTRAINT fk_schedules_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE
);

CREATE INDEX ix_job_schedules_job_id ON job_schedules (job_id);
CREATE INDEX ix_job_schedules_enabled ON job_schedules (is_enabled, schedule_type);
