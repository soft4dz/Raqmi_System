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
