-- Add successor_key_id to pgp_keys for key successor chaining
ALTER TABLE pgp_keys ADD COLUMN IF NOT EXISTS successor_key_id UUID REFERENCES pgp_keys(id);
