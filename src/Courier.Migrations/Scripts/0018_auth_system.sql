-- USERS
CREATE TABLE users (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    username            TEXT NOT NULL,
    email               TEXT,
    display_name        TEXT NOT NULL,
    password_hash       TEXT,
    role                TEXT NOT NULL DEFAULT 'viewer',
    is_active           BOOLEAN NOT NULL DEFAULT TRUE,
    is_sso_user         BOOLEAN NOT NULL DEFAULT FALSE,
    sso_provider_id     UUID,
    sso_subject_id      TEXT,
    failed_login_count  INT NOT NULL DEFAULT 0,
    locked_until        TIMESTAMPTZ,
    last_login_at       TIMESTAMPTZ,
    password_changed_at TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ,
    CONSTRAINT ck_users_role CHECK (role IN ('admin', 'operator', 'viewer')),
    CONSTRAINT uq_users_username UNIQUE (username)
);

-- REFRESH TOKENS
CREATE TABLE refresh_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id         UUID NOT NULL REFERENCES users(id),
    token_hash      TEXT NOT NULL,
    expires_at      TIMESTAMPTZ NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by_ip   TEXT,
    revoked_at      TIMESTAMPTZ,
    replaced_by_id  UUID REFERENCES refresh_tokens(id)
);
CREATE INDEX ix_refresh_tokens_user_id ON refresh_tokens(user_id);
CREATE INDEX ix_refresh_tokens_token_hash ON refresh_tokens(token_hash);

-- AUTH PROVIDERS (skeleton for Phase 2/3 SSO)
CREATE TABLE auth_providers (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    type            TEXT NOT NULL,
    name            TEXT NOT NULL,
    is_enabled      BOOLEAN NOT NULL DEFAULT FALSE,
    configuration   JSONB NOT NULL DEFAULT '{}',
    auto_provision  BOOLEAN NOT NULL DEFAULT TRUE,
    default_role    TEXT NOT NULL DEFAULT 'viewer',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_auth_providers_type CHECK (type IN ('oidc', 'saml')),
    CONSTRAINT ck_auth_providers_default_role CHECK (default_role IN ('admin', 'operator', 'viewer')),
    CONSTRAINT uq_auth_providers_name UNIQUE (name)
);

ALTER TABLE users ADD CONSTRAINT fk_users_sso_provider
    FOREIGN KEY (sso_provider_id) REFERENCES auth_providers(id);

-- SEED AUTH SETTINGS
INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('auth.setup_completed', 'false', 'Whether initial admin setup has been completed', 'system'),
    ('auth.session_timeout_minutes', '15', 'JWT access token lifetime in minutes', 'system'),
    ('auth.refresh_token_days', '7', 'Refresh token lifetime in days', 'system'),
    ('auth.password_min_length', '8', 'Minimum password length', 'system'),
    ('auth.max_login_attempts', '5', 'Failed attempts before lockout', 'system'),
    ('auth.lockout_duration_minutes', '15', 'Account lockout duration', 'system');
