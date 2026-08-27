# Raqmi System

Raqmi System is the new generation ERP foundation for hotel operations, control, revenue reporting, finance, treasury, stock, HR, PMS and management dashboards.

This repository is now structured as a clean C#/.NET solution for the new version.

## Technical direction

- Backend: ASP.NET Core on .NET 10 LTS
- Database: PostgreSQL for the central server
- Desktop client: WPF on Windows
- Architecture: Domain-driven layers with clean separation
- Tests: unit tests first, then integration and end-to-end tests

## Repository structure

~~~text
src/
  RaqmiSystem.Domain/          Business entities and rules
  RaqmiSystem.Application/     Use cases and application contracts
  RaqmiSystem.Infrastructure/  Persistence, external services, PostgreSQL adapters
  RaqmiSystem.Api/             ASP.NET Core server API
  RaqmiSystem.Desktop/         WPF desktop client

tests/
  RaqmiSystem.Tests/           Unit tests

docs/
  architecture.md              Target architecture
  roadmap.md                   Transformation plan
  modules-prioritaires.md      First modules to implement
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
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~

## Status

This repository is currently a new technical foundation. Business modules will be added progressively after the security, database and API foundations are validated.
