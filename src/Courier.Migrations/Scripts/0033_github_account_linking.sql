ALTER TABLE users
    ADD COLUMN github_id          BIGINT,
    ADD COLUMN github_username    TEXT,
    ADD COLUMN github_token       BYTEA,
    ADD COLUMN github_linked_at   TIMESTAMPTZ;

CREATE UNIQUE INDEX ix_users_github_id ON users (github_id) WHERE github_id IS NOT NULL;
