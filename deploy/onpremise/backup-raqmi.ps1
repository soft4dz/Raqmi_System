# deploy/onpremise/backup-raqmi.ps1
#
# Windows-native equivalent of deploy/backup/pg-backup.sh for the on-premise
# pilot server (no Docker): dumps the raqmi_system database with the native
# pg_dump.exe and applies the same retention policy:
#
#   7 daily backups, 4 weekly backups (kept on Sundays),
#   6 monthly backups (kept on the 1st of the month).
#
# Same guarantees as the bash script:
#   * a failed dump leaves NO partial file behind (the dump goes to a .part
#     file that is only renamed into place after pg_dump exits 0 and the file
#     is non-empty);
#   * any failure exits with a non-zero code, so the scheduled task's "last
#     run result" is trustworthy.
#
# Compression: pg_dump's custom format (-Fc) is used instead of
# Compress-Archive or a gzip pipe. It is compressed by default, produced by
# a single robust tool (no pipeline whose first half can fail silently under
# PowerShell 5.1), and restores with pg_restore.exe from the same PostgreSQL
# installation. Restore example (into a scratch database, as admin):
#
#   pg_restore -h localhost -U postgres -d raqmi_restore_test --no-owner "<file>.dump"
#
# Configuration comes exclusively from the protected environment file written
# by install-server.ps1 (default: <install-root>\config\raqmi.env.ps1, i.e.
# the 'config' directory next to this script's 'scripts' directory). Override
# with -EnvFile or the RAQMI_ENV_FILE environment variable (used for tests).
# Variables read from it:
#   RAQMI_POSTGRES__HOST / __PORT / __DATABASE / __USER / __PASSWORD
#   RAQMI_PG_BIN       directory containing pg_dump.exe
#   RAQMI_BACKUP_DIR   root directory backups are written under
#
# The PostgreSQL password is passed to pg_dump via the PGPASSWORD environment
# variable only - never as a command-line argument (arguments are visible in
# process listings and console history).
#
# The dump runs as the raqmi_app role, which create-app-role.sql grants SELECT
# on every current and future table of the security/audit/organization/
# exploitation schemas. If a future migration adds a brand-new SCHEMA, re-run
# create-app-role.sql (extended for it) or the dump will miss that schema.

[CmdletBinding()]
param(
    [string]$EnvFile = ''
)

$ErrorActionPreference = 'Stop'

function Write-BackupLog {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ('[backup-raqmi] ' + $Message)
}

# --- Configuration ------------------------------------------------------------

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
    if (-not [string]::IsNullOrWhiteSpace($env:RAQMI_ENV_FILE)) {
        $EnvFile = $env:RAQMI_ENV_FILE
    }
    else {
        $EnvFile = Join-Path (Split-Path -Parent $PSScriptRoot) 'config\raqmi.env.ps1'
    }
}

if (-not (Test-Path $EnvFile)) {
    Write-BackupLog "ERROR: environment file not found: $EnvFile"
    exit 1
}

. $EnvFile

$missing = @()
foreach ($name in @('RAQMI_POSTGRES__DATABASE', 'RAQMI_POSTGRES__USER', 'RAQMI_POSTGRES__PASSWORD', 'RAQMI_PG_BIN', 'RAQMI_BACKUP_DIR')) {
    $value = [Environment]::GetEnvironmentVariable($name)
    if ([string]::IsNullOrWhiteSpace($value)) {
        $missing += $name
    }
}
if ($missing.Count -gt 0) {
    Write-BackupLog ('ERROR: missing variables in {0}: {1}' -f $EnvFile, ($missing -join ', '))
    exit 1
}

$pgDumpHost = $env:RAQMI_POSTGRES__HOST
if ([string]::IsNullOrWhiteSpace($pgDumpHost)) { $pgDumpHost = 'localhost' }
$pgDumpPort = $env:RAQMI_POSTGRES__PORT
if ([string]::IsNullOrWhiteSpace($pgDumpPort)) { $pgDumpPort = '5432' }
$database = $env:RAQMI_POSTGRES__DATABASE
$pgUser = $env:RAQMI_POSTGRES__USER
$backupRoot = $env:RAQMI_BACKUP_DIR

$pgDump = Join-Path $env:RAQMI_PG_BIN 'pg_dump.exe'
if (-not (Test-Path $pgDump)) {
    Write-BackupLog "ERROR: pg_dump.exe not found at: $pgDump"
    exit 1
}

$dailyDir = Join-Path $backupRoot 'daily'
$weeklyDir = Join-Path $backupRoot 'weekly'
$monthlyDir = Join-Path $backupRoot 'monthly'
New-Item -ItemType Directory -Force -Path $dailyDir, $weeklyDir, $monthlyDir | Out-Null

# A leftover .part file is by definition a failed partial dump (process killed
# mid-dump: scheduled-task time limit, reboot, power loss). The failure path's
# own Remove-Item never ran, and retention only matches *.dump, so clean them
# up here before dumping - otherwise they accumulate forever.
Get-ChildItem -Path $dailyDir -Filter '*.dump.part' -ErrorAction SilentlyContinue | ForEach-Object {
    Write-BackupLog "Removing stale partial dump from a previous failed run: $($_.FullName)"
    Remove-Item $_.FullName -Force
}

$now = Get-Date
$timestamp = $now.ToString('yyyyMMdd_HHmmss')
$fileName = 'raqmi_{0}_{1}.dump' -f $database, $timestamp
$dailyPath = Join-Path $dailyDir $fileName
$partPath = $dailyPath + '.part'

# --- Dump ---------------------------------------------------------------------

Write-BackupLog "Dumping database '$database' (user '$pgUser') with $pgDump..."

$env:PGPASSWORD = $env:RAQMI_POSTGRES__PASSWORD
try {
    & $pgDump -h $pgDumpHost -p $pgDumpPort -U $pgUser -d $database `
        -Fc --no-owner --no-privileges -f $partPath
    $dumpExitCode = $LASTEXITCODE
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

if ($dumpExitCode -ne 0) {
    Write-BackupLog "ERROR: pg_dump failed (exit code $dumpExitCode) - no backup was produced."
    Remove-Item -Path $partPath -Force -ErrorAction SilentlyContinue
    exit 1
}

if (-not (Test-Path $partPath)) {
    Write-BackupLog 'ERROR: pg_dump reported success but produced no file.'
    exit 1
}

$partSize = (Get-Item $partPath).Length
if ($partSize -le 0) {
    Write-BackupLog "ERROR: backup file '$partPath' is empty."
    Remove-Item -Path $partPath -Force -ErrorAction SilentlyContinue
    exit 1
}

Move-Item -Path $partPath -Destination $dailyPath -Force
Write-BackupLog ('Wrote {0} ({1:N0} bytes)' -f $dailyPath, $partSize)

# --- Weekly / monthly promotion -----------------------------------------------
# Promote today's dump into the weekly/monthly tiers before pruning, so each
# tier only ever has to look at its own directory (same as pg-backup.sh).

if ($now.DayOfWeek -eq [DayOfWeek]::Sunday) {
    Copy-Item -Path $dailyPath -Destination (Join-Path $weeklyDir $fileName) -Force
    Write-BackupLog 'Sunday - also kept as a weekly backup.'
}

if ($now.Day -eq 1) {
    Copy-Item -Path $dailyPath -Destination (Join-Path $monthlyDir $fileName) -Force
    Write-BackupLog 'First of the month - also kept as a monthly backup.'
}

# --- Retention ----------------------------------------------------------------
# Filenames sort chronologically (yyyyMMdd_HHmmss), so keeping the N newest
# per tier is "sort by name, delete the oldest excess".

function Remove-OldBackups {
    param(
        [Parameter(Mandatory = $true)][string]$Directory,
        [Parameter(Mandatory = $true)][int]$Keep
    )

    $files = @(Get-ChildItem -Path $Directory -Filter '*.dump' -File | Sort-Object Name)
    if ($files.Count -gt $Keep) {
        $excess = $files.Count - $Keep
        foreach ($old in ($files | Select-Object -First $excess)) {
            Write-BackupLog ('Removing old backup: ' + $old.FullName)
            Remove-Item -Path $old.FullName -Force
        }
    }
}

Remove-OldBackups -Directory $dailyDir -Keep 7
Remove-OldBackups -Directory $weeklyDir -Keep 4
Remove-OldBackups -Directory $monthlyDir -Keep 6

Write-BackupLog 'Done.'
exit 0
