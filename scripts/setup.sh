#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

step() { echo -e "\n==> $1"; }

step "Installation des dependances Node.js"
pnpm install

step "Generation du client Prisma"
pnpm db:generate

step "Creation des dossiers locaux"
mkdir -p storage/tenants license
if [ ! -f .env ] && [ -f .env.example ]; then
  cp .env.example .env
  echo "Fichier .env cree depuis .env.example"
fi

step "Demarrage de l'infrastructure (PostgreSQL + MinIO)"
docker compose -f docker/docker-compose.local.yml up -d

step "Attente de PostgreSQL"
for i in $(seq 1 30); do
  if docker exec raqmi-postgres pg_isready -U raqmi -d raqmi_system >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

step "Initialisation de la base de donnees"
pnpm db:setup

step "Verification des tests"
pnpm test

echo ""
echo "Environnement Raqmi System pret."
echo "Lancer: pnpm dev"
