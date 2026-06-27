param(
  [string]$InstallRoot = "$env:ProgramFiles\Raqmi System Server",
  [string]$DataRoot = "$env:ProgramData\Raqmi System"
)

$ErrorActionPreference = 'Stop'
$pgBin = Join-Path $InstallRoot 'postgresql\bin'
$pgData = Join-Path $DataRoot 'data\postgres'
$pgLog = Join-Path $DataRoot 'logs\postgres.log'

if (-not (Test-Path (Join-Path $pgBin 'initdb.exe'))) {
  Write-Error "PostgreSQL embarqué introuvable dans $pgBin. Consultez installer/assets/postgresql/README.md"
}

New-Item -ItemType Directory -Force -Path $pgData | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $pgLog) | Out-Null

if (-not (Test-Path (Join-Path $pgData 'PG_VERSION'))) {
  & (Join-Path $pgBin 'initdb.exe') -D $pgData -U postgres -A trust -E UTF8 --locale=C
}

$env:PGDATA = $pgData
& (Join-Path $pgBin 'pg_ctl.exe') -D $pgData -l $pgLog -w start

$psql = Join-Path $pgBin 'psql.exe'
& $psql -U postgres -d postgres -c "DO $$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'raqmi') THEN CREATE ROLE raqmi LOGIN PASSWORD 'raqmi_password'; END IF; END $$;"
& $psql -U postgres -d postgres -c "SELECT 1 FROM pg_database WHERE datname = 'raqmi_system'" | Out-Null
if ($LASTEXITCODE -ne 0) {
  & $psql -U postgres -d postgres -c "CREATE DATABASE raqmi_system OWNER raqmi;"
}

Write-Host "PostgreSQL initialisé : $pgData"
