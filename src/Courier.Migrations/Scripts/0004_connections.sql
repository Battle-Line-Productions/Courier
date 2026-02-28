-- ============================================================
-- CONNECTIONS
-- ============================================================
CREATE TABLE connections (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    TEXT NOT NULL,
    "group"                 TEXT,
    protocol                TEXT NOT NULL,
    host                    TEXT NOT NULL,
    port                    INT NOT NULL,
    auth_method             TEXT NOT NULL,
    username                TEXT NOT NULL,
    password_encrypted      BYTEA,
    ssh_key_id              UUID,
    -- FK to ssh_keys deferred until SSH Key Store vertical
    host_key_policy         TEXT NOT NULL DEFAULT 'trust_on_first_use',
    stored_host_fingerprint TEXT,
    passive_mode            BOOLEAN NOT NULL DEFAULT TRUE,
    tls_version_floor       TEXT,
    tls_cert_policy         TEXT NOT NULL DEFAULT 'system_trust',
    tls_pinned_thumbprint   TEXT,
    ssh_algorithms          JSONB,
    connect_timeout_sec     INT NOT NULL DEFAULT 30,
    operation_timeout_sec   INT NOT NULL DEFAULT 300,
    keepalive_interval_sec  INT NOT NULL DEFAULT 60,
    transport_retries       INT NOT NULL DEFAULT 2,
    status                  TEXT NOT NULL DEFAULT 'active',
    fips_override           BOOLEAN NOT NULL DEFAULT FALSE,
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted              BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT ck_connections_protocol CHECK (protocol IN ('sftp', 'ftp', 'ftps')),
    CONSTRAINT ck_connections_auth CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key')),
    CONSTRAINT ck_connections_host_key CHECK (host_key_policy IN ('trust_on_first_use', 'always_trust', 'manual')),
    CONSTRAINT ck_connections_tls_cert CHECK (tls_cert_policy IN ('system_trust', 'pinned_thumbprint', 'insecure')),
    CONSTRAINT ck_connections_status CHECK (status IN ('active', 'disabled')),
    CONSTRAINT ck_connections_retries CHECK (transport_retries BETWEEN 0 AND 3)
);

CREATE INDEX ix_connections_name ON connections (name) WHERE NOT is_deleted;
CREATE INDEX ix_connections_group ON connections ("group") WHERE NOT is_deleted AND "group" IS NOT NULL;
CREATE INDEX ix_connections_protocol ON connections (protocol) WHERE NOT is_deleted;

-- ============================================================
-- KNOWN HOSTS
-- ============================================================
CREATE TABLE known_hosts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id       UUID NOT NULL,
    fingerprint         TEXT NOT NULL,
    key_type            TEXT NOT NULL,
    first_seen          TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen           TIMESTAMPTZ NOT NULL DEFAULT now(),
    approved_by         TEXT NOT NULL,

    CONSTRAINT fk_known_hosts_connections FOREIGN KEY (connection_id)
        REFERENCES connections (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_known_hosts_connection_fingerprint ON known_hosts (connection_id, fingerprint);
