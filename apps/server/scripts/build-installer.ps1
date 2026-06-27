param(
  [switch]$SkipNsis
)

$ErrorActionPreference = 'Stop'
$serverRoot = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $serverRoot -Parent | Split-Path -Parent
$staging = Join-Path $serverRoot 'dist\server-bundle'
$installers = Join-Path $serverRoot 'installers'

Write-Host "=== Build bundle serveur ==="
Push-Location $serverRoot
pnpm exec node scripts/bundle.mjs
Pop-Location

Write-Host "=== Préparation staging installateur ==="
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'server\dist') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'server\keys') | Out-Null

Copy-Item (Join-Path $serverRoot 'dist\server.mjs') (Join-Path $staging 'server\dist\server.mjs')
Copy-Item (Join-Path $serverRoot 'installer\assets\public.jwk.json') (Join-Path $staging 'server\keys\public.jwk.json')

# Node.js portable (optionnel — placer node.exe dans installer/assets/node/)
$nodePortable = Join-Path $serverRoot 'installer\assets\node\node.exe'
if (Test-Path $nodePortable) {
  New-Item -ItemType Directory -Force -Path (Join-Path $staging 'node') | Out-Null
  Copy-Item $nodePortable (Join-Path $staging 'node\node.exe')
} else {
  Write-Warning "Node portable absent (installer/assets/node/node.exe). L'installateur utilisera le Node système."
}

# Prisma client + schéma
$dbPkg = Join-Path $repoRoot 'packages\database'
New-Item -ItemType Directory -Force -Path (Join-Path $staging 'packages\database') | Out-Null
Copy-Item (Join-Path $dbPkg 'prisma') (Join-Path $staging 'packages\database\prisma') -Recurse
if (Test-Path (Join-Path $repoRoot 'node_modules\.prisma')) {
  New-Item -ItemType Directory -Force -Path (Join-Path $staging 'node_modules') | Out-Null
  Copy-Item (Join-Path $repoRoot 'node_modules\.prisma') (Join-Path $staging 'node_modules\.prisma') -Recurse
  Copy-Item (Join-Path $repoRoot 'node_modules\@prisma') (Join-Path $staging 'node_modules\@prisma') -Recurse
}

New-Item -ItemType Directory -Force -Path $installers | Out-Null

if ($SkipNsis) {
  Write-Host "Staging prêt : $staging"
  exit 0
}

$makensis = Get-Command makensis -ErrorAction SilentlyContinue
if (-not $makensis) {
  Write-Warning "NSIS (makensis) introuvable. Installez NSIS ou exécutez avec -SkipNsis."
  Write-Host "Staging prêt : $staging"
  exit 0
}

Push-Location (Join-Path $serverRoot 'installer')
& makensis raqmi-server.nsi
Pop-Location

Write-Host "Installateur : $installers\RaqmiSystemServer-Setup.exe"
