-- Remove GitHub account linking columns from users table
ALTER TABLE users
    DROP COLUMN IF EXISTS github_id,
    DROP COLUMN IF EXISTS github_username,
    DROP COLUMN IF EXISTS github_token,
    DROP COLUMN IF EXISTS github_linked_at;
