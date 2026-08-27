# PostgreSQL

## Local database

A local PostgreSQL instance is declared in docker-compose.yml.

~~~bash
docker compose up -d postgres
~~~

Connection defaults for development:

| Setting | Value |
|---|---|
| Host | localhost |
| Port | 5432 |
| Database | raqmi_system |
| User | raqmi |

The password is a local development value only and must be replaced in production.

## Schemas

| Schema | Purpose |
|---|---|
| security | Users, roles and permissions |
| audit | Audit trail |
| organization | Hotel units |
| exploitation | Daily revenue and operational data |

## SQL scripts

| Script | Purpose |
|---|---|
| database/postgres/001_security_schema.sql | Creates schemas, security tables and indexes |
| database/postgres/002_security_seed.sql | Inserts permissions, roles and role-permission mappings |

## Future EF migrations

When .NET SDK is available, generate the first migration with:

~~~bash
dotnet ef migrations add InitialSecurity \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj \
  --output-dir Persistence/Migrations
~~~

Then apply it:

~~~bash
dotnet ef database update \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~
