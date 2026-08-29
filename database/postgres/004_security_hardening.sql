ALTER TABLE security.users
    ADD COLUMN IF NOT EXISTS failed_login_attempts integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS failed_login_window_started_at timestamptz NULL,
    ADD COLUMN IF NOT EXISTS locked_out_until timestamptz NULL;

CREATE TABLE IF NOT EXISTS security.refresh_tokens (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL REFERENCES security.users(id) ON DELETE CASCADE,
    token_hash varchar(200) NOT NULL,
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    revoked_at timestamptz NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_security_refresh_tokens_token_hash
    ON security.refresh_tokens (token_hash);

CREATE INDEX IF NOT EXISTS ix_security_refresh_tokens_user_id
    ON security.refresh_tokens (user_id);
