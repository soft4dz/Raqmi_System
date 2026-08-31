<#
.SYNOPSIS
    Chaine complete de production des captures du guide utilisateur.

.DESCRIPTION
    Enchaine, sur une base de demonstration DEDIEE :

      1. migrations EF Core sur la base de demonstration ;
      2. seed de securite (permissions, roles, premier administrateur) ;
      3. demarrage d'une instance d'API sur un port dedie ;
      4. jeu de donnees de demonstration (tools/demo-seed) ;
      5. campagne de captures du client WPF (tools/RaqmiSystem.DocShots) ;
      6. arret de l'instance d'API.

    Rien de tout cela ne touche la base ni l'API de travail : la base, le port et le dossier
    de sortie sont des parametres, et leurs valeurs par defaut sont toutes dediees au guide.

    PREALABLE : la base doit exister et appartenir au role applicatif. Le role `raqmi` n'a pas
    le droit CREATEDB, cette commande se lance donc une fois avec le superutilisateur :

        psql -U postgres -h localhost -c "CREATE DATABASE raqmi_demo OWNER raqmi"

.EXAMPLE
    .\tools\generate-guide.ps1
#>
[CmdletBinding()]
param(
    [string]$Database = 'raqmi_demo',
    [string]$PostgresHost = 'localhost',
    [int]$PostgresPort = 5432,
    [string]$PostgresUser = 'raqmi',
    [string]$PostgresPassword = 'change-me',
    [int]$ApiPort = 5190,
    [string]$AdminEmail = 'admin@demo.local',
    [string]$AdminPassword = 'Demo-Admin-2026!',
    [string]$OutputDirectory = 'docs/guide/captures',
    [switch]$SkipSeed,
    [switch]$SkipCapture
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repositoryRoot

$apiBaseUrl = 'http://localhost:' + $ApiPort
$connectionString = "Host=$PostgresHost;Port=$PostgresPort;Database=$Database;Username=$PostgresUser;Password=$PostgresPassword"

function Write-Phase {
    param([string]$Message)
    Write-Host ''
    Write-Host ('### ' + $Message) -ForegroundColor White
}

# ------------------------------------------------------------------------ 1. Migrations

Write-Phase ('Migrations EF Core sur ' + $Database)

$env:RAQMI_CONNECTION_STRING = $connectionString

dotnet ef database update `
    --project src/RaqmiSystem.Infrastructure `
    --startup-project src/RaqmiSystem.Api 2>&1 | Select-Object -Last 5

if ($LASTEXITCODE -ne 0) {
    throw "Les migrations ont echoue. La base '$Database' existe-t-elle et appartient-elle a '$PostgresUser' ?"
}

# ---------------------------------------------------------------------- 2. Seed securite

Write-Phase 'Seed de securite (permissions, roles, administrateur)'

# Les variables d'environnement priment sur appsettings : c'est ce qui permet de viser la base
# de demonstration sans modifier un seul fichier de configuration du depot.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:RAQMI_POSTGRES__HOST = $PostgresHost
$env:RAQMI_POSTGRES__PORT = $PostgresPort
$env:RAQMI_POSTGRES__DATABASE = $Database
$env:RAQMI_POSTGRES__USER = $PostgresUser
$env:RAQMI_POSTGRES__PASSWORD = $PostgresPassword
$env:RAQMI_INITIAL_ADMIN_EMAIL = $AdminEmail
$env:RAQMI_INITIAL_ADMIN_PASSWORD = $AdminPassword

# Sauvegarde a la demande : sans ces deux variables le module 28 reste consultable mais ne
# peut rien declencher, et la capture montrerait un ecran sans historique.
$env:BACKUP_DIR = Join-Path $repositoryRoot 'artifacts/demo-backups'
$env:RAQMI_PG_BIN = 'C:\Program Files\PostgreSQL\18\bin'
New-Item -ItemType Directory -Force -Path $env:BACKUP_DIR | Out-Null

$apiExecutable = Join-Path $repositoryRoot 'src/RaqmiSystem.Api/bin/Release/net10.0/RaqmiSystem.Api.exe'
if (-not (Test-Path $apiExecutable)) { throw "API non compilee : lancez d'abord dotnet build -c Release." }

& $apiExecutable --seed-security | Out-Null

# ------------------------------------------------------------------- 3. Instance d'API

Write-Phase ('Demarrage de l''API de demonstration sur ' + $apiBaseUrl)

$env:ASPNETCORE_URLS = $apiBaseUrl
$api = Start-Process -FilePath $apiExecutable -PassThru -WindowStyle Hidden

try {
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $health = Invoke-RestMethod -Uri ($apiBaseUrl + '/health/database') -TimeoutSec 3
            if ($health.status -eq 'healthy') { $ready = $true; break }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $ready) { throw "L'API de demonstration n'a pas repondu sur $apiBaseUrl." }
    Write-Host ('   API prete (PID ' + $api.Id + ')') -ForegroundColor DarkGray

    # ------------------------------------------------------------ 4. Jeu de demonstration

    if (-not $SkipSeed) {
        Write-Phase 'Jeu de donnees de demonstration'

        & (Join-Path $PSScriptRoot 'demo-seed/seed-demo.ps1') `
            -ApiBaseUrl $apiBaseUrl `
            -AdminUser $AdminEmail `
            -AdminPassword $AdminPassword
    }

    # ---------------------------------------------------------------- 5. Captures d'ecran

    if (-not $SkipCapture) {
        Write-Phase 'Captures des ecrans du client WPF'

        $shots = Join-Path $repositoryRoot 'tools/RaqmiSystem.DocShots/bin/Release/net10.0-windows/RaqmiSystem.DocShots.exe'
        if (-not (Test-Path $shots)) { throw "Outil de capture non compile : dotnet build tools/RaqmiSystem.DocShots -c Release." }

        # Les identifiants memorises du poste sont mis a l'abri : la campagne se connecte avec
        # un compte de demonstration, elle n'a aucune raison de remplacer ceux de l'utilisateur.
        $settingsPath = Join-Path $env:APPDATA 'RaqmiSystem/desktop-settings.json'
        $settingsBackup = $settingsPath + '.before-guide'
        $hadSettings = Test-Path $settingsPath

        if ($hadSettings) { Copy-Item $settingsPath $settingsBackup -Force }

        try {
            & $shots `
                --api $apiBaseUrl `
                --user $AdminEmail `
                --password $AdminPassword `
                --out (Join-Path $repositoryRoot $OutputDirectory)

            if ($LASTEXITCODE -ne 0) { throw "La campagne de captures a echoue (code $LASTEXITCODE)." }
        }
        finally {
            if ($hadSettings) {
                Copy-Item $settingsBackup $settingsPath -Force
                Remove-Item $settingsBackup -Force
            }
            elseif (Test-Path $settingsPath) {
                Remove-Item $settingsPath -Force
            }
        }
    }
}
finally {
    Write-Phase 'Arret de l''API de demonstration'
    if ($null -ne $api -and -not $api.HasExited) {
        Stop-Process -Id $api.Id -Force
        Write-Host '   Arretee.' -ForegroundColor DarkGray
    }
}

Write-Host ''
Write-Host 'Chaine terminee.' -ForegroundColor Green
