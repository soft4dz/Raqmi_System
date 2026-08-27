# Phase 2 — Contrôle interne, hôtellerie légale & archivage

> **Document déplacé** — voir le document canonique : [`phase2-controle-hotellerie-archivage.md`](./phase2-controle-hotellerie-archivage.md)

**Application :** Raqmi System v0.8.0
**Prérequis :** Phase 1 conformité légale validée (`docs/erp/phase1-conformite-legale.md`)

## Objectif

Piloter et contrôler l’ERP selon les obligations hôtelières algériennes : workflow transversal, clôture journalière, rapprochements, créances, cockpit DEC, dashboard PDG, organisation RH EGT, checklists, hôtellerie légale et archivage GED.

## Migrations

| Fichier | Contenu |
|---------|---------|
| `055_erp_10_axes_foundation.sql` | Socle 10 axes (workflow, clôtures, rapprochements, créances, cockpit, checklists, KPI PDG) |
| `058_phase2_controle_hotellerie.sql` | Fiche police, taxe séjour, rapports tourisme, GED légal, items checklist, lien clôture↔rapprochement |

## Services backend

| Service | Rôle |
|---------|------|
| `workflow.service.ts` | Circuit validation transversal |
| `daily-closure.service.ts` | Clôture journalière par unité |
| `finance-reconciliation.service.ts` | Rapprochement recettes / trésorerie |
| `creances.service.ts` | Créances globales et balance âgée |
| `dec-cockpit.service.ts` | Alertes et widgets DEC |
| `dashboard-pdg.service.ts` | KPI consolidés PDG |
| `rh-organisation-egt.service.ts` | Organigramme EGT et fiches de poste |
| `checklist.service.ts` | Exécution checklists contrôle |
| `hotel-legal.service.ts` | Fiche police, taxe séjour, tourisme |
| `ged-archivage.service.ts` | Archivage légal SHA-256, rétention 10 ans |

## IPC (preload + handlers)

- `workflow:*` — create, submit, approve, reject, history, listPending
- `cloture:*` — create, prefill, submit, validateUnit, validateDec, reject, close, list
- `reconciliation:*` — create, prefill, justify, validate, list
- `creances:*` — list, fromFacture, relance, balanceAgee, updateStatut, paiement
- `dec:cockpit:get`, `dec:alerts:*`
- `dashboard:pdg:*`
- `checklist:*`
- `hotelLegal:*`
- `ged:retention:list`, `ged:legal:*`
- `rh:egt:*`, `rh:fichesPoste:*`

## Pages UI

| Route | Page |
|-------|------|
| `/workflows` | File d’attente validations |
| `/recettes/cloture` | Clôture journalière |
| `/finance/rapprochements` | Rapprochements |
| `/creances` | Créances globales |
| `/dec/cockpit` | Cockpit DEC |
| `/dashboard/pdg` | Dashboard PDG |
| `/rh/organisation/egt` | Organisation EGT |
| `/rh/fiches-poste` | Fiches de poste |
| `/controle/checklists` | Checklists |
| `/hotel-legal` | Conformité hôtelière |
| `/ged/archivage-legal` | Archivage légal GED |

## Intégrations Phase 1

- Clôture journalière **verrouille** recettes et encaissements du jour (`isDateJournalLocked`)
- Rapprochement lié à la clôture via `reconciliation_id`
- Écart non justifié → anomalie auto + alerte DEC
- Créances générables depuis factures impayées
- Archivage GED avec politiques 10 ans (compta, RH, hôtel, contrats)

## Tests

```powershell
npm test
```

Fichier : `electron/services/phase2-controle.test.ts`

## Limites MVP (hors scope immédiat)

- Brancher workflow sur chaque validation facturation/achats (hooks partiels via clôture/rapprochement)
- Export PDF organigramme / fiches de poste (CSV disponible)
- Planificateur automatique alertes 09h30 (hook `checkClosureDeadlineAlerts` prêt côté service)
