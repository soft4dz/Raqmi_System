# Déploiement serveur local (Windows installable)

## Objectif

Permettre à un client d'utiliser Raqmi System sur son propre réseau, avec PostgreSQL emplace, stockage fichiers centralisé et licence offline signée.

## Architecture

```text
Poste utilisateur 1 ─┐
Poste utilisateur 2 ─┼→ Raqmi System Server → PostgreSQL embarqué
Poste utilisateur 3 ─┘                    → %ProgramData%\Raqmi System\storage
                                           → Vérification licence (.license)
```

## Chemins Windows après installation

| Élément | Chemin |
|---------|--------|
| Binaires serveur | `%ProgramFiles%\Raqmi System Server\` |
| Données PostgreSQL | `%ProgramData%\Raqmi System\data\postgres` |
| Fichier licence | `%ProgramData%\Raqmi System\license.license` |
| Stockage fichiers | `%ProgramData%\Raqmi System\storage` |
| Clé publique éditeur | `%ProgramFiles%\Raqmi System Server\server\keys\public.jwk.json` |
| Config environnement | `%ProgramData%\Raqmi System\server.env` |

## Services Windows

| Service | Rôle |
|---------|------|
| `RaqmiPostgreSQL` | Base PostgreSQL embarquée |
| `RaqmiSystemServer` | API Raqmi (port 3000 par défaut) |

## Import de licence

### 1. Obtenir l'empreinte serveur

```http
GET http://127.0.0.1:3000/api/v1/license/fingerprint
```

Réponse : `{ "fingerprint": "abc123..." }`

Communiquez cette empreinte à l'éditeur pour lier la licence à ce serveur (optionnel).

### 2. Recevoir le fichier `.license`

L'éditeur génère le fichier via **Raqmi License Manager** (export signé Ed25519).

### 3. Importer (admin)

```http
POST http://127.0.0.1:3000/api/v1/license/import
Authorization: Bearer <token_admin>
Content-Type: application/json

{ "content": "<contenu JSON du fichier .license>" }
```

Alternative : raccourci **Raqmi Server Admin** (page locale `installer/admin/index.html`).

### 4. Vérifier le statut

```http
GET http://127.0.0.1:3000/api/v1/license/status
Authorization: Bearer <token>
```

## Variables d'environnement (`server.env`)

```env
PORT=3000
DATABASE_URL=postgresql://raqmi:raqmi_password@127.0.0.1:5432/raqmi_system
RAQMI_DATA_DIR=C:\ProgramData\Raqmi System
LICENSE_FILE_PATH=C:\ProgramData\Raqmi System\license.license
LICENSE_PUBLIC_KEY_PATH=C:\Program Files\Raqmi System Server\server\keys\public.jwk.json
LICENSE_MODE=offline
FILE_STORAGE_DRIVER=local
FILE_STORAGE_LOCAL_PATH=C:\ProgramData\Raqmi System\storage
JWT_SECRET=change-me-after-install
```

## Préparation installateur (éditeur / CI)

1. Placer PostgreSQL 16 Windows dans `apps/server/installer/assets/postgresql/`
2. (Optionnel) Node portable dans `apps/server/installer/assets/node/node.exe`
3. `pnpm keys:generate` — synchronise la clé publique dans le bundle serveur
4. `pnpm dist:server`

## Sécurité minimale

- Accès serveur limité au réseau client
- HTTPS interne recommandé en production
- Comptes nominatifs + rôles
- Contrôle licence côté API (`requireModule`)
- Sauvegardes PostgreSQL + stockage fichiers
