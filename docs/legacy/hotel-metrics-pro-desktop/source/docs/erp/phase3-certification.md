# Phase 3 — Certification, conformité avancée & industrialisation

**Application :** Raqmi System v0.8.0
**Prérequis :** Phases 1 et 2 validées

## Lot 1 — Conformité Loi 18-07 / ANPDP (livré)

**Base légale :** Loi n° 18-07 du 10 juin 2018 relative à la protection des personnes physiques dans le traitement des données à caractère personnel (Algérie), autorité ANPDP.

### Migration

`059_phase3_rgpd_loi1807.sql` :
- `rgpd_traitements` — registre des traitements (art. 30)
- `rgpd_consentements` — consentements avec preuve et retrait
- `rgpd_demandes_droits` — accès, rectification, suppression, opposition, portabilité (art. 7-12)
- `rgpd_incidents` — violations et notification ANPDP (art. 41)
- `rgpd_politique_conservation` — durées liées à `ged_retention_policies` (Phase 2)

Seed : 4 traitements types hôtel + 4 politiques de conservation alignées GED.

### Service

`electron/services/rgpd-anpdp.service.ts` :
- Dashboard conformité (KPI retards demandes, incidents critiques)
- CRUD traitements, consentements, demandes, incidents
- Échéance automatique J+30 sur demandes de droits
- Workflow transversal sur demandes et incidents graves/critiques
- Export CSV registre des traitements
- Audit log sur toutes les opérations sensibles

### IPC (validation stricte)

| Canal | Handlers |
|-------|----------|
| `rgpd:dashboard` | Synthèse |
| `rgpd:traitements:*` | list, upsert, exportCsv |
| `rgpd:consentements:*` | list, create, revoke |
| `rgpd:demandes:*` | list, create, update |
| `rgpd:incidents:*` | list, create, update |
| `rgpd:conservation:list` | Politiques + lien GED |

### UI

Route : `/conformite/donnees-personnelles/*` (admin système)

| Sous-route | Contenu |
|------------|---------|
| Hub | KPI conformité |
| traitements | Registre + export CSV |
| consentements | Saisie et liste |
| demandes | Exercice des droits + workflow |
| incidents | Violations + ANPDP |
| conservation | Tableau durées / GED |

Sidebar : **Exploitation → Données personnelles (18-07)**

### Tests

`electron/services/phase3-rgpd.test.ts`

## Lot 2 — Fiscalité avancée & SIFEC (livré)

**Base légale :** Code des impôts directs et indirects (Algérie), système SIFEC / télédéclaration DGI.

### Migration

`060_phase3_fiscalite_sifec.sql` :
- `sifec_config` — configuration connecteur (sandbox / production)
- `sifec_transmissions` — journal des envois factures électroniques
- `fiscalite_teledeclarations` — suivi télédéclarations TVA, liasse, retenue
- `fiscalite_g50_referentiel` — codes G50/G4/G29 étendus
- Colonnes SIFEC sur `factures_fiscales_metadata` (uid, date transmission, erreur)
- Colonnes traçabilité sur `registre_tva_achats` (source, lien bon de commande)

### Services

| Fichier | Rôle |
|---------|------|
| `sifec-connector.service.ts` | Dashboard, config, préparation payload/QR, transmission sandbox, lot, audit + workflow si échec |
| `fiscalite-avancee.service.ts` | TVA achats (CRUD, import bons validés), liasse G50 avancée, export G50 TVA, télédéclarations |

### IPC (validation stricte)

| Canal | Handlers |
|-------|----------|
| `sifec:*` | dashboard, config, test, factures (list/prepare/submit/batch), transmissions |
| `fiscalite:achats:*` | list, create, importBons, exportCsv |
| `fiscalite:liasse:genererAvancee` | Liasse G50/G4/G29 étendue |
| `fiscalite:teledecl:*` | exportTvaG50, list, marquerDeclaree |

### UI

Routes sous `/fiscalite/*` :

| Route | Contenu |
|-------|---------|
| `/fiscalite/tva-achats` | Registre TVA déductible + import achats |
| `/fiscalite/teledeclarations` | Export G50 et suivi dépôt DGI |
| `/fiscalite/sifec` | Hub connecteur SIFEC |
| `/fiscalite/sifec/factures` | Transmissions factures |
| `/fiscalite/sifec/config` | Paramètres sandbox / production |

Onglets ajoutés dans **Fiscalité DGI** + bouton « Liasse avancée » sur la page liasse.

### Tests

`electron/services/phase3-sifec.test.ts`

## Lot 3 — Modules légaux restants (livré)

**Base légale :** SCF (immobilisations classe 2), CASNOS (TNS Algérie), obligation d'inventaire physique annuel.

### Migration

`061_phase3_modules_legaux.sql` :
- Comptes SCF `281000`, `681000`
- `immobilisations` + `immobilisations_amortissements`
- `casnos_affilies` + `casnos_declarations`
- `inventaire_legal_sessions` + `inventaire_legal_lignes`
- Seed immobilisation hôtelière type

### Services

| Fichier | Rôle |
|---------|------|
| `immobilisations.service.ts` | Registre, plan amortissement linéaire, comptabilisation OD mensuelle, export CSV |
| `casnos.service.ts` | Affiliés TNS, calcul cotisations, déclarations, export CSV |
| `inventaire-legal.service.ts` | Session depuis stocks, écarts, clôture + workflow, export légal |

### IPC

Canal unifié `modulesLegaux:dashboard` + `immo:*`, `casnos:*`, `inventaireLegal:*` (validation stricte).

### UI

Route : `/conformite/modules-legaux/*` (admin système)

| Sous-route | Contenu |
|------------|---------|
| Hub | KPI immobilisations, CASNOS, inventaires |
| immobilisations | Registre, plan, comptabilisation mensuelle |
| casnos | Affiliés, déclarations période |
| inventaire | Sessions, écarts, clôture |

Sidebar : **Exploitation → Modules légaux**

### Tests

`electron/services/phase3-modules-legaux.test.ts`

## Lot 4 — Industrialisation (CI/CD, audit IPC)

### Objectif

Garantir en CI que les handlers IPC Phase 3 (mutations avec arguments utilisateur) passent par `assert*` avant d’atteindre les services.

### Fichiers

| Fichier | Rôle |
|---------|------|
| `electron/ipc/ipc-security-audit.ts` | Logique d’audit (canaux mutation, couverture) |
| `electron/ipc/ipc-security-audit.test.ts` | Test Vitest — Phase 3 à 100 % |
| `scripts/audit-ipc-security.mjs` | Script CI standalone |
| `electron/ipc/validation.ts` | `assertPeriodeMois()` et helpers existants |
| `electron/ipc/comptabilite.ipc.ts` | Validation stricte sur `comptabilite:ecritures:create` |
| `electron/ipc/facturation.ipc.ts` | Validation sur create, valider, paiements, etc. |
| `electron/ipc/fiscalite-dz.ipc.ts` | `assertPeriodeMois` sur generer / export / calculer |

### Règles audit

- Canaux mutation détectés via `:(create|update|valider|export|…)`
- Validation requise **uniquement** si le handler reçoit des args utilisateur (pas `(event)` seul)
- **CI bloquante :** fichiers Phase 3 (`rgpd`, `sifec`, `fiscalite-avancee`, `modules-legaux`) à **100 %**
- Handlers hors Phase 3 sans validation = **avertissement** (dette IPC), pas échec CI

### Commandes

```bash
npm run audit:ipc              # audit IPC (bloquant Phase 3)
npm run validate:certification # tests + audit + tsc
```

CI GitHub (`.github/workflows/ci.yml`) : `npm test` → `npm run audit:ipc` → `npm run build`.

## Lot 5 — Packaging production (livré)

### Objectif

Installateur Windows NSIS, gestion de licence offline et checklist certification release.

### Composants

| Fichier | Rôle |
|---------|------|
| `electron/services/license.service.ts` | Essai 30 j, activation clé RS-{edition}-{date}-{sig}, empreinte poste |
| `electron/ipc/license.ipc.ts` | `license:getStatus`, `activate`, `getMachineId`, `clear` |
| `scripts/generate-license-key.mjs` | Génération clés éditeur (`npm run generate:license`) |
| `scripts/validate-packaging.mjs` | Contrôles pré-release (migrations 059–061, EULA, builder) |
| `scripts/build-installer.mjs` | Build NSIS via electron-builder |
| `assets/LICENSE-EULA.txt` | Contrat affiché à l'installation |
| `.github/workflows/release.yml` | Build installateur sur tag `v*` |

### Format clé

```
RS-{STANDARD|PRO|ENTERPRISE}-{YYYYMMDD}-{SIG8}
```

Signature HMAC-SHA256 (secret `HMP_LICENSE_SECRET` au build release).

### Commandes

```bash
npm run validate:packaging     # contrôles packaging
npm run validate:certification # tests + audit IPC + packaging + tsc
npm run dist:installer         # installateur dans installers/
npm run generate:license PRO 2027-12-31
```

Release : pousser un tag `v0.8.0` déclenche le workflow release (artefact `.exe`).

### UI

**Paramètres généraux** → carte « Licence Raqmi System » (admin) : statut, identifiant poste, activation.

**Santé système** (`/settings/system-health`) : contrôle licence + migrations Phase 3 (≥ 061).

## Phase 3 — statut global

| Lot | Statut |
|-----|--------|
| 1 — Loi 18-07 / ANPDP | ✅ |
| 2 — Fiscalité avancée & SIFEC | ✅ |
| 3 — Modules légaux | ✅ |
| 4 — Industrialisation | ✅ |
| 5 — Packaging production | ✅ |
