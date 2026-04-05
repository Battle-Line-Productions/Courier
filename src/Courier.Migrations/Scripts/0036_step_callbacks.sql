-- 0036: Step callbacks for Azure Function completion detection
-- Adds step_callbacks table and function_key auth method.

CREATE TABLE step_callbacks (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    callback_key        TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'pending',
    result_payload      JSONB,
    error_message       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at        TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX ix_step_callbacks_key ON step_callbacks(callback_key);

ALTER TABLE step_callbacks ADD CONSTRAINT ck_step_callbacks_status
    CHECK (status IN ('pending', 'completed', 'failed'));

-- Add function_key to auth method constraint.
-- Handle both possible constraint names (0007 bug: name may be ck_connections_auth or ck_connections_auth_method).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth_method'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth_method;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth;
    END IF;
END $$;

ALTER TABLE connections ADD CONSTRAINT ck_connections_auth_method
    CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key', 'service_principal', 'function_key'));
