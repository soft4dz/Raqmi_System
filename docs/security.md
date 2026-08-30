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

## Anti-lockout guards on user administration

An administration screen must never be able to put the installation into a state nobody can get it
out of. Three rules are enforced by `UserAdministrationService` - in the service, not in the user
interface, so no HTTP client can go around them. Each one is refused with `400 Bad Request` and an
explicit message:

1. A user cannot deactivate their own account.
2. A user cannot remove from their own roles the one that carries `users.write`.
3. The last ACTIVE holder of `users.write` can be neither deactivated nor stripped of it, by
   anyone. In other words, the installation always keeps at least one active account able to
   administer users.

Rule 3 is not theoretical: an access token is a permission snapshot taken at sign-in and is not
revoked when the account behind it is deactivated, so a just-deactivated administrator keeps a
usable token until it expires - and is exactly the caller able to close the door behind them.

Administrators never choose another person's password. Creating an account and resetting a password
both generate a CSPRNG temporary password, persist only its hash, flag the account
`MustChangePassword`, and return the secret exactly once in the HTTP response (there is no
email/SMTP infrastructure in this repository yet). It is never written to the audit log.

## API endpoints

| Endpoint | Protection |
|---|---|
| GET /health | Public |
| GET /health/database | Public health check |
| POST /api/v1/auth/login | Public login |
| GET /api/v1/me | Authenticated |
| GET /api/v1/security/permissions | users.read |
| GET /api/v1/security/roles | users.read |
| GET /api/v1/security/users | users.read |
| GET /api/v1/security/users/{id} | users.read |
| POST /api/v1/security/users | users.write |
| PUT /api/v1/security/users/{id} | users.write |
| POST /api/v1/security/users/{id}/activate | users.write |
| POST /api/v1/security/users/{id}/deactivate | users.write |
| PUT /api/v1/security/users/{id}/roles | users.write |
| POST /api/v1/security/users/{id}/unlock | users.write |
| POST /api/v1/security/users/{id}/reset-password | users.write |
| POST /api/v1/auth/refresh | Public (valid refresh token required) |
| GET /api/v1/audit | audit.read |
| POST /api/v1/audit/purge | security.seed |
| GET /api/v1/revenue/sample-summary | revenue.read |

## Next security tasks

- Add full permission matrix by module.
