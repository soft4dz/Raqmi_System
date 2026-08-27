# Agent workflow

## Roles

| Actor | Main responsibility |
|---|---|
| Human team | Business truth, legal validation, accounting validation, real hotel tests |
| ChatGPT Codex | Architecture, security review, integration, Git organization, final code review |
| Claude | Backend, business logic, ASP.NET Core, PostgreSQL, data migration |
| Gemini | WPF UI, UX, dashboards, documentation, functional test scenarios |

## Git rules

- Work on short branches.
- Do not push directly to main except for foundation commits approved by the product owner.
- One branch equals one bounded task.
- Avoid two agents editing the same file at the same time.
- Every branch must build before review.
- Codex performs final integration and security review.

## Suggested branch names

| Work | Branch |
|---|---|
| Architecture and security | codex/architecture-security |
| Backend auth | claude/backend-auth |
| Daily revenue API | claude/daily-revenue-api |
| Dashboard UI | gemini/dashboard-ui |
| Desktop shell | gemini/desktop-shell |
