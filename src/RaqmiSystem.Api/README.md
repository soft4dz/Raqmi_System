# Configuration files (appsettings)

ASP.NET Core loads these in order, each overriding the previous one:

1. `appsettings.json` - base defaults for every environment.
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json` - `Development` or `Production` overlay.
3. Environment variables with the `RAQMI_` prefix (double underscore for nesting,
   e.g. `RAQMI_POSTGRES__PASSWORD`, `RAQMI_JWT__SIGNINGKEY`) - see `Program.cs`.

## `appsettings.Production.json` must never contain a secret

This file is committed to the repository, so it is readable by anyone with access
to the source. It intentionally has **no** `Postgres:Password` and **no**
`Jwt:SigningKey` entries. Those two values must be supplied exclusively through the
`RAQMI_POSTGRES__PASSWORD` and `RAQMI_JWT__SIGNINGKEY` environment variables at
deploy time (see `docker-compose.prod.yml` and `.env.example`).

If you ever find yourself about to add a real password, connection string, or
signing key to this file - stop, and put it in the environment / secret store
instead. `JwtOptions.Validate()` deliberately throws a startup error when the
signing key is missing rather than silently falling back to something insecure.
