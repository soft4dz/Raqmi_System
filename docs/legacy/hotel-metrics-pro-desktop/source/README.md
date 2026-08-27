# Raqmi System

Application desktop **offline-first** de pilotage hôtelier multi-établissements (Algérie), avec module marina **PortMaster** et suite **RH algérienne** (paie DZ, conformité, registres légaux).

| | |
|---|---|
| **Version** | 0.8.0 |
| **Stack** | Electron 36 · React 18 · SQLite · TypeScript |
| **Cible** | Windows 10/11 (x64) |
| **Dépôt** | [github.com/soft4dz/Hotel_Metrics_Pro_Desktop](https://github.com/soft4dz/Hotel_Metrics_Pro_Desktop) |

---

## Sommaire

1. [Présentation](#présentation)
2. [Configuration système requise](#configuration-système-requise)
3. [Installation](#installation)
4. [Première utilisation](#première-utilisation)
5. [Navigation et interface](#navigation-et-interface)
6. [Modules métier](#modules-métier)
7. [Module RH](#module-rh)
8. [PortMaster](#portmaster)
9. [Sécurité et authentification](#sécurité-et-authentification)
10. [Données, stockage et sauvegarde](#données-stockage-et-sauvegarde)
11. [Synchronisation et API centrale](#synchronisation-et-api-centrale)
12. [Scripts npm](#scripts-npm)
13. [Tests et intégration continue](#tests-et-intégration-continue)
14. [Packaging et déploiement](#packaging-et-déploiement)
15. [Import base legacy](#import-base-legacy)
16. [Variables d'environnement (développement)](#variables-denvironnement-développement)
17. [Architecture](#architecture)
18. [Documentation complémentaire](#documentation-complémentaire)
19. [Dépannage](#dépannage)
20. [Feuille de route](#feuille-de-route)
21. [Licence](#licence)

---

## Présentation

Raqmi System centralise le pilotage d'un groupe hôtelier : recettes, trésorerie, facturation, hébergement (PMS), stocks, achats, qualité, RH et marina. L'application fonctionne **sans connexion Internet** : la base SQLite locale est la source de vérité.

**Points clés :**

- **Multi-hôtel** avec périmètre par rôle (RBAC granulaire)
- **~30 modules métier** activables individuellement
- **Offline-first** : saisie et consultation disponibles hors ligne
- **Sync optionnelle** vers une API centrale (clients, facturation, trésorerie)
- **Traçabilité** : journal d'audit, verrouillages métier, exports PDF/Excel
- **Contexte algérien** : wilayas, paie DZ, registres légaux RH

---

## Configuration système requise

### Poste utilisateur (application installée)

Configuration pour l'exploitation quotidienne via l'installateur Windows (NSIS x64).

| Critère | Minimal | Recommandé |
|---------|---------|------------|
| **Système** | Windows 10 64 bits (à jour) | Windows 11 64 bits |
| **Processeur** | 2 cœurs 64 bits (Core i3 / Ryzen 3) | 4 cœurs ou plus (i5 / Ryzen 5) |
| **Mémoire RAM** | 4 Go | 8 Go (16 Go si multitâche intensif) |
| **Disque** | 2 Go libres | 10–20 Go libres (base, GED, sauvegardes) |
| **Écran** | 1280 × 720 | 1920 × 1080 ou plus (UI responsive jusqu'au 4K) |
| **Graphique** | GPU intégré | GPU intégré (aucune carte dédiée requise) |
| **Réseau** | Non obligatoire | Connexion stable si sync API ou mises à jour |

> **Note :** avec 4 Go de RAM, l'application (Electron/Chromium) reste utilisable mais le poste sera lent. **8 Go** est le seuil confortable pour un usage bureau.

**Non pris en charge actuellement :** macOS, Linux desktop, Windows ARM.

### Poste développeur (build et `npm run dev`)

| Critère | Minimal | Recommandé |
|---------|---------|------------|
| **Node.js** | 20 LTS | 20 LTS ou 22 LTS |
| **RAM** | 8 Go | 16 Go |
| **Disque** | 5 Go libres | 15 Go+ (`node_modules`, builds) |
| **Outils** | — | Visual Studio Build Tools si `better-sqlite3` ne se précompile pas |

### Serveur central optionnel (`server/`)

Uniquement si vous déployez l'API NestJS + PostgreSQL pour la synchronisation multi-sites.

| Critère | Minimal | Recommandé |
|---------|---------|------------|
| **RAM** | 4 Go | 8 Go |
| **CPU** | 2 vCPU | 4 vCPU |
| **Disque** | 20 Go | 50 Go+ |
| **Base** | PostgreSQL 15+ | PostgreSQL 15+ sur Linux ou Windows Server |
| **Réseau** | LAN stable vers les postes | HTTPS, sauvegardes DB planifiées |

---

## Installation

### Utilisateur final (installateur)

1. Télécharger l'installateur depuis `release/` (après `npm run dist`) ou depuis votre canal de distribution interne.
2. Lancer l'installateur NSIS x64.
3. Ouvrir **Raqmi System** depuis le menu Démarrer ou le raccourci bureau.
4. Se connecter avec un compte fourni par l'administrateur (voir [Première utilisation](#première-utilisation)).

### Développeur (sources)

```bash
git clone https://github.com/soft4dz/Hotel_Metrics_Pro_Desktop.git
cd Hotel_Metrics_Pro_Desktop
npm install
npm run dev
```

**Windows rapide :** double-clic sur `dev.bat` ou `.\dev.ps1`

Au démarrage :

1. **Vite** sert l'interface sur `http://localhost:5173`
2. **Electron** ouvre la fenêtre applicative (`contextIsolation` + `sandbox`)

> Ne pas utiliser uniquement le navigateur : `window.electronAPI` n'existe que dans Electron.

**Dépendance native :** `npm install` exécute `postinstall` (prebuild `better-sqlite3` pour Electron 36). En cas d'échec :

```bash
npm run rebuild:native
```

Electron est fixé en **36.x** pour bénéficier des binaires précompilés sans compilation locale.

---

## Première utilisation

### Connexion par défaut (base neuve)

| E-mail | Mot de passe | Rôle |
|--------|--------------|------|
| `[REDACTED_LEGACY_ADMIN_EMAIL]` | `[REDACTED_LEGACY_PASSWORD]` | SUPERADMIN |

Ce compte est recréé/garanti au démarrage si absent (`electron/database/authBootstrap.ts`).

### Réinitialiser complètement la base

Fermer l'application, puis :

```bash
npm run reset:db
```

Options : `npm run reset:db -- --db "C:\chemin\hotel_metrics_local.db"`

Résultat : schéma à jour, **aucune donnée métier**, un seul compte SUPERADMIN avec le mot de passe ci-dessus.

### Jeu de données démo

```bash
npm run seed:demo          # données multi-modules (hôtels, PMS, facturation, stocks…)
npm run seed:demo -- --force   # réinitialise les données démo
npm run seed:pms           # chambres, tarifs, clients PMS uniquement
```

### Comptes et rôles disponibles

| Code rôle | Libellé | Usage typique |
|-----------|---------|---------------|
| `SUPERADMIN` | Super administrateur | Paramétrage global, sécurité, modules |
| `ADMIN_DEC` | Administrateur décisionnel | Accès total |
| `PDG` | PDG | Consultation consolidée |
| `DIRECTEUR_UNITE` | Directeur d'unité | Son hôtel |
| `CONTROLEUR_UNITE` | Contrôleur unité | Saisie recettes |
| `RESPONSABLE_PORT` | Responsable port | PortMaster |
| `COMPTABILITE` | Comptabilité | Facturation, encaissements |
| `AUDIT_INTERNE` | Audit interne | Journaux, contrôles |
| `RH_MANAGER` | Responsable RH | Paie, contrats, planning |
| `CHEF_DEPARTEMENT` | Chef de département | Validation équipe |
| `RECEPTIONNISTE` | Réceptionniste | Hébergement, self-service RH |
| `LECTURE_SEULE` | Lecture seule | Consultation sans modification |
| `DGA` | Directeur Général Adjoint | Coordination des directions fonctionnelles (Scénario 3) |
| `DIRECTEUR_UNITES_TOURISTIQUES` | Directeur des Unités touristiques | Pilotage transversal des 5 unités (Scénario 3) |
| `DIRECTEUR_QUALITE` | Directeur Qualité | Standards, écoute client, audits qualité |
| `DIRECTEUR_COMMERCIAL` | Directeur Commerce & Marketing | Stratégie commerciale, marketing, partenariats |
| `DIRECTEUR_MAINTENANCE` | Directeur Équipement & Maintenance | Planification maintenance, travaux, investissements |
| `DIRECTEUR_SI` | Directeur Informatique (DSI) | Schéma directeur IT, cybersécurité, support |
| `RESPONSABLE_SECURITE` | Responsable Sécurité | Sécurité des personnes et des biens |
| `RESPONSABLE_JURIDIQUE` | Responsable Juridique | Contrats, contentieux, conformité RGPD |
| `RESPONSABLE_ACHATS` | Responsable Achats | Appels d'offres, négociation, fournisseurs |
| `CONTROLEUR_GESTION` | Contrôleur de Gestion | Budget consolidé, écarts, tableaux de bord |

Les comptes utilisateurs se créent dans **Administration → Utilisateurs** (`/admin/users`).

---

## Navigation et interface

### Page d'accueil

Après connexion, l'application ouvre la page **Applications** (`/modules`) : grille de lancement style Odoo avec recherche et filtres par domaine.

### Barre de navigation

- **Navbar horizontale** responsive (mobile → 4K)
- Accès aux modules activés, tableau de bord, RH, PortMaster, paramètres
- Menu utilisateur : profil, déconnexion, indicateur de sync

### Activation des modules

**Paramètres → Modules activés** (`/settings/modules`, profil admin). Les routes métier vérifient l'activation avant d'autoriser l'accès (`RequireModuleEnabled`).

### Raccourcis utiles

| Route | Description |
|-------|-------------|
| `/modules` | Hub Applications |
| `/dashboard` | Tableaux de bord directionnels |
| `/rh` | Suite RH (redirection vers hub) |
| `/portmaster` | Hub PortMaster |
| `/settings` | Paramétrage global |
| `/system/sync` | Synchronisation multi-postes |
| `/settings/backup` | Sauvegarde et restauration |

---

## Modules métier

Catalogue complet : page `/modules` ou fichier `src/modules/moduleCatalog.ts` (30 modules).

| Domaine | Modules |
|---------|---------|
| **Socle** | Administration & utilisateurs, paramétrage global, unités hôtelières |
| **Pilotage** | Tableaux de bord, rapports automatiques, alertes, comparatif inter-unités |
| **Finance** | Recettes journalières, trésorerie, budget, facturation, créances, clients, tarifs |
| **Exploitation** | Hébergement/PMS, stocks, achats, maintenance, parking, plage, qualité |
| **RH** | RH & productivité (suite complète) |
| **Contrôle** | Audit, journal des anomalies, décisions & instructions |
| **Juridique & commercial** | Contrats & conventions, commercial & partenariats |
| **Spécifique** | PortMaster (marina) |
| **Système** | GED, sauvegarde, synchronisation, journalisation |

**Statuts dans le catalogue :**

- `operationnel` — module utilisable en production
- `socle` — structure prête, enrichissement en cours
- `a-developper` — planifié

---

## Module RH

Navigation par hubs : `/rh/:hub/:sub`

| Hub | Contenu |
|-----|---------|
| **Pilotage & Vision** | Dashboard, analyses IA, prévisions, comparatif, onboarding |
| **Collaborateurs** | Annuaire, wizard employé, fiche 360, contrats, organigramme, affectations |
| **Temps & Présence** | Planning, pointages, absences & congés |
| **Paie & légal DZ** | Pré-paie, bulletins PDF, registres légaux, conformité |
| **Talents** | Recrutement, formations |
| **Validations** | Workflow N+1 |
| **Mon espace** | Self-service employé |

**Données démo RH** (migration `033_rh_seed_demo.sql`) : employés `demo.chef@hotel.local`, `demo.amina@hotel.local`, `demo.omar@hotel.local`, `demo.rh@hotel.local` — sans compte applicatif dédié ; lier un utilisateur via l'administration.

**Test moteur paie algérienne :**

```bash
npm run test:rh-paie
```

---

## PortMaster

Hub Applications : `/portmaster` — sous-applications :

| Application | Route |
|-------------|-------|
| Tableau de bord | `/portmaster/dashboard` |
| Référentiel port | `/portmaster/referentiel` |
| Clients portuaires | `/portmaster/clients` |
| Bateaux & emplacements | `/portmaster/bateaux` |
| Contrats d'amarrage | `/portmaster/contrats` |
| Facturation portuaire | `/portmaster/factures` |
| Tarifs & grilles | `/portmaster/tarifs` |
| Validations | `/portmaster/validations` |
| Mouvements | `/portmaster/mouvements` |
| Recouvrement | `/portmaster/recouvrement` |

**Seed démo :** au premier démarrage sur base vide, des emplacements, bateaux et contrats de démonstration sont créés automatiquement.

**Compte port démo :** `port@raqmi.local` — créé uniquement si le seed PortMaster s'exécute ; le **mot de passe est généré aléatoirement** et affiché **une fois dans les logs** du processus principal (console / fichier log). Changer le mot de passe à la première connexion.

---

## Sécurité et authentification

- **Connexion obligatoire** en développement et en production (pas d'auto-identification)
- Mots de passe hashés **bcrypt** (cost 12)
- **Verrouillage** après 5 échecs de connexion (15 minutes)
- Connexions et actions sensibles **journalisées** (audit)
- **Changement de mot de passe obligatoire** configurable par compte (`must_change_password`)
- **Permissions granulaires** par module (`users.manage`, `recettes.validate`, `portmaster.full`, etc.)
- **Isolation Electron** : `contextIsolation`, preload contrôlé, pas d'accès Node depuis le renderer

Bonnes pratiques :

1. Changer le mot de passe admin immédiatement en production
2. Créer un compte par personne (pas de comptes partagés)
3. Activer uniquement les modules nécessaires par site
4. Planifier sauvegardes et tests de restauration mensuels

---

## Données, stockage et sauvegarde

### Emplacement de la base SQLite

```
%AppData%\hotel-metrics-pro-desktop\data\hotel_metrics_local.db
```

Alternative possible en déploiement : `C:\ProgramData\HotelMetricsPro\data\`

### Migrations

53 migrations SQL versionnées dans `electron/database/migrations/`. Appliquées automatiquement au démarrage.

### Sauvegarde

- Interface : **Paramètres → Sauvegarde** (`/settings/backup`)
- Procédure détaillée : `docs/PROCEDURE_SAUVEGARDE_RESTAURATION.md`

**Fréquence recommandée :**

- Sauvegarde automatique quotidienne
- Sauvegarde manuelle avant chaque mise à jour
- Archive mensuelle conservée ≥ 30 jours

**Nommage suggéré :** `hotel_metrics_YYYY-MM-DD_HH-mm_v0.8.0.db`

**Contenu minimal d'une sauvegarde :** base SQLite, fichiers GED locaux, paramètres applicatifs, version de l'app.

---

## Synchronisation et API centrale

L'application fonctionne seule. La sync est **optionnelle** pour répliquer certaines entités vers un serveur central.

| Composant | Rôle |
|-----------|------|
| `electron/services/sync.service.ts` | File `sync_queue`, retry automatique |
| `/system/sync` | Interface de pilotage sync |
| `server/` | API NestJS + PostgreSQL (clients, facturation, trésorerie) |
| `server/deploy/` | Déploiement PHP/cloud (sync légère) |

**Démarrer l'API locale :**

```bash
npm run server:install
npm run server:prisma:migrate
npm run server:seed
npm run server:dev    # port 3001 par défaut
```

Documentation serveur : `server/README.md` et `server/deploy/README-CLOUD.md`.

**Clé API sync :** variable `HMP_SYNC_API_KEY` (identique côté client et serveur).

---

## Scripts npm

| Commande | Description |
|----------|-------------|
| `npm run dev` | Développement Electron + Vite |
| `npm test` | Suite Vitest (routes, IPC, modules, UI) |
| `npm run test:watch` | Vitest en mode watch |
| `npm run test:smoke` | Smoke test des routes React |
| `npm run test:all` | Tests + paie RH + vérification phase 3 |
| `npm run build` | Compilation TypeScript + build Vite |
| `npm run preview` | Prévisualisation build Vite |
| `npm run dist` | Installateur Windows NSIS x64 → `release/` |
| `npm run rebuild:native` | Recompile `better-sqlite3` pour Electron |
| `npm run seed:demo` | Jeu de données démo complet |
| `npm run seed:pms` | Seed PMS (chambres, tarifs) |
| `npm run reset:db` | Réinitialisation base + SUPERADMIN seul |
| `npm run test:rh-paie` | Test moteur paie algérienne |
| `npm run verify:phase3` | Vérification intégrité phase 3 |
| `npm run import:legacy` | Import dump MySQL legacy (configurer le chemin) |
| `npm run server:dev` | API NestJS en développement |
| `npm run server:build` | Build API NestJS |
| `npm run server:start` | API NestJS en production |
| `npm run server:install` | `npm install` dans `server/` |
| `npm run server:prisma:migrate` | Migrations Prisma |
| `npm run server:seed` | Seed PostgreSQL |

---

## Tests et intégration continue

```bash
npm test                 # tous les tests unitaires / composants
npm run test:smoke       # routes applicatives
npm run build            # vérifie la compilation production
```

**CI GitHub Actions** (`.github/workflows/ci.yml`) sur chaque push/PR vers `main` :

- OS : `windows-latest`
- Node 20
- `npm ci` → `npm run rebuild:native` → `npm test` → `npm run build`

---

## Packaging et déploiement

```bash
npm run dist
```

**Sortie :** dossier `installers/` — installateur **NSIS x64** Windows (`Raqmi-System-{version}-Setup.exe`).

**Configuration :** `electron-builder.yml`

| Paramètre | Valeur |
|-----------|--------|
| `appId` | `dz.raqmisystem.desktop` |
| Architecture | x64 uniquement |
| Raccourcis | Bureau + menu Démarrer |
| Migrations SQL | embarquées dans `extraResources` |

Checklist production : `docs/STABILISATION_PRODUCTION.md`

---

## Import base legacy

Migration depuis un export MySQL / phpMyAdmin :

```bat
import.bat "C:\chemin\vers\dump.sql"
```

ou adapter la commande dans `package.json` (`import:legacy`).

> Fermer les saisies en cours et sauvegarder la base avant import.

---

## Variables d'environnement (développement)

| Variable | Description |
|----------|-------------|
| `HMP_DEV_PORT` | Port Vite (défaut `5173`) |
| `HMP_DEVTOOLS` | `1` pour ouvrir les DevTools Electron au démarrage |
| `HMP_DEBUG` | `1` pour logs détaillés |
| `HMP_SYNC_API_KEY` | Clé d'authentification sync (défaut dev à changer en prod) |
| `HMP_API_PORT` | Port API sync légère (`server/index.mjs`, défaut `3847`) |

Aucune variable n'est requise pour l'utilisation standard de l'installateur.

---

## Architecture

```
Hotel_Metrics_Pro_Desktop/
├── electron/                 # Process principal Electron
│   ├── main.ts               # Point d'entrée, fenêtre, migrations
│   ├── preload.ts            # Pont sécurisé renderer ↔ main
│   ├── ipc/                  # ~38 domaines IPC
│   ├── services/             # Logique métier (~73 services)
│   └── database/
│       ├── migrations/       # Schéma SQLite versionné
│       └── sqlite.ts         # Accès better-sqlite3
├── src/                      # Interface React
│   ├── routes/               # HashRouter, gardes auth/modules
│   ├── pages/                # Écrans par module
│   ├── components/           # UI, graphiques (Recharts), apps Odoo
│   ├── hooks/                # TanStack Query, modules activés
│   ├── stores/               # Zustand (auth, UI)
│   └── shared/               # Types IPC, permissions, constantes
├── server/                   # API NestJS + Prisma (optionnelle)
├── docs/                     # Guides utilisateurs et procédures
├── scripts/                  # Dev, seed, reset, tests
└── release/                  # Installateurs générés (gitignored)
```

**Flux de données :**

```
React (renderer)
    ↕ preload / electronAPI
IPC handlers (electron/ipc)
    ↕
Services métier (electron/services)
    ↕
SQLite (better-sqlite3)  ──optionnel──▶  API centrale / sync_queue
```

**Bibliothèques principales :** TanStack Query, Zustand, Radix UI, Tailwind CSS, Recharts, ExcelJS, pdf-lib.

---

## Documentation complémentaire

| Fichier | Contenu |
|---------|---------|
| `docs/guides-utilisateurs/` | 11 guides par profil (super-admin, PDG, RH, réception…) |
| `docs/STABILISATION_PRODUCTION.md` | Checklist mise en production |
| `docs/PROCEDURE_SAUVEGARDE_RESTAURATION.md` | Backup, restore, tests |
| `README_ADMIN_MODULE_V1.md` | Module administration |
| `server/README.md` | API NestJS + PostgreSQL |
| `server/deploy/README-CLOUD.md` | Sync cloud / hébergement |
| `ANALYSE_PROJET.md` | Analyse technique du dépôt |

---

## Dépannage

| Problème | Solution |
|----------|----------|
| `better-sqlite3` ne s'installe pas | `npm run rebuild:native` — installer Visual Studio Build Tools |
| Page blanche en navigateur seul | Lancer via `npm run dev` ou l'exe Electron, pas Vite seul |
| Connexion refusée / compte verrouillé | Attendre 15 min ou `npm run reset:db` (perte des données) |
| Mot de passe admin inconnu | `npm run reset:db` → `[REDACTED_LEGACY_PASSWORD]` |
| Port 5173 occupé | `set HMP_DEV_PORT=5174` puis relancer |
| Sync en échec (401) | Aligner `HMP_SYNC_API_KEY` client et serveur |
| Base corrompue | Restaurer depuis sauvegarde (`docs/PROCEDURE_SAUVEGARDE_RESTAURATION.md`) |
| Modules invisibles | Vérifier activation dans `/settings/modules` et permissions du rôle |

**Logs :** activer `HMP_DEBUG=1` pour le processus Electron.

---

## Feuille de route

| Phase | Statut | Contenu |
|-------|--------|---------|
| 1–5 | ✅ | Auth, admin, recettes, dashboards |
| 6–8 | ✅ | PortMaster, facturation, sync |
| 9 | 🔄 | Stabilisation prod, GED, documentation |
| 10 | ✅ | Installateur NSIS, gestion de licence offline |

---

## Licence

Usage privé / projet métier — voir votre contrat de licence.

---

**Raqmi System** — Pilotage hôtelier & marina · Algérie
