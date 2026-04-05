-- SSO External Identity Providers
-- Enhances auth_providers table and creates sso_user_links

-- Add new columns to auth_providers
ALTER TABLE auth_providers
  ADD COLUMN IF NOT EXISTS slug TEXT,
  ADD COLUMN IF NOT EXISTS allow_local_password BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS role_mapping JSONB DEFAULT '{}',
  ADD COLUMN IF NOT EXISTS display_order INT DEFAULT 0,
  ADD COLUMN IF NOT EXISTS icon_url TEXT,
  ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

-- Generate slugs for any existing providers
UPDATE auth_providers
SET slug = TRIM(BOTH '-' FROM REGEXP_REPLACE(
    LOWER(REGEXP_REPLACE(name, '[^a-zA-Z0-9]+', '-', 'g')),
    '-{2,}', '-', 'g'))
WHERE slug IS NULL;

-- Handle edge case: empty slug after sanitization
UPDATE auth_providers SET slug = 'provider-' || LEFT(id::text, 8) WHERE slug = '' OR slug IS NULL;

ALTER TABLE auth_providers ALTER COLUMN slug SET NOT NULL;
ALTER TABLE auth_providers ADD CONSTRAINT uq_auth_providers_slug UNIQUE (slug);

-- SSO user links table
CREATE TABLE sso_user_links (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  provider_id     UUID NOT NULL REFERENCES auth_providers(id) ON DELETE RESTRICT,
  subject_id      TEXT NOT NULL,
  email           TEXT,
  linked_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_login_at   TIMESTAMPTZ,
  CONSTRAINT uq_sso_user_links_provider_subject UNIQUE (provider_id, subject_id)
);

CREATE INDEX ix_sso_user_links_user_id ON sso_user_links (user_id);
CREATE INDEX ix_sso_user_links_provider_id ON sso_user_links (provider_id);

-- Update audit log CHECK constraint to include auth_provider
ALTER TABLE audit_log_entries DROP CONSTRAINT IF EXISTS ck_audit_entity_type CASCADE;

ALTER TABLE audit_log_entries ADD CONSTRAINT ck_audit_entity_type
  CHECK (entity_type IN (
    'job', 'job_execution', 'step_execution', 'connection',
    'pgp_key', 'ssh_key', 'file_monitor', 'tag', 'chain',
    'chain_execution', 'notification_rule', 'user', 'known_host',
    'auth_provider'
  ));
