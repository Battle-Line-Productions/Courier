-- Create key_share_links table for secure public key sharing
CREATE TABLE key_share_links (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    key_id UUID NOT NULL,
    key_type TEXT NOT NULL,
    token_hash TEXT NOT NULL,
    token_salt TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_by TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    revoked_at TIMESTAMPTZ
);

CREATE INDEX idx_key_share_links_key ON key_share_links (key_id, key_type);
CREATE INDEX idx_key_share_links_token ON key_share_links (token_hash);
