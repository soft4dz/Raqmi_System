# Phase 2 — Contrôle interne, hôtellerie légale & archivage

**Application :** Raqmi System v0.8.0
**Prérequis :** Phase 1 conformité légale validée (`docs/erp/phase1-conformite-legale.md`)

> Document canonique Phase 2. L’ancien fichier `phase2-controle-hotellerie.md` est conservé comme alias historique.

## Objectif

Piloter et contrôler l’ERP selon les obligations hôtelières algériennes : workflow transversal, clôture journalière, rapprochements, créances, cockpit DEC, dashboard PDG, organisation RH EGT, checklists, hôtellerie légale, archivage GED et santé système.

## Migrations

| Fichier | Contenu |
|---------|---------|
| `055_erp_10_axes_foundation.sql` | Socle 10 axes (workflow, clôtures, rapprochements, créances, cockpit, checklists, KPI PDG) |
| `058_phase2_controle_hotellerie.sql` | Fiche police, taxe séjour, rapports tourisme, GED légal, items checklist, lien clôture↔rapprochement |

## Services backend

| Service | Rôle |
|---------|------|
| `workflow.service.ts` | Circuit validation transversal |
| `daily-closure.service.ts` | Clôture journalière, verrouillage, alertes 09h30 |
| `finance-reconciliation.service.ts` | Rapprochement recettes / trésorerie |
| `creances.service.ts` | Créances globales et balance âgée |
| `dec-cockpit.service.ts` | Alertes et widgets DEC (appel auto `checkClosureDeadlineAlerts`) |
| `dashboard-pdg.service.ts` | KPI PDG + export CSV + **rapport mensuel Excel** |
| `rh-organisation-egt.service.ts` | Organigramme EGT et fiches de poste |
| `checklist.service.ts` | Exécution checklists contrôle |
| `hotel-legal.service.ts` | Fiche police, taxe séjour, tourisme |
| `ged-archivage.service.ts` | Archivage légal SHA-256, rétention 10 ans, protection suppression |
| `system-health.service.ts` | Diagnostic base, backup, sync, workflows, intégrité GED |

## IPC

- `workflow:*`, `cloture:*`, `reconciliation:*`, `creances:*`
- `dec:cockpit:get`, `dec:alerts:*`
- `dashboard:pdg:get`, `dashboard:pdg:exportCsv`, `dashboard:pdg:exportMensuel`
- `checklist:*`, `hotelLegal:*`
- `ged:legal:*`, `ged:delete` (refus si archive légale active)
- `systemHealth:get`, `systemHealth:gedIntegrity`
- `rh:egt:*`, `rh:fichesPoste:*`

## Pages UI

| Route | Page |
|-------|------|
| `/workflows` | File d’attente validations |
| `/recettes/cloture` | Clôture journalière |
| `/finance/rapprochements` | Rapprochements |
| `/creances` | Créances globales |
| `/dec/cockpit` | Cockpit DEC |
| `/dashboard/pdg` | Dashboard PDG (+ export rapport mensuel CA) |
| `/rh/organisation/egt` | Organisation EGT |
| `/rh/fiches-poste` | Fiches de poste |
| `/controle/checklists` | Checklists |
| `/hotel-legal` | Conformité hôtelière |
| `/ged/archivage-legal` | Archivage légal GED |
| `/settings/system-health` | Santé système |

## Fonctionnalités clés complétées

### Alerte clôture 09h30
- `checkClosureDeadlineAlerts()` appelée à chaque chargement du cockpit DEC
- Widget `retard_saisie_0930` actif uniquement **après 09h30** locale
- Création d’alertes DEC par unité non clôturée

### Archivage GED protégé
- `assertDocumentLegallyProtected()` bloque `ged:delete` si archive légale active
- Vérification batch intégrité depuis `/settings/system-health`

### Rapport mensuel PDG
- Export Excel multi-feuilles : synthèse CA, par unité, CA journalier
- Destiné au reporting Conseil d’Administration

## Intégrations Phase 1

- Clôture journalière verrouille recettes et encaissements (`isDateJournalLocked`)
- Rapprochement lié à la clôture via `reconciliation_id`
- Écart non justifié → anomalie + alerte DEC
- Créances depuis factures impayées
- Archivage GED 10 ans (compta, RH, hôtel, contrats)

## Tests

```powershell
npm test
```

Fichier : `electron/services/phase2-controle.test.ts`

## Limites résiduelles (Phase 3)

- Workflow non branché sur chaque validation facturation/achats individuelle
- Export PDF organigramme / fiches de poste (CSV disponible)
- Planificateur cron alertes 09h30 en arrière-plan (déclenchement actuel : ouverture cockpit)
