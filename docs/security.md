# Security baseline

## Implemented foundation

The repository now contains the first security foundation:

- Users
- Roles
- Permissions
- Role-permission mapping
- User-role mapping
- Audit log
- JWT authentication
- PostgreSQL schema preparation
- Initial security seeding command

## Rules from day one

- No default administrator password is committed to the repository.
- No API key, database password or license secret should be committed.
- Production configuration must be injected by environment variables or a secure secret store.
- Passwords are hashed with PBKDF2-SHA256 and a per-password salt.
- JWT signing requires a key of at least 32 bytes.
- Every authentication attempt is written to the audit log.
- Permissions are checked server-side through JWT claims and authorization policies.

## Environment variables

Use double underscores for nested .NET configuration keys:

~~~bash
RAQMI_POSTGRES__HOST=localhost
RAQMI_POSTGRES__PORT=5432
RAQMI_POSTGRES__DATABASE=raqmi_system
RAQMI_POSTGRES__USER=raqmi
RAQMI_POSTGRES__PASSWORD=change-me

RAQMI_JWT__ISSUER=RaqmiSystem
RAQMI_JWT__AUDIENCE=RaqmiSystem.Client
RAQMI_JWT__SIGNINGKEY=replace-with-a-random-secret-of-at-least-32-bytes
RAQMI_JWT__ACCESSTOKENMINUTES=60
~~~

## Database preparation

Start PostgreSQL locally:

~~~bash
docker compose up -d postgres
~~~

Apply the security SQL scripts if you want to initialize the database manually:

~~~bash
psql -h localhost -U raqmi -d raqmi_system -f database/postgres/001_security_schema.sql
psql -h localhost -U raqmi -d raqmi_system -f database/postgres/002_security_seed.sql
~~~

## Initial administrator

The first administrator is optional and must be provided through environment variables:

~~~bash
RAQMI_INITIAL_ADMIN_EMAIL=admin@example.local
RAQMI_INITIAL_ADMIN_PASSWORD=replace-with-a-strong-temporary-password
~~~

Then run the seed command:

~~~bash
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj -- --seed-security
~~~

The created administrator is marked with MustChangePassword = true.

## API endpoints

| Endpoint | Protection |
|---|---|
| GET /health | Public |
| GET /health/database | Public health check |
| POST /api/v1/auth/login | Public login |
| GET /api/v1/me | Authenticated |
| GET /api/v1/security/permissions | users.read |
| GET /api/v1/security/users | users.read |
| POST /api/v1/security/users/{id}/reset-password | users.write |
| POST /api/v1/auth/refresh | Public (valid refresh token required) |
| GET /api/v1/audit | audit.read |
| POST /api/v1/audit/purge | security.seed |
| GET /api/v1/revenue/sample-summary | revenue.read |

## Next security tasks

- Add full permission matrix by module.
