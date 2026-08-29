# On-premise pilot deployment (Windows server, no VPS)

This document covers the pilot deployment mode chosen for the first hotel
unit: a Windows PC installed on site acts as the server, running native
PostgreSQL and the API as a local process, with the WPF desktop clients
connecting over the local network. There is no VPS, no Docker and no reverse
proxy in this mode. The VPS/Docker path described in `docs/deployment.md`
remains the target for later generalization; nothing in this mode diverges
from it at the code level (see "Assumed limitations" below).

## Architecture

~~~text
+---------------------------------------------+
| Windows server PC (in the hotel unit)       |
|                                             |
|  PostgreSQL 18 (native Windows service)     |
|      ^ localhost:5432, role raqmi_app       |
|  RaqmiSystem.Api.exe (self-contained)       |
|      ^ scheduled task, SYSTEM, at boot      |
|      ^ listens on http://0.0.0.0:5180       |
|  Daily pg_dump backup (03:30, 7d/4w/6m)     |
+---------------------------------------------+
                 ^ LAN, HTTP :5180
                 | (firewall: Domain/Private profiles only)
    +------------+------------+------ ...
    |                         |
  Workstation 1             Workstation N
  RaqmiSystem.Desktop       RaqmiSystem.Desktop
  (installed via RaqmiSystem-Setup.exe)
~~~

Everything the runtime needs lives under one directory on the server
(default `C:\RaqmiSystem`):

| Path | Content |
|---|---|
| `C:\RaqmiSystem\api` | Self-contained published API (`RaqmiSystem.Api.exe`) |
| `C:\RaqmiSystem\config\raqmi.env.ps1` | Protected environment file (secrets) - ACL restricted to Administrators + SYSTEM |
| `C:\RaqmiSystem\scripts` | Copies of `start-api.ps1`, `backup-raqmi.ps1`, `check-health.ps1` used by the scheduled tasks |
| `C:\RaqmiSystem\logs` | API console logs (JSON lines, 30-day rotation) and `health-status.txt` |
| `C:\RaqmiSystem\backups` | `daily\` / `weekly\` / `monthly\` pg_dump files (`-DataDir` at install time) |

## Prerequisites (server PC)

* Windows 10/11 with Windows PowerShell 5.1 (built in).
* PostgreSQL installed natively (the scripts default to
  `C:\Program Files\PostgreSQL\18\bin`; pass `-PgBin` for another version),
  with its Windows service running and the password of the admin user
  (`postgres` by default) known.
* .NET SDK 10 - **for the installation only**. It runs the EF Core
  migrations and `dotnet publish`; the published API is self-contained and
  does not need any runtime installed afterwards.
* A clone of this repository (the installer resolves paths relative to
  itself).
* The server connected to the hotel LAN with a **static IP or a DHCP
  reservation** - the workstations are pointed at that address.
* The network connection classified as **Private** (or joined to a domain).
  The firewall rule created by the installer only allows the API port on the
  Domain/Private profiles; on a Public-profile network the clients will not
  reach the API, by design.

## Installation

Open PowerShell **as Administrator** in the repository root and run:

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File deploy\onpremise\install-server.ps1
~~~

Optional parameters: `-ApiPort 5180`, `-DataDir C:\RaqmiSystem\backups`,
`-PgBin "C:\Program Files\PostgreSQL\18\bin"`, `-PgAdminUser postgres`,
`-InstallDir C:\RaqmiSystem`.

The script prompts interactively for the PostgreSQL admin password and for
the password to set on the `raqmi_app` role (leave the latter empty to
generate a random one). Passwords are never accepted as command-line
arguments - they would persist in the console history. It also offers to
create the initial administrator account (seeded with
`MustChangePassword = true`, so the temporary password entered there must be
changed at first login).

It then performs, in order (mirroring `docs/deployment.md`):

1. Creates the `raqmi_system` database if absent.
2. Applies the EF Core migrations as the admin user (`dotnet tool restore` +
   `dotnet-ef database update`) - migrations run **before**
   `create-app-role.sql`, whose `GRANT`s need the schemas to exist.
3. Runs `deploy/postgres/create-app-role.sql` to create the least-privilege
   `raqmi_app` role the API connects as. Re-running the installer never
   resets an existing `raqmi_app` password (the SQL script is idempotent by
   design).
4. Publishes the API self-contained (`dotnet publish -r win-x64
   --self-contained true`) to `C:\RaqmiSystem\api`.
5. Writes `C:\RaqmiSystem\config\raqmi.env.ps1` with
   `ASPNETCORE_ENVIRONMENT=Production`, `ASPNETCORE_URLS=http://0.0.0.0:<port>`,
   the `RAQMI_POSTGRES__*` variables (user `raqmi_app`) and the
   `RAQMI_JWT__*` variables including a freshly generated random 64-byte
   signing key (`JwtOptions.Validate()` refuses to start outside Development
   without a key of at least 32 bytes). The file's ACL is restricted to
   Administrators + SYSTEM via `icacls`.
6. Seeds the security catalog (`RaqmiSystem.Api.exe --seed-security`) and
   the optional initial administrator.
7. Registers the **"Raqmi System API"** scheduled task: SYSTEM account,
   at-boot trigger, restart on failure, running
   `scripts\start-api.ps1` (which dot-sources the protected config, rotates
   logs older than 30 days, and redirects the API's JSON console output to
   `logs\api_<timestamp>.log`). A scheduled task is used instead of a
   Windows service on purpose: the API is a plain console executable, not a
   service - making it one would require a wrapper (NSSM or similar) or code
   changes, while the Task Scheduler provides boot start, a SYSTEM identity
   and restart-on-failure with no extra dependency.
8. Opens Windows Firewall inbound TCP on the API port, rule
   **"Raqmi System API"**, profiles **Domain and Private only** - never
   Public.
9. Registers the **"Raqmi System Backup"** daily scheduled task at 03:30
   running `scripts\backup-raqmi.ps1`.
10. Starts the API task, polls `http://localhost:<port>/health`, and prints
    a recap with the URL to configure on the workstations.

### Verifying the server

~~~powershell
Invoke-WebRequest http://localhost:5180/health
Invoke-WebRequest http://localhost:5180/health/database
# or the packaged probe (exit code 0/1 + logs\health-status.txt):
powershell -NoProfile -ExecutionPolicy Bypass -File C:\RaqmiSystem\scripts\check-health.ps1
~~~

`check-health.ps1` writes its result to
`C:\RaqmiSystem\logs\health-status.txt` (a plain state file was chosen over
the Windows event log: no event-source registration needed, and whoever
investigates an incident opens that logs directory anyway). It can be
registered as a repeating scheduled task for a local alert signal - the
exact command is in the script's header comment.

## Backups

`C:\RaqmiSystem\scripts\backup-raqmi.ps1` is the Windows-native equivalent
of `deploy/backup/pg-backup.sh`: native `pg_dump.exe` in custom format
(`-Fc`, compressed by default - chosen over `Compress-Archive`/gzip pipes as
the most robust single-tool option on Windows), same retention (7 daily,
4 weekly kept on Sundays, 6 monthly kept on the 1st), and the same
guarantees: a failed dump exits non-zero and leaves no partial file (the
dump is written to a `.part` file renamed only after success).

The dump connects as `raqmi_app` with the password read from the protected
config file, passed via the `PGPASSWORD` environment variable only. If a
future migration adds a brand-new schema, extend and re-run
`create-app-role.sql` so `raqmi_app` (and therefore the backup) can read it.

### Testing a restore

A backup that has never been restored is not verified. Restore into a
scratch database, never the real one (`CREATE`/`DROP DATABASE` need the
admin account, not `raqmi_app`):

~~~powershell
$pg = 'C:\Program Files\PostgreSQL\18\bin'
& "$pg\psql.exe" -h localhost -U postgres -d postgres -c "CREATE DATABASE raqmi_restore_test;"
& "$pg\pg_restore.exe" -h localhost -U postgres -d raqmi_restore_test --no-owner "C:\RaqmiSystem\backups\daily\<file>.dump"
& "$pg\psql.exe" -h localhost -U postgres -d raqmi_restore_test -c "SELECT count(*) FROM security.users;"
& "$pg\psql.exe" -h localhost -U postgres -d postgres -c "DROP DATABASE raqmi_restore_test;"
~~~

## Client workstations

Each workstation gets the same installer package,
`deploy/installer/output/RaqmiSystem-Setup.exe` (built as described in
`docs/deployment.md`; the output directory is git-ignored, so build it once
and carry the .exe to the site). The installer does not ask for the API URL.

Pointing a workstation at the server is a separate per-machine step. The
client resolves its API base URL in this order
(`src/RaqmiSystem.Desktop/DesktopSettings.cs`):

1. The `RAQMI_DESKTOP_API_URL` environment variable, if set.
2. `%APPDATA%\RaqmiSystem\desktop-settings.json`, if present.
3. The hard-coded fallback `http://localhost:5180`.

Either mechanism works; pick one per site and stick to it:

~~~powershell
# Option A - per-user environment variable:
[Environment]::SetEnvironmentVariable('RAQMI_DESKTOP_API_URL', 'http://<server-ip>:5180', 'User')

# Option B - settings file:
New-Item -ItemType Directory -Force -Path "$env:APPDATA\RaqmiSystem" | Out-Null
Set-Content -Path "$env:APPDATA\RaqmiSystem\desktop-settings.json" `
  -Value '{ "ApiBaseUrl": "http://<server-ip>:5180" }' -Encoding utf8
~~~

`<server-ip>` is the address printed in the installer's recap. Quick test
from a workstation: open `http://<server-ip>:5180/health` in a browser - it
must return the JSON health payload.

## Assumed limitations of this mode

* **Single site, LAN only.** The API is reachable only from the hotel's
  local network. There is no remote/multi-site access; that is exactly the
  pilot's scope.
* **HTTP on the LAN, no TLS.** The VPS path terminates TLS in Caddy; here
  clients talk plain HTTP to the server. This is acceptable for the pilot
  because the traffic never leaves the site's private network, the firewall
  rule excludes the Public profile, and the API still enforces JWT
  authentication and per-permission authorization on every endpoint. It
  would *not* be acceptable over the internet - which is why the later VPS
  migration reintroduces Caddy/TLS.
* **The later VPS switch changes no code.** The API reads all its
  configuration from `RAQMI_*` environment variables in both modes, the
  schema is owned by the same EF Core migrations, and CI already publishes
  the container image to ghcr.io on version tags. Moving to the VPS means
  following `docs/deployment.md` with a restored backup - plus repointing
  each workstation's API URL (the one per-machine step above).
* **Server PC availability = system availability.** No failover; if the PC
  is off, the clients cannot work. Backups (and restore drills) are the
  mitigation for data loss, not for downtime.

## Go-live checklist (on-premise pilot)

Before opening the system to the unit's staff:

- [ ] **Migrations applied**: `dotnet tool run dotnet-ef -- database update ...`
      ran without error during install (re-running it reports "No migrations
      were applied" when up to date).
- [ ] **`raqmi_app` role active**: `psql -U postgres -d raqmi_system -c "\du raqmi_app"`
      shows the role, and the API's `/health/database` returns 200 (the API
      connects as `raqmi_app`).
- [ ] **Unique JWT key generated**: `raqmi.env.ps1` contains the freshly
      generated `RAQMI_JWT__SIGNINGKEY` (never a value copied from another
      environment or from Git), and the file's ACL is restricted
      (`icacls C:\RaqmiSystem\config\raqmi.env.ps1` lists only
      Administrators and SYSTEM).
- [ ] **Backup task active and restore tested**: task "Raqmi System Backup"
      exists (`Get-ScheduledTask 'Raqmi System Backup'`), a manual run
      produced a file under `backups\daily\`, and that file was restored
      into a scratch database as described above.
- [ ] **Firewall limited to the LAN**: rule "Raqmi System API" exists on
      Domain/Private profiles only, and the server's network connection is
      not classified Public.
- [ ] **Administrator created and password changed at first login**: the
      initial admin logs in from a workstation and replaces the temporary
      password (the account is seeded with `MustChangePassword = true`).
- [ ] **Client installed on every workstation**: `RaqmiSystem-Setup.exe`
      run, API URL configured, `/health` reachable from each machine, and a
      real login performed.
- [ ] **Rollback procedure understood**: to roll back a bad update, restore
      the latest known-good backup into `raqmi_system` (as admin, after
      stopping the "Raqmi System API" task) and reinstall the previous
      published API executable / previous client `RaqmiSystem-Setup.exe` on
      the workstations. Keep the previous installer .exe and the pre-update
      backup until the new version is validated.
