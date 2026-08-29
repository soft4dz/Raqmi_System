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
| database/postgres/001_security_schema.sql | Creates schemas, security tables, hotel units and daily revenue tables |
| database/postgres/002_security_seed.sql | Inserts permissions, roles and role-permission mappings |
| database/postgres/003_organization_revenue_module.sql | Adds or updates hotel units and daily revenue structures, then seeds the first EGT units |

## Current business tables

| Table | Purpose |
|---|---|
| organization.hotel_units | Operational hotel units with code, name, type, display order and active state |
| exploitation.daily_revenues | One daily revenue entry per date and unit, with categories, workflow status and validation trace |

## Schema migrations (source of truth)

Starting with the `InitialSchema` migration, the database schema is owned by EF Core
migrations under `src/RaqmiSystem.Infrastructure/Persistence/Migrations/`, generated from
the current EF model (`RaqmiDbContext` and `Persistence/Configurations/`). The
`database/postgres/001` to `004` SQL scripts remain in the repository as historical record
of how the schema was originally built by hand, but they must **not** be run against a new
environment anymore — applying them alongside the EF migrations would fight over the same
tables. From now on, `dotnet ef database update` is the only supported way to provision or
upgrade the schema.

The repository uses a local, versioned `dotnet-ef` tool (see `.config/dotnet-tools.json`)
so the CLI version always matches `Microsoft.EntityFrameworkCore.Design` from
`Directory.Packages.props`. Restore it once per clone/CI run, then use `dotnet ef` as usual:

~~~bash
dotnet tool restore

dotnet ef database update \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~

To add a new migration after changing the EF model:

~~~bash
dotnet ef migrations add <MigrationName> \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj \
  --output-dir Persistence/Migrations
~~~
