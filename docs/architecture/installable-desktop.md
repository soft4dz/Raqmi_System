# Architecture installable desktop

## Décision

Raqmi System est livré comme logiciels Windows installables :

| Logiciel | Installateur | Qui l'installe |
|----------|--------------|----------------|
| **Raqmi System Server** | `RaqmiSystemServer-Setup.exe` | IT du client (1 par site) |
| **Raqmi System Client** | `RaqmiSystemClient-Setup.exe` | Chaque poste utilisateur |
| **Raqmi License Manager** | `RaqmiLicenseManager-Setup.exe` | Éditeur uniquement |

## Schéma

```text
Postes utilisateurs (Client NSIS)
        │
        ▼ HTTP (ex. http://192.168.1.10:3000)
Raqmi System Server (NSIS)
        ├── API Hono
        ├── PostgreSQL embarqué (%ProgramData%\Raqmi System\data\postgres)
        ├── Stockage fichiers (%ProgramData%\Raqmi System\storage)
        └── Fichier licence signé (%ProgramData%\Raqmi System\license.license)

Éditeur (License Manager NSIS)
        └── Génère fichier .license signé (Ed25519)
```

## Build des installateurs

Depuis la racine du monorepo :

```powershell
pnpm install
pnpm keys:generate          # paire Ed25519 éditeur (1 fois)
pnpm dist:server            # apps/server/installers/RaqmiSystemServer-Setup.exe
pnpm dist:client            # apps/client/installers/
pnpm dist:license-manager     # apps/license-manager/installers/
pnpm dist:all               # les trois
```

Prérequis serveur :

- Binaires PostgreSQL 16 dans `apps/server/installer/assets/postgresql/` (voir README du dossier)
- Node.js portable optionnel dans `apps/server/installer/assets/node/node.exe`
- NSIS (`makensis`) pour produire l'exe — sinon `pnpm --filter @raqmi/server dist:stage` prépare le staging

## Procédure client (résumé)

1. Installer **Raqmi System Server** sur le PC/serveur du réseau local
2. Ouvrir **Raqmi Server Admin** (raccourci bureau) → copier l'empreinte serveur
3. L'éditeur crée la licence dans **License Manager** et exporte le fichier `.license`
4. Importer la licence (admin API ou page Raqmi Server Admin)
5. Sur chaque poste : installer **Raqmi System Client** et configurer l'URL du serveur

## Configuration client desktop

Fichier `%APPDATA%\Raqmi System Client\config.json` :

```json
{ "serverUrl": "http://192.168.1.10:3000" }
```

Configurable au premier lancement via l'écran Paramètres.

## Pourquoi pas application web pure

Un ERP vendu aux clients doit pouvoir être installé, contrôlé et configuré sur le réseau local. L'interface React est emballée dans Electron pour produire un logiciel installable avec raccourcis bureau.
