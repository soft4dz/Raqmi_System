# Deployment

## Production configuration file (`appsettings.Production.json`)

`src/RaqmiSystem.Api/appsettings.Production.json` is loaded automatically by
ASP.NET Core whenever `ASPNETCORE_ENVIRONMENT=Production` (which
`docker-compose.prod.yml` sets for the `api` service). It has the same shape as
`appsettings.json` / `appsettings.Development.json`, but **it must never contain
a secret** - see `src/RaqmiSystem.Api/README.md` for the full rule. In practice
that means `Postgres:Password` and `Jwt:SigningKey` are absent from that file;
they are supplied exclusively through the `RAQMI_POSTGRES__PASSWORD` and
`RAQMI_JWT__SIGNINGKEY` environment variables (already read by
`builder.Configuration.AddEnvironmentVariables(prefix: "RAQMI_")` in
`Program.cs`, and already wired through `docker-compose.prod.yml`'s `.env`
file). `JwtOptions.Validate()` fails startup with a clear error if the signing
key is missing in a non-Development environment, rather than silently starting
insecurely.

## Structured logging

The API logs through Serilog (`Serilog.AspNetCore`), configured directly in
`Program.cs` via `builder.Host.UseSerilog(...)` - not through an `appsettings`
`Serilog` section. Output is one compact JSON object per line
(`Serilog.Formatting.Compact.CompactJsonFormatter`) written to the console,
because `docker-compose.prod.yml` runs the API as a container and Docker
captures stdout/stderr; a JSON-lines format is what an external log collector
reading that stream expects. Minimum level is `Information`, with the noisy
`Microsoft.*` categories (`Microsoft.AspNetCore`, EF Core's internal command
logging, etc.) reduced to `Warning` so routine framework chatter does not
drown out application log events.

## Dedicated PostgreSQL application role

`deploy/postgres/create-app-role.sql` creates a dedicated, least-privilege
`raqmi_app` role for the API to connect as in production - `SELECT`/`INSERT`/
`UPDATE`/`DELETE` on the `security`, `audit`, `organization` and `exploitation`
schemas, and nothing else (no `CREATE`, no superuser, no ownership). Schema
changes (EF Core migrations) must keep running under a separate, more
privileged account.

`docker-compose.prod.yml` provisions exactly this split: the `postgres`
service bootstraps under `RAQMI_POSTGRES_ADMIN_USER`/`RAQMI_POSTGRES_ADMIN_PASSWORD`
(from `.env`), while the `api` service only ever authenticates as
`RAQMI_POSTGRES__USER`/`RAQMI_POSTGRES__PASSWORD` - which must be set to the
`raqmi_app` role created below, never to the admin account. The API's own
configuration code never reads the admin variables at all.

Run it once per environment, against a fresh database, in this order:

1. **Create the schema first.** `create-app-role.sql` grants access on the
   `security`/`audit`/`organization`/`exploitation` schemas - they must
   already exist, or the `GRANT USAGE ON SCHEMA ...` statements fail with
   `schema "security" does not exist`. Apply the EF Core migrations as the
   admin/bootstrap Postgres user first:

   ~~~bash
   dotnet ef database update \
     --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
     --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
   ~~~

   The runtime container built from the repo's `Dockerfile` (based on
   `aspnet:10.0`) does not include the .NET SDK or the `dotnet-ef` tool - run
   this from a machine that has the SDK (a developer/ops workstation, or a
   one-off container built `FROM mcr.microsoft.com/dotnet/sdk:10.0`), with
   `RAQMI_POSTGRES__*` pointed at the production database and set to the
   admin account (`RAQMI_POSTGRES_ADMIN_USER`/`_PASSWORD` from `.env`, not the
   `raqmi_app` credentials), *before* the API container itself is deployed.

2. Run `create-app-role.sql` as that same admin role, passing the real
   password on the command line rather than editing the file - there is no
   placeholder or default baked into the script, it aborts if you don't:

   ~~~bash
   psql -h <host> -U <admin-user> -d raqmi_system \
        -v app_password='<a-long-random-value>' \
        -f deploy/postgres/create-app-role.sql
   ~~~

3. Set `RAQMI_POSTGRES__USER=raqmi_app` / `RAQMI_POSTGRES__PASSWORD=<the same
   real password from step 2>` wherever the API's own environment is
   configured (`.env` for the compose stack, or the host's secret store) -
   this is already the default `Postgres:User` in `appsettings.Production.json`.
   The admin account used in steps 1-2 keeps its elevated privileges but is no
   longer what the running API authenticates as.

The script is idempotent - re-running it is safe and will not reset the
password of an already-existing `raqmi_app` role.

## Database backups

`deploy/backup/pg-backup.sh` dumps the production database (via
`docker compose exec postgres pg_dump`, gzip-compressed) into date-stamped
files and applies a simple retention policy: 7 daily backups, 4 weekly
backups (kept every Sunday), 6 monthly backups (kept on the 1st of the
month). It exits non-zero and leaves no partial file behind if `pg_dump`
fails, so a failed backup is never silently mistaken for a good one.

Run it manually:

~~~bash
deploy/backup/pg-backup.sh
~~~

By default it writes under `/var/backups/raqmi/{daily,weekly,monthly}/` and
reads `RAQMI_POSTGRES__USER`/`RAQMI_POSTGRES__DATABASE` from the `.env` file
next to `docker-compose.prod.yml` (or from the current shell's environment).
See the comment header in the script for every configuration variable it
accepts (`RAQMI_BACKUP_DIR`, `RAQMI_COMPOSE_FILE`, `RAQMI_BACKUP_PG_USER`,
...).

### Automatic daily execution (systemd timer)

`deploy/backup/raqmi-backup.service` and `deploy/backup/raqmi-backup.timer`
are example systemd units for running the script once a day on the
production VPS:

~~~bash
# Adjust WorkingDirectory/User in raqmi-backup.service first, then:
sudo cp deploy/backup/raqmi-backup.service deploy/backup/raqmi-backup.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now raqmi-backup.timer

# Check it is scheduled, and inspect the last run:
systemctl list-timers raqmi-backup.timer
journalctl -u raqmi-backup.service
~~~

### Testing a restore

A backup that has never been restored is not verified. Restore into a
throwaway database rather than the real one. `CREATE DATABASE`/`DROP DATABASE`
need the admin account, not the restricted `raqmi_app` role:

~~~bash
# Create a scratch database.
docker compose -f docker-compose.prod.yml exec -T postgres \
  psql -U "$RAQMI_POSTGRES_ADMIN_USER" -d postgres -c "CREATE DATABASE raqmi_restore_test;"

# Restore the chosen backup into it.
gunzip -c /var/backups/raqmi/daily/<file>.sql.gz | docker compose -f docker-compose.prod.yml exec -T postgres \
  psql -U "$RAQMI_POSTGRES_ADMIN_USER" -d raqmi_restore_test

# Spot-check that data actually came back.
docker compose -f docker-compose.prod.yml exec -T postgres \
  psql -U "$RAQMI_POSTGRES_ADMIN_USER" -d raqmi_restore_test -c "SELECT count(*) FROM security.users;"

# Clean up.
docker compose -f docker-compose.prod.yml exec -T postgres \
  psql -U "$RAQMI_POSTGRES_ADMIN_USER" -d postgres -c "DROP DATABASE raqmi_restore_test;"
~~~

## Dockerfile

The root `Dockerfile` builds and runs the API only (`src/RaqmiSystem.Api`). It is a
multi-stage build: the SDK image restores and publishes the project, then the
published output is copied into a smaller ASP.NET runtime image that runs as a
non-root user. The build context is the repository root because the API project
references the Domain/Application/Infrastructure projects and the root-level
`Directory.Build.props` / `Directory.Packages.props` files.

The image listens on port 8080 (`ASPNETCORE_URLS=http://+:8080`) and declares a
Docker `HEALTHCHECK` against `GET /health`.

## Running the production-style stack locally

`docker-compose.prod.yml` runs Postgres, the API, and a Caddy reverse proxy
together. It is separate from the root `docker-compose.yml`, which only runs
Postgres for local development.

1. Copy `.env.example` to `.env` and fill in real values. `.env` is listed in
   `.gitignore` and must never be committed.
2. Start the stack:

   ~~~bash
   docker compose -f docker-compose.prod.yml up -d --build
   ~~~

3. The API is reachable through Caddy on ports 80/443, not exposed directly.
   Postgres has no host port mapping in this file.

Before real production use, edit `deploy/Caddyfile` and replace the
`raqmi.example.com` placeholder with the actual domain.

## CI image publishing

The `dotnet` workflow has a `publish-api` job that runs only when a tag matching
`v*.*.*` is pushed. It builds the Dockerfile above and pushes the image to
`ghcr.io/<repository>` tagged with the version and `latest`.

This job builds and publishes the image only. It does not deploy anything —
rolling a new image out to a server remains a manual step, done separately from
CI.

## Client desktop

The WPF desktop client (`src/RaqmiSystem.Desktop`) is distributed as a
self-contained Windows build plus an Inno Setup installer. There is no CI job
for this yet; both steps below are run manually.

### Publishing a self-contained build

~~~bash
dotnet publish src/RaqmiSystem.Desktop/RaqmiSystem.Desktop.csproj -c Release -r win-x64 \
  --self-contained true -p:PublishSingleFile=false -o publish/desktop
~~~

This produces `publish/desktop/RaqmiSystem.Desktop.exe` bundled with its own
.NET runtime. It is deliberately self-contained (`--self-contained true`)
rather than framework-dependent: hotel workstations are not guaranteed to have
a matching .NET runtime installed, so the published output must not depend on
one being present. `publish/` is git-ignored; run this command locally (or
from CI later) before building the installer.

### Building the installer

The installer script lives at `deploy/installer/RaqmiSystemDesktop.iss` and
packages the contents of `publish/desktop/` produced above. It requires
[Inno Setup 6](https://jrsoftware.org/isinfo.php) with its command-line
compiler (`iscc.exe`) available.

~~~bash
iscc deploy/installer/RaqmiSystemDesktop.iss
~~~

The compiled installer is written to `deploy/installer/output/RaqmiSystem-Setup.exe`
(that output directory is git-ignored — installer binaries are not committed).
The installer copies the published files, creates Start Menu and optional
Desktop shortcuts, and offers to launch the application after install. It does
not configure or prompt for the API URL — see below.

### Configuring the API URL (post-install)

The desktop client does not bake in an API URL at install time. At startup it
resolves the API base URL in this order (see
`src/RaqmiSystem.Desktop/DesktopSettings.cs`):

1. The `RAQMI_DESKTOP_API_URL` environment variable, if set.
2. The per-user settings file `%APPDATA%\RaqmiSystem\desktop-settings.json`
   (a JSON object with an `ApiBaseUrl` property), if present.
3. A hard-coded fallback default (`http://localhost:5180`).

For this first pilot, every workstation gets the same installer package, and
pointing a given machine at the correct server is a separate, per-machine step
done after installation — either by setting the environment variable or by
writing the settings file.
