-- deploy/postgres/create-app-role.sql
--
-- Creates a dedicated, least-privilege PostgreSQL role for the running API to
-- connect as (the value meant for RAQMI_POSTGRES__USER / RAQMI_POSTGRES__PASSWORD
-- in production). It is NOT a superuser and cannot create, alter or drop schemas,
-- tables or the database itself.
--
-- The role used to run this script, and to run EF Core migrations
-- (`dotnet ef database update`, see docs/postgresql.md), stays a SEPARATE,
-- more privileged account - typically the same Postgres admin/bootstrap user
-- created by docker-compose.prod.yml's POSTGRES_USER/POSTGRES_PASSWORD. Never
-- point RAQMI_POSTGRES__USER at that admin account in production; point it at
-- the role created here instead, once its password has been set for real.
--
-- Idempotent: safe to run more than once. Re-running it will NOT reset the
-- password of an already-existing role (see the DO block below), so it will
-- not clobber a real password with the placeholder on a second run.
--
-- Run this AFTER the schema exists (i.e. after `dotnet ef database update` has
-- created the security/audit/organization/exploitation schemas and tables -
-- see docs/deployment.md), and before the API's first startup. While
-- connected to the target application database (not "postgres"):
--
--   psql -h <host> -U <admin-user> -d raqmi_system \
--        -v app_password='<a-long-random-value>' \
--        -f deploy/postgres/create-app-role.sql
--
-- =============================================================================
-- REQUIRED: pass the real password via -v app_password=... as shown above.
--
--   There is no default or placeholder password baked into this script on
--   purpose: if you run it without -v app_password=..., it aborts instead of
--   silently creating raqmi_app with a known, source-controlled password.
--   Store the real value only in the environment / secret store that
--   provides RAQMI_POSTGRES__PASSWORD - never in this file or in Git.
-- =============================================================================

-- Fail fast on any error, regardless of how the caller invoked psql.
\set ON_ERROR_STOP on

\if :{?app_password}
\else
    \warn 'create-app-role.sql: the app_password psql variable is not set. Re-run with -v app_password=''<a-long-random-value>''.'
    -- \quit alone would exit 0 and callers checking the exit code would think
    -- the role was provisioned; raise a real error so ON_ERROR_STOP makes
    -- psql exit non-zero instead.
    DO $abort$ BEGIN RAISE EXCEPTION 'app_password psql variable is not set'; END $abort$;
\endif

-- psql variables are NOT interpolated inside dollar-quoted DO $$ ... $$ bodies,
-- so the CREATE ROLE statement is built outside any dollar quoting and executed
-- via \gexec. format(%L) safely quotes the password; the WHERE clause keeps the
-- script idempotent (zero rows selected => \gexec executes nothing).
SELECT format('CREATE ROLE raqmi_app LOGIN PASSWORD %L', :'app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'raqmi_app')
\gexec

-- Schema-level access. USAGE only - no CREATE, so raqmi_app cannot add, alter
-- or drop tables/objects inside these schemas.
GRANT USAGE ON SCHEMA security TO raqmi_app;
GRANT USAGE ON SCHEMA audit TO raqmi_app;
GRANT USAGE ON SCHEMA organization TO raqmi_app;
GRANT USAGE ON SCHEMA exploitation TO raqmi_app;
GRANT USAGE ON SCHEMA finance TO raqmi_app;

-- Row-level CRUD on every table that exists today in each schema.
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA security TO raqmi_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA audit TO raqmi_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA organization TO raqmi_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA exploitation TO raqmi_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA finance TO raqmi_app;

-- EF Core's migrations history table lives at public."__EFMigrationsHistory"
-- (HasDefaultSchema("raqmi") does not move it). raqmi_app never writes it, but
-- pg_dump run as raqmi_app (deploy/onpremise/backup-raqmi.ps1) must be able to
-- read every table in the database or the whole dump aborts with "permission
-- denied" - and a backup missing this table could not be restored and then
-- migrated forward. Requires the migrations to have been applied first (the
-- documented install order guarantees that).
GRANT SELECT ON TABLE public."__EFMigrationsHistory" TO raqmi_app;

-- Sequences: the current schema uses uuid primary keys (gen_random_uuid()), not
-- serial/identity columns, so there are none today. Granted anyway so a future
-- EF Core migration that adds an identity/serial column does not silently break
-- inserts for this role.
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA security TO raqmi_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA audit TO raqmi_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA organization TO raqmi_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA exploitation TO raqmi_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA finance TO raqmi_app;

-- Default privileges: apply the same grants automatically to tables/sequences
-- created AFTER this point, so future `dotnet ef database update` runs do not
-- need a manual re-grant for raqmi_app to keep working.
--
-- IMPORTANT: "ALTER DEFAULT PRIVILEGES" only affects objects later created BY
-- THE ROLE RUNNING THIS BLOCK (the current role, since no "FOR ROLE ..." clause
-- is given below). Run this script as the same admin/migration role that will
-- actually execute future EF Core migrations. If migrations run under a
-- different role, either re-run this block (just the ALTER DEFAULT PRIVILEGES
-- statements) as that role, or add an explicit "FOR ROLE <that_role>" clause.
ALTER DEFAULT PRIVILEGES IN SCHEMA security
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA organization
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA exploitation
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA finance
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO raqmi_app;

ALTER DEFAULT PRIVILEGES IN SCHEMA security
    GRANT USAGE, SELECT ON SEQUENCES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA audit
    GRANT USAGE, SELECT ON SEQUENCES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA organization
    GRANT USAGE, SELECT ON SEQUENCES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA exploitation
    GRANT USAGE, SELECT ON SEQUENCES TO raqmi_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA finance
    GRANT USAGE, SELECT ON SEQUENCES TO raqmi_app;

-- Deliberately NOT granted, by design:
--   * CREATE on any schema or on the database - raqmi_app cannot create or
--     drop tables/indexes/schemas. Migrations must run as the admin role.
--   * SUPERUSER, CREATEDB, CREATEROLE, REPLICATION - none of them.
--   * ALL PRIVILEGES / ownership of any object.
-- If a future migration truly needs raqmi_app to have more than CRUD, revisit
-- this script deliberately rather than widening it ad hoc in production.
