#!/usr/bin/env bash
#
# deploy/backup/pg-backup.sh
#
# Dumps the Raqmi System PostgreSQL database running in docker-compose.prod.yml
# and applies a simple daily/weekly/monthly retention policy:
#
#   7 daily backups, 4 weekly backups, 6 monthly backups.
#
# Intended target: the production VPS (Linux), run once a day, e.g. via the
# systemd timer in deploy/backup/raqmi-backup.timer (see
# deploy/backup/raqmi-backup.service and docs/deployment.md for installation).
# It is a plain script on purpose - no config file format, no external backup
# tool, just pg_dump + gzip + find/rm.
#
# Usage:
#   deploy/backup/pg-backup.sh
#
# Configuration (all optional environment variables):
#   RAQMI_BACKUP_DIR       Root directory backups are written under.
#                          Default: /var/backups/raqmi
#   RAQMI_COMPOSE_FILE     docker-compose file to run pg_dump through.
#                          Default: docker-compose.prod.yml at the repo root.
#   RAQMI_COMPOSE_SERVICE  Name of the postgres service in that file.
#                          Default: postgres
#   RAQMI_BACKUP_PG_USER   Postgres role pg_dump connects as. Must be able to
#                          read every schema (security, audit, organization,
#                          exploitation) - the restricted `raqmi_app` role
#                          created by deploy/postgres/create-app-role.sql is
#                          NOT guaranteed to be enough for future schemas, so
#                          this defaults to the admin/migration account
#                          (RAQMI_POSTGRES_ADMIN_USER) unless overridden.
#   RAQMI_POSTGRES_ADMIN_USER, RAQMI_POSTGRES__DATABASE
#                          Same admin variable docker-compose.prod.yml uses to
#                          bootstrap the postgres container (never the app's
#                          own RAQMI_POSTGRES__USER, which is the restricted
#                          raqmi_app role in production). Loaded from a .env
#                          file next to the compose file if not already
#                          exported in the current shell (systemd unit, etc).
#
# No password handling: `docker compose exec` runs pg_dump *inside* the
# postgres container, which connects over the local Unix socket rather than
# TCP. The official postgres image's default pg_hba.conf trusts local socket
# connections, so no PGPASSWORD is needed here. If pg_hba.conf has been
# hardened to also require a password on the local socket, export PGPASSWORD
# before calling this script.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

COMPOSE_FILE="${RAQMI_COMPOSE_FILE:-$REPO_ROOT/docker-compose.prod.yml}"
COMPOSE_SERVICE="${RAQMI_COMPOSE_SERVICE:-postgres}"
BACKUP_ROOT="${RAQMI_BACKUP_DIR:-/var/backups/raqmi}"

ENV_FILE="$REPO_ROOT/.env"
if [ -f "$ENV_FILE" ]; then
    # Parsed literally (KEY=VALUE per line), never executed as shell - unlike
    # `source`, this can't run arbitrary commands if a value happens to
    # contain $(...), backticks, or unescaped quotes. Matches how `docker
    # compose` itself reads this same file (no shell evaluation).
    while IFS='=' read -r env_key env_value; do
        case "$env_key" in
            ''|'#'*) continue ;;
        esac
        [ -n "${!env_key:-}" ] && continue
        export "$env_key=$env_value"
    done < "$ENV_FILE"
fi

PG_DATABASE="${RAQMI_POSTGRES__DATABASE:?RAQMI_POSTGRES__DATABASE is not set (export it or define it in .env)}"
ADMIN_PG_USER="${RAQMI_POSTGRES_ADMIN_USER:?RAQMI_POSTGRES_ADMIN_USER is not set (export it or define it in .env)}"
PG_USER="${RAQMI_BACKUP_PG_USER:-$ADMIN_PG_USER}"

DAILY_DIR="$BACKUP_ROOT/daily"
WEEKLY_DIR="$BACKUP_ROOT/weekly"
MONTHLY_DIR="$BACKUP_ROOT/monthly"
mkdir -p "$DAILY_DIR" "$WEEKLY_DIR" "$MONTHLY_DIR"

TIMESTAMP="$(date +%Y%m%d_%H%M%S)"
DAY_OF_MONTH="$(date +%d)"
DAY_OF_WEEK="$(date +%u)" # 1 = Monday ... 7 = Sunday
FILENAME="raqmi_${PG_DATABASE}_${TIMESTAMP}.sql.gz"
DAILY_PATH="$DAILY_DIR/$FILENAME"

echo "[pg-backup] Dumping database '$PG_DATABASE' (user '$PG_USER') via the '$COMPOSE_SERVICE' service..."

if ! docker compose -f "$COMPOSE_FILE" exec -T "$COMPOSE_SERVICE" \
        pg_dump -U "$PG_USER" -d "$PG_DATABASE" --no-owner --no-privileges \
        | gzip > "$DAILY_PATH"; then
    echo "[pg-backup] ERROR: pg_dump failed - no valid backup was produced." >&2
    rm -f "$DAILY_PATH"
    exit 1
fi

if [ ! -s "$DAILY_PATH" ]; then
    echo "[pg-backup] ERROR: backup file '$DAILY_PATH' is empty." >&2
    rm -f "$DAILY_PATH"
    exit 1
fi

echo "[pg-backup] Wrote $DAILY_PATH ($(du -h "$DAILY_PATH" | cut -f1))"

# Promote today's dump into the weekly/monthly tiers before pruning below, so
# each tier only ever has to look at its own directory.
if [ "$DAY_OF_WEEK" = "7" ]; then
    cp "$DAILY_PATH" "$WEEKLY_DIR/$FILENAME"
    echo "[pg-backup] Sunday - also kept as a weekly backup."
fi

if [ "$DAY_OF_MONTH" = "01" ]; then
    cp "$DAILY_PATH" "$MONTHLY_DIR/$FILENAME"
    echo "[pg-backup] First of the month - also kept as a monthly backup."
fi

# Retention: filenames sort chronologically (YYYYMMDD_HHMMSS), so keeping the
# N newest per tier is just "sort, drop the oldest excess, delete them".
prune() {
    local dir="$1"
    local keep="$2"
    local total
    total=$(find "$dir" -maxdepth 1 -name '*.sql.gz' | wc -l)

    if [ "$total" -gt "$keep" ]; then
        find "$dir" -maxdepth 1 -name '*.sql.gz' | sort | head -n "$((total - keep))" | while read -r old; do
            echo "[pg-backup] Removing old backup: $old"
            rm -f "$old"
        done
    fi
}

prune "$DAILY_DIR" 7
prune "$WEEKLY_DIR" 4
prune "$MONTHLY_DIR" 6

echo "[pg-backup] Done."
