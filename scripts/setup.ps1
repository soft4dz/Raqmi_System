$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Write-Step($message) {
  Write-Host "`n==> $message" -ForegroundColor Cyan
}

function Test-Command($name) {
  return [bool](Get-Command $name -ErrorAction SilentlyContinue)
}

function Test-DockerDaemon {
  if (-not (Test-Command "docker")) { return $false }
  $previous = $ErrorActionPreference
  $ErrorActionPreference = "SilentlyContinue"
  & docker info 1>$null 2>$null
  $ok = $LASTEXITCODE -eq 0
  $ErrorActionPreference = $previous
  return $ok
}

Write-Step "Installation des dependances Node.js"
pnpm install --ignore-scripts

Write-Step "Generation du client Prisma"
pnpm db:generate

Write-Step "Creation des dossiers locaux"
$dirs = @("storage", "storage/tenants", "license")
foreach ($dir in $dirs) {
  if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir | Out-Null
  }
}

if (-not (Test-Path ".env") -and (Test-Path ".env.example")) {
  Copy-Item ".env.example" ".env"
  Write-Host "Fichier .env cree depuis .env.example"
}

Write-Step "Verification de Docker"
if (-not (Test-Command "docker")) {
  Write-Host "Docker CLI absent. Installation de Docker Desktop..." -ForegroundColor Yellow
  winget install -e --id Docker.DockerDesktop --accept-package-agreements --accept-source-agreements
  Write-Host ""
  Write-Host "1. Lancez Docker Desktop depuis le menu Demarrer" -ForegroundColor Yellow
  Write-Host "2. Attendez l'indicateur vert (Docker running)" -ForegroundColor Yellow
  Write-Host "3. Relancez: pnpm setup" -ForegroundColor Yellow
  exit 1
}

if (-not (Test-DockerDaemon)) {
  Write-Host "Docker est installe mais le moteur n'est pas demarre." -ForegroundColor Yellow
  Start-Process "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe" -ErrorAction SilentlyContinue
  Write-Host "Demarrage de Docker Desktop en cours..." -ForegroundColor Yellow

  $ready = $false
  for ($i = 0; $i -lt 36; $i++) {
    if (Test-DockerDaemon) {
      $ready = $true
      break
    }
    Start-Sleep -Seconds 5
    Write-Host "  Attente du moteur Docker... ($($i + 1)/36)"
  }

  if (-not $ready) {
    Write-Host ""
    Write-Host "Le moteur Docker n'est pas encore pret." -ForegroundColor Yellow
    Write-Host "Apres le premier lancement, Docker peut demander:" -ForegroundColor Yellow
    Write-Host "  - activation de WSL2" -ForegroundColor Yellow
    Write-Host "  - un redemarrage Windows" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Quand Docker Desktop affiche Running, relancez: pnpm setup" -ForegroundColor Yellow
    exit 1
  }
}

Write-Step "Demarrage de l'infrastructure (PostgreSQL + MinIO)"
docker compose -f docker/docker-compose.local.yml up -d

Write-Step "Attente de PostgreSQL"
$ready = $false
for ($i = 0; $i -lt 30; $i++) {
  docker exec raqmi-postgres pg_isready -U raqmi -d raqmi_system *> $null
  if ($LASTEXITCODE -eq 0) {
    $ready = $true
    break
  }
  Start-Sleep -Seconds 2
}

if (-not $ready) {
  Write-Host "PostgreSQL n'est pas pret. Consultez: pnpm infra:logs" -ForegroundColor Red
  exit 1
}

Write-Step "Initialisation de la base de donnees"
pnpm db:setup

Write-Step "Verification des tests"
pnpm test

Write-Host ""
Write-Host "Environnement Raqmi System pret." -ForegroundColor Green
Write-Host ""
Write-Host "Services:" -ForegroundColor Green
Write-Host "  Client web      http://localhost:5173"
Write-Host "  API serveur     http://localhost:3000"
Write-Host "  PostgreSQL      localhost:5432  (raqmi / raqmi_password)"
Write-Host "  MinIO API       http://localhost:9000"
Write-Host "  MinIO Console   http://localhost:9001  (raqmi / raqmi_password)"
Write-Host ""
Write-Host "Compte demo:" -ForegroundColor Green
Write-Host "  Email           admin@demo.raqmi.local"
Write-Host "  Mot de passe    demo1234"
Write-Host ""
Write-Host "Lancer l'application: pnpm dev" -ForegroundColor Green
