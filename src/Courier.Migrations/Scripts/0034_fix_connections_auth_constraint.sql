-- 0034: Fix stale auth_method check constraint on connections table
-- Migration 0004 created constraint as 'ck_connections_auth'.
-- Migration 0007 tried to drop 'ck_connections_auth_method' (wrong name),
-- so the original constraint was never removed. It still rejects
-- 'service_principal', blocking Azure Function connections.

DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth;
    END IF;
END $$;
