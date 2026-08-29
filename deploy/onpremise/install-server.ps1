# deploy/onpremise/install-server.ps1
#
# Installs the Raqmi System API on the pilot on-premise Windows server (no VPS,
# no Docker): native PostgreSQL + self-contained API + scheduled tasks.
#
# =============================================================================
# PREREQUISITE: run this script MANUALLY, in an ELEVATED PowerShell console
# ("Run as Administrator"). It registers scheduled tasks, opens a firewall
# port and writes ACL-protected files - none of that works unelevated.
# The script checks for elevation and exits immediately if it is missing.
#
# Other prerequisites (see docs/deployment-onpremise.md):
#   * Windows PowerShell 5.1 (built into Windows).
#   * PostgreSQL installed natively (default binaries under
#     "C:\Program Files\PostgreSQL\18\bin", override with -PgBin).
#   * .NET SDK 10 on PATH - needed for the INSTALL only (EF migrations and
#     `dotnet publish`); the published API is self-contained afterwards.
#   * This repository cloned locally (the script resolves paths relative to
#     its own location: deploy\onpremise\ -> repository root).
# =============================================================================
#
# What it does, in order:
#   1. Prompts (interactively, never as command-line parameters) for the
#      PostgreSQL admin password and the password to set on the raqmi_app role.
#   2. Generates a random 64-byte base64 JWT signing key.
#   3. Creates the raqmi_system database if absent (as the admin user).
#   4. Applies the EF Core migrations (dotnet tool restore + dotnet-ef) as the
#      admin user - migrations run BEFORE create-app-role.sql, as documented
#      in docs/deployment.md (the GRANTs need the schemas to exist).
#   5. Runs deploy/postgres/create-app-role.sql (psql -v app_password=...).
#   6. Publishes the API self-contained to <InstallDir>\api.
#   7. Writes the protected environment file <InstallDir>\config\raqmi.env.ps1
#      (ACL restricted to Administrators + SYSTEM via icacls) - this is the
#      single source of runtime configuration, dot-sourced by start-api.ps1,
#      backup-raqmi.ps1 and check-health.ps1.
#   8. Seeds the security catalog (RaqmiSystem.Api.exe --seed-security) and,
#      optionally, the initial administrator account.
#   9. Registers the "Raqmi System API" scheduled task (SYSTEM, at boot,
#      restart on failure) and starts it. A scheduled task is used instead of
#      a Windows service on purpose: a console .NET app is not a service - it
#      would need a wrapper (NSSM, sc.exe tricks, or code changes) to answer
#      the Service Control Manager, while the Task Scheduler runs any
#      executable at boot under SYSTEM with built-in restart-on-failure,
#      with zero extra dependencies.
#  10. Opens Windows Firewall for the API port on Domain/Private profiles
#      ONLY (never Public), rule name "Raqmi System API".
#  11. Registers the daily "Raqmi System Backup" scheduled task at 03:30.
#  12. Prints a recap: client URL, backup location, health-check commands.
#
# No password is ever accepted as a command-line parameter (it would persist
# in the console history), and none is ever echoed back.

[CmdletBinding()]
param(
    [int]$ApiPort = 5180,
    [string]$DataDir = 'C:\RaqmiSystem\backups',
    [string]$PgBin = 'C:\Program Files\PostgreSQL\18\bin',
    [string]$PgAdminUser = 'postgres',
    [string]$PgServerHost = 'localhost',
    [int]$PgPort = 5432,
    [string]$Database = 'raqmi_system',
    [string]$InstallDir = 'C:\RaqmiSystem'
)

$ErrorActionPreference = 'Stop'

# --- 0. Elevation check ------------------------------------------------------

$identity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object System.Security.Principal.WindowsPrincipal($identity)
if (-not $principal.IsInRole([System.Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'ERROR: this script must run in an elevated PowerShell console.' -ForegroundColor Red
    Write-Host 'Right-click PowerShell, choose "Run as Administrator", then re-run:'
    Write-Host "  powershell -NoProfile -ExecutionPolicy Bypass -File `"$($MyInvocation.MyCommand.Path)`""
    exit 1
}

# --- Helpers -----------------------------------------------------------------

function ConvertTo-PlainText {
    param([Parameter(Mandatory = $true)][securestring]$Secure)
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        return [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

function Assert-LastExitCode {
    param([Parameter(Mandatory = $true)][string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "Step failed (exit code ${LASTEXITCODE}): $Step"
    }
}

# Values are written into raqmi.env.ps1 inside single quotes; doubling the
# single quotes is the only escaping single-quoted PowerShell strings need.
function ConvertTo-SingleQuoted {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value)
    return "'" + ($Value -replace "'", "''") + "'"
}

# --- Path and tool resolution ------------------------------------------------

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$psql = Join-Path $PgBin 'psql.exe'
$createRoleSql = Join-Path $repoRoot 'deploy\postgres\create-app-role.sql'
$apiProject = Join-Path $repoRoot 'src\RaqmiSystem.Api\RaqmiSystem.Api.csproj'
$infraProject = Join-Path $repoRoot 'src\RaqmiSystem.Infrastructure\RaqmiSystem.Infrastructure.csproj'

$apiDir = Join-Path $InstallDir 'api'
$configDir = Join-Path $InstallDir 'config'
$scriptsDir = Join-Path $InstallDir 'scripts'
$logsDir = Join-Path $InstallDir 'logs'
$configFile = Join-Path $configDir 'raqmi.env.ps1'
$apiExe = Join-Path $apiDir 'RaqmiSystem.Api.exe'

if (-not (Test-Path $psql)) {
    throw "psql.exe not found at '$psql'. Install PostgreSQL natively or pass -PgBin."
}
if (-not (Test-Path $createRoleSql)) {
    throw "create-app-role.sql not found at '$createRoleSql'. Run this script from a full clone of the repository."
}
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if ($null -eq $dotnet) {
    throw 'dotnet was not found on PATH. Install the .NET SDK 10 (needed for the installation only).'
}

Write-Host "Raqmi System on-premise installation" -ForegroundColor Cyan
Write-Host "  Repository : $repoRoot"
Write-Host "  Install dir: $InstallDir"
Write-Host "  API port   : $ApiPort"
Write-Host "  Backups    : $DataDir"
Write-Host "  PostgreSQL : $PgServerHost`:$PgPort (admin user '$PgAdminUser', bin '$PgBin')"
Write-Host ''

# --- 1. Interactive secrets --------------------------------------------------

$adminSecure = Read-Host -AsSecureString "Password of the PostgreSQL admin user '$PgAdminUser'"
$adminPassword = ConvertTo-PlainText -Secure $adminSecure
if ([string]::IsNullOrWhiteSpace($adminPassword)) {
    throw 'The PostgreSQL admin password cannot be empty.'
}

Write-Host ''
Write-Host "Password to SET on the application role 'raqmi_app'."
Write-Host 'Leave empty to generate a strong random one (recommended - it is stored'
Write-Host 'in the protected config file, nobody needs to type it again).'
$appSecure = Read-Host -AsSecureString "Password for 'raqmi_app' (empty = generate)"
$appPassword = ConvertTo-PlainText -Secure $appSecure

if ([string]::IsNullOrWhiteSpace($appPassword)) {
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $appBytes = New-Object byte[] 24
    $rng.GetBytes($appBytes)
    $rng.Dispose()
    # base64 then made shell/psql-safe: no quotes, no '+', '/' or '='.
    $appPassword = ([Convert]::ToBase64String($appBytes) -replace '\+', 'A' -replace '/', 'B' -replace '=', '')
    Write-Host 'Generated a random raqmi_app password.'
}
else {
    if ($appPassword.Length -lt 16) {
        throw 'The raqmi_app password must be at least 16 characters long.'
    }
    if ($appPassword -match "['`"]" -or $appPassword -match '\s') {
        # The value travels through `psql -v app_password=...`; quotes and
        # whitespace make that fragile. Random generation avoids them entirely.
        throw 'The raqmi_app password must not contain quotes or whitespace (or leave it empty to auto-generate).'
    }
}

Write-Host ''
Write-Host 'Initial administrator account (seeded with MustChangePassword=true,'
Write-Host 'so the password below is temporary and must be changed at first login).'
$initialAdminEmail = Read-Host "Initial admin email (empty = skip account creation)"
$initialAdminPassword = ''
if (-not [string]::IsNullOrWhiteSpace($initialAdminEmail)) {
    $initialAdminSecure = Read-Host -AsSecureString 'Temporary password for that admin (min 12 characters)'
    $initialAdminPassword = ConvertTo-PlainText -Secure $initialAdminSecure
    if ($initialAdminPassword.Length -lt 12) {
        throw 'The initial administrator password must be at least 12 characters long (enforced by the seeder).'
    }
}

# --- 2. JWT signing key ------------------------------------------------------

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$jwtBytes = New-Object byte[] 64
$rng.GetBytes($jwtBytes)
$rng.Dispose()
$jwtSigningKey = [Convert]::ToBase64String($jwtBytes)
Write-Host 'Generated a random 64-byte JWT signing key.'

# --- 3. Create the database if absent ----------------------------------------

Write-Host ''
Write-Host "Checking that database '$Database' exists..." -ForegroundColor Cyan
$env:PGPASSWORD = $adminPassword
try {
    $dbExists = & $psql -h $PgServerHost -p $PgPort -U $PgAdminUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$Database';"
    Assert-LastExitCode -Step 'psql: check database existence (is the PostgreSQL service running, is the admin password correct?)'

    if ("$dbExists".Trim() -ne '1') {
        Write-Host "Creating database '$Database'..."
        & $psql -h $PgServerHost -p $PgPort -U $PgAdminUser -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE $Database;"
        Assert-LastExitCode -Step "psql: CREATE DATABASE $Database"
    }
    else {
        Write-Host "Database '$Database' already exists."
    }
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
}

# --- 4. EF Core migrations (BEFORE create-app-role.sql) -----------------------
# docs/deployment.md: the GRANT USAGE ON SCHEMA statements in
# create-app-role.sql fail unless the schemas already exist, so migrations
# must run first, under the admin account.

Write-Host ''
Write-Host 'Applying EF Core migrations (admin account)...' -ForegroundColor Cyan

$env:RAQMI_POSTGRES__HOST = $PgServerHost
$env:RAQMI_POSTGRES__PORT = "$PgPort"
$env:RAQMI_POSTGRES__DATABASE = $Database
$env:RAQMI_POSTGRES__USER = $PgAdminUser
$env:RAQMI_POSTGRES__PASSWORD = $adminPassword
# The startup project is the API itself: give it a valid JWT key so
# JwtOptions.Validate() does not abort the design-time host outside Development.
$env:RAQMI_JWT__SIGNINGKEY = $jwtSigningKey
$env:ASPNETCORE_ENVIRONMENT = 'Production'

Push-Location $repoRoot
try {
    & dotnet tool restore
    Assert-LastExitCode -Step 'dotnet tool restore'

    & dotnet tool run dotnet-ef -- database update --project $infraProject --startup-project $apiProject
    Assert-LastExitCode -Step 'dotnet-ef database update'
}
finally {
    Pop-Location
}

# --- 5. Application role (create-app-role.sql) --------------------------------

Write-Host ''
Write-Host "Creating/refreshing the 'raqmi_app' role..." -ForegroundColor Cyan
# The app password must never appear on a child process command line (argv is
# visible to every local user via Task Manager / Win32_Process while psql
# runs). Instead, write a short-lived ACL-restricted wrapper script that \set's
# the variable, include the real SQL file from it, and pass only the wrapper's
# path to psql.
$env:PGPASSWORD = $adminPassword
$roleWrapper = Join-Path $env:TEMP ("raqmi-role-{0}.psql" -f [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType File -Path $roleWrapper -Force | Out-Null
    & icacls $roleWrapper /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F' | Out-Null
    Assert-LastExitCode -Step 'icacls: restrict role wrapper file'
    $escapedAppPassword = $appPassword -replace "'", "''"
    $sqlPathForPsql = $createRoleSql -replace '\\', '/'
    Set-Content -Path $roleWrapper -Encoding ascii -Value @(
        ("\set app_password '{0}'" -f $escapedAppPassword)
        ("\i {0}" -f $sqlPathForPsql)
    )
    & $psql -h $PgServerHost -p $PgPort -U $PgAdminUser -d $Database -v ON_ERROR_STOP=1 -f $roleWrapper
    Assert-LastExitCode -Step 'psql: create-app-role.sql'
}
finally {
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item $roleWrapper -Force -ErrorAction SilentlyContinue
}
Write-Host 'NOTE: create-app-role.sql never resets the password of an already-existing'
Write-Host 'raqmi_app role. On a re-install over an existing database, the password'
Write-Host 'written to the config file below only matches if it is the original one,'
Write-Host "or after running: ALTER ROLE raqmi_app PASSWORD '<new-value>'; as admin."

# --- 6. Publish the API (self-contained) --------------------------------------

Write-Host ''
Write-Host "Publishing the API to $apiDir (self-contained win-x64)..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $apiDir | Out-Null
& dotnet publish $apiProject -c Release -r win-x64 --self-contained true -o $apiDir
Assert-LastExitCode -Step 'dotnet publish RaqmiSystem.Api'
if (-not (Test-Path $apiExe)) {
    throw "Publish finished but '$apiExe' was not produced."
}

# --- 7. Protected environment file --------------------------------------------
# Single chosen approach: one dot-sourceable PowerShell file, outside the
# publish output (so re-publishing never touches it), ACL-restricted to
# Administrators + SYSTEM. start-api.ps1 / backup-raqmi.ps1 / check-health.ps1
# all read their configuration exclusively from this file.

Write-Host ''
Write-Host "Writing protected configuration file $configFile..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path $configDir | Out-Null
New-Item -ItemType Directory -Force -Path $scriptsDir | Out-Null
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null

# Lock the file down BEFORE any secret is written into it: create it empty,
# drop inheritance and grant only Administrators (S-1-5-32-544) and SYSTEM
# (S-1-5-18) - SIDs, not names, so this works on localized Windows editions.
# Writing first and restricting after would leave a window where the inherited
# C:\ ACLs (BUILTIN\Users read) expose the DB password and JWT key.
New-Item -ItemType File -Path $configFile -Force | Out-Null
& icacls $configFile /inheritance:r /grant:r '*S-1-5-32-544:F' '*S-1-5-18:F'
Assert-LastExitCode -Step 'icacls: restrict raqmi.env.ps1'

$configLines = @(
    '# raqmi.env.ps1 - generated by deploy/onpremise/install-server.ps1'
    '# Contains secrets. ACLs restrict it to Administrators + SYSTEM; do not'
    '# copy it elsewhere and never commit it. Dot-sourced by start-api.ps1,'
    '# backup-raqmi.ps1 and check-health.ps1.'
    ('$env:ASPNETCORE_ENVIRONMENT = {0}' -f (ConvertTo-SingleQuoted 'Production'))
    ('$env:ASPNETCORE_URLS = {0}' -f (ConvertTo-SingleQuoted "http://0.0.0.0:$ApiPort"))
    ('$env:RAQMI_POSTGRES__HOST = {0}' -f (ConvertTo-SingleQuoted $PgServerHost))
    ('$env:RAQMI_POSTGRES__PORT = {0}' -f (ConvertTo-SingleQuoted "$PgPort"))
    ('$env:RAQMI_POSTGRES__DATABASE = {0}' -f (ConvertTo-SingleQuoted $Database))
    ('$env:RAQMI_POSTGRES__USER = {0}' -f (ConvertTo-SingleQuoted 'raqmi_app'))
    ('$env:RAQMI_POSTGRES__PASSWORD = {0}' -f (ConvertTo-SingleQuoted $appPassword))
    ('$env:RAQMI_JWT__ISSUER = {0}' -f (ConvertTo-SingleQuoted 'RaqmiSystem'))
    ('$env:RAQMI_JWT__AUDIENCE = {0}' -f (ConvertTo-SingleQuoted 'RaqmiSystem.Client'))
    ('$env:RAQMI_JWT__SIGNINGKEY = {0}' -f (ConvertTo-SingleQuoted $jwtSigningKey))
    ('$env:RAQMI_JWT__ACCESSTOKENMINUTES = {0}' -f (ConvertTo-SingleQuoted '60'))
    '# Used by the operational scripts only (not read by the API itself):'
    ('$env:RAQMI_API_PORT = {0}' -f (ConvertTo-SingleQuoted "$ApiPort"))
    ('$env:RAQMI_PG_BIN = {0}' -f (ConvertTo-SingleQuoted $PgBin))
    ('$env:RAQMI_BACKUP_DIR = {0}' -f (ConvertTo-SingleQuoted $DataDir))
)
Set-Content -Path $configFile -Value $configLines -Encoding utf8

# --- 8. Security seed ---------------------------------------------------------
# RaqmiSystem.Api.exe --seed-security (see Program.cs) populates the
# permission/role catalog, and creates the initial administrator only when
# RAQMI_INITIAL_ADMIN_EMAIL / RAQMI_INITIAL_ADMIN_PASSWORD are set.

Write-Host ''
Write-Host 'Seeding the security catalog...' -ForegroundColor Cyan
$env:RAQMI_POSTGRES__USER = 'raqmi_app'
$env:RAQMI_POSTGRES__PASSWORD = $appPassword
$env:ASPNETCORE_URLS = "http://0.0.0.0:$ApiPort"
if (-not [string]::IsNullOrWhiteSpace($initialAdminEmail)) {
    $env:RAQMI_INITIAL_ADMIN_EMAIL = $initialAdminEmail
    $env:RAQMI_INITIAL_ADMIN_PASSWORD = $initialAdminPassword
}
try {
    & $apiExe --seed-security
    Assert-LastExitCode -Step 'RaqmiSystem.Api.exe --seed-security'
}
finally {
    Remove-Item Env:RAQMI_INITIAL_ADMIN_EMAIL -ErrorAction SilentlyContinue
    Remove-Item Env:RAQMI_INITIAL_ADMIN_PASSWORD -ErrorAction SilentlyContinue
}

# --- 9. Copy operational scripts ---------------------------------------------
# Scheduled tasks point at stable copies under the install dir, so the cloned
# repository can move or disappear without breaking the running server.

Copy-Item -Path (Join-Path $PSScriptRoot 'start-api.ps1') -Destination $scriptsDir -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'backup-raqmi.ps1') -Destination $scriptsDir -Force
Copy-Item -Path (Join-Path $PSScriptRoot 'check-health.ps1') -Destination $scriptsDir -Force

# --- 10. Scheduled task: API at boot ------------------------------------------

Write-Host ''
Write-Host "Registering scheduled task 'Raqmi System API'..." -ForegroundColor Cyan
$existingTask = Get-ScheduledTask -TaskName 'Raqmi System API' -ErrorAction SilentlyContinue
if ($null -ne $existingTask) {
    Stop-ScheduledTask -TaskName 'Raqmi System API' -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName 'Raqmi System API' -Confirm:$false
}

$apiAction = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f (Join-Path $scriptsDir 'start-api.ps1'))
$apiTrigger = New-ScheduledTaskTrigger -AtStartup
$apiPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$apiSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit ([TimeSpan]::Zero)
Register-ScheduledTask -TaskName 'Raqmi System API' `
    -Action $apiAction -Trigger $apiTrigger -Principal $apiPrincipal -Settings $apiSettings `
    -Description 'Starts the Raqmi System API at boot (deploy/onpremise).' | Out-Null

# --- 11. Firewall: API port, Private/Domain only ------------------------------

Write-Host "Opening Windows Firewall for TCP $ApiPort (Domain/Private profiles only)..." -ForegroundColor Cyan
$existingRule = Get-NetFirewallRule -DisplayName 'Raqmi System API' -ErrorAction SilentlyContinue
if ($null -ne $existingRule) {
    Remove-NetFirewallRule -DisplayName 'Raqmi System API'
}
New-NetFirewallRule -DisplayName 'Raqmi System API' `
    -Direction Inbound -Protocol TCP -LocalPort $ApiPort `
    -Action Allow -Profile Domain, Private | Out-Null

# --- 12. Scheduled task: daily backup at 03:30 --------------------------------

Write-Host "Registering scheduled task 'Raqmi System Backup' (daily 03:30)..." -ForegroundColor Cyan
$existingBackupTask = Get-ScheduledTask -TaskName 'Raqmi System Backup' -ErrorAction SilentlyContinue
if ($null -ne $existingBackupTask) {
    Unregister-ScheduledTask -TaskName 'Raqmi System Backup' -Confirm:$false
}

$backupAction = New-ScheduledTaskAction -Execute 'powershell.exe' `
    -Argument ('-NoProfile -ExecutionPolicy Bypass -File "{0}"' -f (Join-Path $scriptsDir 'backup-raqmi.ps1'))
$backupTrigger = New-ScheduledTaskTrigger -Daily -At '03:30'
$backupPrincipal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
$backupSettings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable `
    -ExecutionTimeLimit (New-TimeSpan -Hours 2)
Register-ScheduledTask -TaskName 'Raqmi System Backup' `
    -Action $backupAction -Trigger $backupTrigger -Principal $backupPrincipal -Settings $backupSettings `
    -Description 'Daily pg_dump backup of raqmi_system with 7d/4w/6m retention (deploy/onpremise).' | Out-Null

# --- 13. Start the API and verify /health -------------------------------------

Write-Host ''
Write-Host 'Starting the API task...' -ForegroundColor Cyan
Start-ScheduledTask -TaskName 'Raqmi System API'

$healthy = $false
$healthUrl = "http://localhost:$ApiPort/health"
for ($attempt = 1; $attempt -le 15; $attempt++) {
    Start-Sleep -Seconds 2
    try {
        $response = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 5
        if ($response.StatusCode -eq 200) {
            $healthy = $true
            break
        }
    }
    catch {
        # Not up yet - keep polling.
    }
}

# --- 14. Recap ----------------------------------------------------------------

$localIp = '<server-ip>'
$ipConfig = Get-NetIPConfiguration -ErrorAction SilentlyContinue |
    Where-Object { $null -ne $_.IPv4DefaultGateway -and $null -ne $_.IPv4Address } |
    Select-Object -First 1
if ($null -ne $ipConfig) {
    $localIp = $ipConfig.IPv4Address.IPAddress
}

Write-Host ''
Write-Host '=================================================================' -ForegroundColor Green
Write-Host ' Raqmi System - on-premise installation complete' -ForegroundColor Green
Write-Host '================================================================='
if ($healthy) {
    Write-Host " API status         : HEALTHY ($healthUrl answered 200)"
}
else {
    Write-Host ' API status         : NOT CONFIRMED - check the log files below and' -ForegroundColor Yellow
    Write-Host "                      re-test manually: Invoke-WebRequest $healthUrl" -ForegroundColor Yellow
}
Write-Host " Client API URL     : http://${localIp}:$ApiPort"
Write-Host '                      (set RAQMI_DESKTOP_API_URL, or ApiBaseUrl in'
Write-Host '                      %APPDATA%\RaqmiSystem\desktop-settings.json, on each workstation)'
Write-Host " Config (protected) : $configFile"
Write-Host " API binaries       : $apiDir"
Write-Host " API logs           : $logsDir"
Write-Host " Backups            : $DataDir (daily 03:30, task 'Raqmi System Backup')"
Write-Host ' Health check       : Invoke-WebRequest http://localhost:' -NoNewline
Write-Host "$ApiPort/health  (and /health/database)"
Write-Host "                      or: powershell -NoProfile -File `"$(Join-Path $scriptsDir 'check-health.ps1')`""
Write-Host " Firewall           : rule 'Raqmi System API', TCP $ApiPort, Domain/Private only"
Write-Host '================================================================='
Write-Host 'Next steps: see docs/deployment-onpremise.md (client workstations,'
Write-Host 'restore test, go-live checklist).'

# Best effort: drop plaintext secrets from this session's memory.
$adminPassword = $null
$appPassword = $null
$initialAdminPassword = $null
$jwtSigningKey = $null
Remove-Item Env:RAQMI_POSTGRES__PASSWORD -ErrorAction SilentlyContinue
Remove-Item Env:RAQMI_JWT__SIGNINGKEY -ErrorAction SilentlyContinue
[System.GC]::Collect()
