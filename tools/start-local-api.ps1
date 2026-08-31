<#
.SYNOPSIS
    Demarre l'API Raqmi System en local, pour le client WPF du poste.

.DESCRIPTION
    deploy/onpremise/start-api.ps1 est le point d'entree de la tache planifiee d'un serveur
    installe : il attend une arborescence d'installation et un fichier d'environnement protege.
    Ce script-ci ne sert qu'au poste de developpement : il lance le binaire compile du depot,
    contre la base de travail, sur le port que le client cherche par defaut.

    Le processus est detache : la fenetre PowerShell peut etre fermee, l'API continue. Pour
    l'arreter : Stop-Process -Name RaqmiSystem.Api

.EXAMPLE
    .\tools\start-local-api.ps1

.EXAMPLE
    .\tools\start-local-api.ps1 -Database raqmi_demo -Port 5190
#>
[CmdletBinding()]
param(
    [int]$Port = 5180,
    [string]$Database = 'raqmi_system',
    [string]$PostgresHost = 'localhost',
    [int]$PostgresPort = 5432,
    [string]$PostgresUser = 'raqmi',
    [string]$PostgresPassword = 'change-me',
    [switch]$Restart
)

$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$apiExecutable = Join-Path $repositoryRoot 'src\RaqmiSystem.Api\bin\Release\net10.0\RaqmiSystem.Api.exe'
$baseUrl = 'http://localhost:' + $Port

if (-not (Test-Path $apiExecutable)) {
    throw "API non compilee. Lancez : dotnet build RaqmiSystem.sln -c Release"
}

$running = Get-Process -Name 'RaqmiSystem.Api' -ErrorAction SilentlyContinue

if ($running -and -not $Restart) {
    Write-Host ('API deja en cours (PID ' + ($running.Id -join ', ') + '). Utilisez -Restart pour la relancer.') -ForegroundColor Yellow
    return
}

if ($running) {
    $running | Stop-Process -Force
    Write-Host ('Instance precedente arretee (PID ' + ($running.Id -join ', ') + ').') -ForegroundColor DarkGray
}

# La configuration passe par l'environnement : Program.cs lit les variables prefixees RAQMI_,
# ce qui evite de modifier appsettings pour viser une autre base ou un autre port.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = $baseUrl
$env:RAQMI_POSTGRES__HOST = $PostgresHost
$env:RAQMI_POSTGRES__PORT = $PostgresPort
$env:RAQMI_POSTGRES__DATABASE = $Database
$env:RAQMI_POSTGRES__USER = $PostgresUser
$env:RAQMI_POSTGRES__PASSWORD = $PostgresPassword

$process = Start-Process -FilePath $apiExecutable -PassThru -WindowStyle Hidden

for ($attempt = 0; $attempt -lt 30; $attempt++) {
    try {
        $health = Invoke-RestMethod -Uri ($baseUrl + '/health/database') -TimeoutSec 3

        if ($health.status -eq 'healthy') {
            Write-Host ''
            Write-Host ('API prete sur ' + $baseUrl) -ForegroundColor Green
            Write-Host ('  base : ' + $Database + ' sur ' + $PostgresHost + ':' + $PostgresPort) -ForegroundColor DarkGray
            Write-Host ('  PID  : ' + $process.Id) -ForegroundColor DarkGray
            return
        }
    }
    catch {
        Start-Sleep -Milliseconds 500
    }
}

throw "L'API n'a pas repondu sur $baseUrl. PostgreSQL est-il demarre, et la base '$Database' accessible ?"
