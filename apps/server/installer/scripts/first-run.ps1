param(
  [string]$InstallRoot = "$env:ProgramFiles\Raqmi System Server",
  [string]$DataRoot = "$env:ProgramData\Raqmi System"
)

$ErrorActionPreference = 'Stop'
$pnpm = Get-Command pnpm -ErrorAction SilentlyContinue
if (-not $pnpm) {
  Write-Warning "pnpm absent — exécutez db:push et db:seed depuis une machine de build avant déploiement."
  exit 0
}

$env:DATABASE_URL = 'postgresql://raqmi:raqmi_password@127.0.0.1:5432/raqmi_system'
$repoRoot = Split-Path (Split-Path $InstallRoot -Parent) -Parent
if (Test-Path (Join-Path $repoRoot 'packages\database')) {
  Push-Location (Join-Path $repoRoot 'packages\database')
  pnpm db:generate
  pnpm exec prisma db push
  pnpm exec tsx src/seed.ts
  Pop-Location
}

Write-Host "Premier démarrage terminé."
