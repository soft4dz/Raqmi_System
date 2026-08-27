CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS security;
CREATE SCHEMA IF NOT EXISTS audit;
CREATE SCHEMA IF NOT EXISTS organization;
CREATE SCHEMA IF NOT EXISTS exploitation;

CREATE TABLE IF NOT EXISTS security.users (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name varchar(80) NOT NULL,
    normalized_user_name varchar(80) NOT NULL,
    email varchar(160) NOT NULL,
    normalized_email varchar(160) NOT NULL,
    display_name varchar(160) NOT NULL,
    password_hash varchar(500) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    must_change_password boolean NOT NULL DEFAULT true,
    last_login_at timestamptz NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_security_users_normalized_user_name
    ON security.users (normalized_user_name);

CREATE UNIQUE INDEX IF NOT EXISTS ux_security_users_normalized_email
    ON security.users (normalized_email);

CREATE TABLE IF NOT EXISTS security.roles (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name varchar(80) NOT NULL,
    display_name varchar(120) NOT NULL,
    description varchar(500) NOT NULL,
    is_system boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_security_roles_name
    ON security.roles (name);

CREATE TABLE IF NOT EXISTS security.permissions (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    key varchar(120) NOT NULL,
    name varchar(160) NOT NULL,
    category varchar(80) NOT NULL,
    description varchar(500) NOT NULL,
    is_system boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_security_permissions_key
    ON security.permissions (key);

CREATE TABLE IF NOT EXISTS security.user_roles (
    user_id uuid NOT NULL REFERENCES security.users(id) ON DELETE CASCADE,
    role_id uuid NOT NULL REFERENCES security.roles(id) ON DELETE CASCADE,
    assigned_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, role_id)
);

CREATE INDEX IF NOT EXISTS ix_security_user_roles_role_id
    ON security.user_roles (role_id);

CREATE TABLE IF NOT EXISTS security.role_permissions (
    role_id uuid NOT NULL REFERENCES security.roles(id) ON DELETE CASCADE,
    permission_id uuid NOT NULL REFERENCES security.permissions(id) ON DELETE CASCADE,
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (role_id, permission_id)
);

CREATE INDEX IF NOT EXISTS ix_security_role_permissions_permission_id
    ON security.role_permissions (permission_id);

CREATE TABLE IF NOT EXISTS audit.audit_logs (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id uuid NULL,
    user_name varchar(160) NULL,
    action varchar(160) NOT NULL,
    entity_name varchar(160) NOT NULL,
    entity_id varchar(120) NULL,
    ip_address varchar(80) NULL,
    details_json jsonb NULL,
    occurred_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_occurred_at
    ON audit.audit_logs (occurred_at);

CREATE INDEX IF NOT EXISTS ix_audit_logs_user_id
    ON audit.audit_logs (user_id);

CREATE INDEX IF NOT EXISTS ix_audit_logs_action
    ON audit.audit_logs (action);

CREATE TABLE IF NOT EXISTS organization.hotel_units (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    code varchar(40) NOT NULL,
    name varchar(160) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_organization_hotel_units_code
    ON organization.hotel_units (code);

CREATE TABLE IF NOT EXISTS exploitation.daily_revenues (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    business_date date NOT NULL,
    hotel_unit_code varchar(40) NOT NULL,
    accommodation numeric(18, 2) NOT NULL DEFAULT 0,
    food numeric(18, 2) NOT NULL DEFAULT 0,
    beverage numeric(18, 2) NOT NULL DEFAULT 0,
    other_revenue numeric(18, 2) NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by varchar(160) NOT NULL DEFAULT 'system',
    updated_at timestamptz NULL,
    updated_by varchar(160) NULL,
    CONSTRAINT fk_daily_revenues_hotel_unit_code
        FOREIGN KEY (hotel_unit_code)
        REFERENCES organization.hotel_units(code)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_exploitation_daily_revenues_date_unit
    ON exploitation.daily_revenues (business_date, hotel_unit_code);
