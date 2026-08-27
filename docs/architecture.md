# Architecture

## Target

Raqmi System is structured as a server-client ERP.

The server owns the central business data, security rules, audit trail and integrations. The desktop client focuses on daily use by hotel units, direction, finance, HR and control teams.

## Layers

| Layer | Project | Responsibility |
|---|---|---|
| Domain | RaqmiSystem.Domain | Business entities, invariants and core rules |
| Application | RaqmiSystem.Application | Use cases, DTOs, validation flow and orchestration |
| Infrastructure | RaqmiSystem.Infrastructure | PostgreSQL, file storage, external connectors and technical adapters |
| API | RaqmiSystem.Api | HTTP endpoints, authentication, authorization and server hosting |
| Desktop | RaqmiSystem.Desktop | Windows desktop UI for users |
| Tests | RaqmiSystem.Tests | Unit tests and future integration tests |

## Main principles

- One central PostgreSQL database per deployment.
- No hardcoded secrets in source code.
- Role-based access control from the first version.
- Every sensitive operation must be audited.
- Business rules must live outside the UI.
- The daily revenue module is the first pilot module.
- Offline mode will be designed only after the first server-client pilot is stable.

## First bounded context

The first bounded context is exploitation control:

- Hotel units
- Users and roles
- Daily revenue
- Dashboard direction
- Audit trail
