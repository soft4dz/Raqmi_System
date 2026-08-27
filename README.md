# Raqmi System

Raqmi System is the new generation ERP foundation for hotel operations, control, revenue reporting, finance, treasury, stock, HR, PMS and management dashboards.

This repository is now structured as a clean C#/.NET solution for the new version.

## Technical direction

- Backend: ASP.NET Core on .NET 10 LTS
- Database: PostgreSQL for the central server
- Desktop client: WPF on Windows
- Architecture: Domain-driven layers with clean separation
- Security: users, roles, permissions, audit log and JWT authentication
- Tests: unit tests first, then integration and end-to-end tests

## Repository structure

~~~text
src/
  RaqmiSystem.Domain/          Business entities and rules
  RaqmiSystem.Application/     Use cases and application contracts
  RaqmiSystem.Infrastructure/  PostgreSQL, security services and technical adapters
  RaqmiSystem.Api/             ASP.NET Core server API
  RaqmiSystem.Desktop/         WPF desktop client

database/
  postgres/                    PostgreSQL schema and seed scripts

tests/
  RaqmiSystem.Tests/           Unit tests

docs/
  architecture.md              Target architecture
  roadmap.md                   Transformation plan
  modules-prioritaires.md      First modules to implement
  security.md                  Security baseline
  postgresql.md                PostgreSQL setup
  agent-workflow.md            Work split between Codex, Claude, Gemini and humans
~~~

## First objective

The first operational target is not to rebuild all modules at once. The first target is a reliable server-client pilot covering:

1. Users and roles
2. Hotel units
3. Daily revenue entry
4. Direction dashboard
5. Audit trail

## Local start

Prerequisites:

- .NET 10 SDK
- PostgreSQL 16 or newer
- Visual Studio 2026 or newer for WPF development on Windows

Common commands:

~~~bash
dotnet restore RaqmiSystem.sln
dotnet build RaqmiSystem.sln
dotnet test RaqmiSystem.sln
docker compose up -d postgres
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~

Security seed:

~~~bash
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj -- --seed-security
~~~

## Status

This repository is currently a new technical foundation. The first implemented foundation covers security, authentication contracts, audit trail and PostgreSQL preparation. Business modules will be added progressively after the security, database and API foundations are validated.
