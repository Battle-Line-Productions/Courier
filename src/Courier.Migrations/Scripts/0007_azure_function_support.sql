-- 0007: Azure Function support
-- Adds properties (JSONB) and client_secret_encrypted (BYTEA) columns to connections table.
-- Updates check constraints for protocol and auth_method to include azure_function / service_principal.

ALTER TABLE connections ADD COLUMN properties JSONB;
ALTER TABLE connections ADD COLUMN client_secret_encrypted BYTEA;

-- Drop existing check constraints if they exist, then recreate with new values
DO $$
BEGIN
    -- Protocol constraint
    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_protocol'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_protocol;
    END IF;

    -- Auth method constraint
    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth_method'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth_method;
    END IF;
END $$;

ALTER TABLE connections ADD CONSTRAINT ck_connections_protocol
    CHECK (protocol IN ('sftp', 'ftp', 'ftps', 'azure_function'));

ALTER TABLE connections ADD CONSTRAINT ck_connections_auth_method
    CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key', 'service_principal'));
