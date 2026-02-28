-- ============================================================
-- PGP KEYS
-- ============================================================
CREATE TABLE pgp_keys (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    fingerprint         TEXT,
    short_key_id        TEXT,
    algorithm           TEXT NOT NULL,
    key_type            TEXT NOT NULL DEFAULT 'key_pair',
    purpose             TEXT,
    status              TEXT NOT NULL DEFAULT 'active',
    public_key_data     TEXT,
    private_key_data    BYTEA,
    passphrase_hash     BYTEA,
    expires_at          TIMESTAMPTZ,
    successor_key_id    UUID,
    created_by          TEXT,
    notes               TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ,

    CONSTRAINT ck_pgp_keys_algorithm CHECK (algorithm IN (
        'rsa_2048', 'rsa_3072', 'rsa_4096',
        'ecc_curve25519', 'ecc_p256', 'ecc_p384'
    )),
    CONSTRAINT ck_pgp_keys_key_type CHECK (key_type IN ('public_only', 'key_pair')),
    CONSTRAINT ck_pgp_keys_status CHECK (status IN ('active', 'expiring', 'retired', 'revoked', 'deleted')),
    CONSTRAINT fk_pgp_keys_successor FOREIGN KEY (successor_key_id)
        REFERENCES pgp_keys (id)
);

CREATE INDEX ix_pgp_keys_name ON pgp_keys (name) WHERE NOT is_deleted;
CREATE INDEX ix_pgp_keys_status ON pgp_keys (status) WHERE NOT is_deleted;
CREATE UNIQUE INDEX ix_pgp_keys_fingerprint ON pgp_keys (fingerprint) WHERE NOT is_deleted AND fingerprint IS NOT NULL;

-- ============================================================
-- SSH KEYS
-- ============================================================
CREATE TABLE ssh_keys (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    key_type            TEXT NOT NULL,
    public_key_data     TEXT,
    private_key_data    BYTEA,
    passphrase_hash     BYTEA,
    fingerprint         TEXT,
    status              TEXT NOT NULL DEFAULT 'active',
    notes               TEXT,
    created_by          TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ,

    CONSTRAINT ck_ssh_keys_key_type CHECK (key_type IN ('rsa_2048', 'rsa_4096', 'ed25519', 'ecdsa_256')),
    CONSTRAINT ck_ssh_keys_status CHECK (status IN ('active', 'retired', 'deleted'))
);

CREATE INDEX ix_ssh_keys_name ON ssh_keys (name) WHERE NOT is_deleted;
CREATE INDEX ix_ssh_keys_status ON ssh_keys (status) WHERE NOT is_deleted;
CREATE UNIQUE INDEX ix_ssh_keys_fingerprint ON ssh_keys (fingerprint) WHERE NOT is_deleted AND fingerprint IS NOT NULL;

-- ============================================================
-- WIRE DEFERRED FK: connections.ssh_key_id → ssh_keys.id
-- ============================================================
ALTER TABLE connections
    ADD CONSTRAINT fk_connections_ssh_key
    FOREIGN KEY (ssh_key_id) REFERENCES ssh_keys (id);
