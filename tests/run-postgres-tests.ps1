<#
.SYNOPSIS
    Joue la suite de tests sur PostgreSQL reel (collection « Postgres ») sur un poste de
    developpement, avec le conteneur declare dans docker-compose.yml.

.DESCRIPTION
    La suite principale (dotnet test sans variable) tourne sur SQLite/InMemory et marque les tests
    PostgreSQL comme ignores. Ce script :
      1. demarre le service postgres du docker-compose.yml du depot (identifiants du compose) ;
      2. attend qu'il accepte les connexions ;
      3. definit RAQMI_TEST_POSTGRES (sauf si elle est deja definie : votre base, votre choix) ;
      4. lance dotnet test filtre sur Category=Postgres.

    Les tests creent leurs propres bases raqmi_test_* et les suppriment en sortie : la base de
    developpement raqmi_system du conteneur n'est jamais modifiee. Le conteneur est laisse en
    marche apres les tests (c'est la base de developpement) ; -StopContainer l'arrete sans
    supprimer son volume.

.PARAMETER Configuration
    Configuration de build passee a dotnet test (Release par defaut, comme la CI).

.PARAMETER NoBuild
    Passe --no-build a dotnet test quand le projet de tests vient d'etre compile.

.PARAMETER StopContainer
    Arrete le conteneur postgres a la fin (docker compose stop, jamais down -v : le volume de
    developpement est conserve).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-postgres-tests.ps1

.EXAMPLE
    $env:RAQMI_TEST_POSTGRES = "Host=db.local;Port=5432;Database=postgres;Username=ci;Password=..."
    powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-postgres-tests.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$NoBuild,
    [switch]$StopContainer
)

$ErrorActionPreference = "Stop"

# Le script vit dans tests/ ; le compose et le projet de tests se resolvent depuis la racine.
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$testProject = Join-Path $repositoryRoot "tests/RaqmiSystem.Tests/RaqmiSystem.Tests.csproj"

# Identifiants de docker-compose.yml. Ce sont des valeurs de developpement local ; la variable
# RAQMI_TEST_POSTGRES, si elle est deja definie, prime et permet de viser un autre serveur.
$composeUser = "raqmi"
$composePassword = "change-me"
$composeDatabase = "raqmi_system"
$composePort = 5432

$startedByScript = $false

Push-Location $repositoryRoot
try {
    if ([string]::IsNullOrWhiteSpace($env:RAQMI_TEST_POSTGRES)) {
        Write-Host "Demarrage de PostgreSQL (docker compose up -d postgres)..."
        docker compose up -d postgres
        if ($LASTEXITCODE -ne 0) {
            throw "docker compose up -d postgres a echoue (code $LASTEXITCODE). Docker est-il demarre ?"
        }
        $startedByScript = $true

        # pg_isready repond avant que le premier demarrage n'ait termine son initialisation ; on
        # attend donc une connexion REELLE au role et a la base du compose, pas seulement le port.
        Write-Host "Attente de la disponibilite de PostgreSQL..."
        $ready = $false
        for ($attempt = 1; $attempt -le 30; $attempt++) {
            docker compose exec -T postgres psql -U $composeUser -d $composeDatabase -c "SELECT 1" *> $null
            if ($LASTEXITCODE -eq 0) {
                $ready = $true
                break
            }
            Start-Sleep -Seconds 2
        }
        if (-not $ready) {
            throw "PostgreSQL n'accepte toujours pas de connexion apres 60 s (docker compose logs postgres pour le detail)."
        }

        $env:RAQMI_TEST_POSTGRES = "Host=localhost;Port=$composePort;Database=$composeDatabase;Username=$composeUser;Password=$composePassword"
        Write-Host "RAQMI_TEST_POSTGRES definie vers le conteneur du compose (localhost:$composePort, base $composeDatabase)."
    }
    else {
        Write-Host "RAQMI_TEST_POSTGRES est deja definie : le conteneur du compose n'est pas demarre, la chaine fournie est utilisee telle quelle."
    }

    $arguments = @(
        "test", $testProject,
        "--configuration", $Configuration,
        "--filter", "Category=Postgres",
        "--verbosity", "normal"
    )
    if ($NoBuild) {
        $arguments += "--no-build"
    }

    Write-Host "dotnet $($arguments -join ' ')"
    & dotnet @arguments
    $testExitCode = $LASTEXITCODE
}
finally {
    if ($StopContainer -and $startedByScript) {
        Write-Host "Arret du conteneur postgres (le volume de developpement est conserve)."
        docker compose stop postgres
    }
    Pop-Location
}

exit $testExitCode
