# Phase 6 — Automatisation opérationnelle (production cuisine + pointeuses RH)

**Application :** Raqmi System v0.8.0+  
**Prérequis :** Phase 5 livrée (`065_phase6_automatisation.sql` appliquée)  
**Objectif :** Transformer les actions métier en **événements automatiques** — fiche technique validée → coût + stock ; pointeuse → pointage RH.

---

## Vision métier

| Action utilisateur | Avant | Après Phase 6 |
|-------------------|-------|----------------|
| Chef crée fiche technique | Document isolé | Nomenclature (BOM) + coût matière PMP |
| Directeur valide fiche | — | Événement `RECIPE_VALIDATED`, marge calculée |
| Planification production | — | Ordre de production → sorties stock auto |
| Export pointeuse CSV | Saisie manuelle pointages | `raw_punches` → pointages brouillon → workflow N+1 inchangé |

---

## Architecture technique

```
UI React → preload → IPC → service métier → SQLite → erp_evenements (bus)
```

**Bus d'événements :** `electron/services/event-bus.service.ts`  
Types : `RECIPE_VALIDATED`, `PRODUCTION_EXECUTED`, `POINTEUSE_IMPORTED`, `POINTAGES_GENERATED`

---

## Lot 1 — Production & fiches techniques (livré)

### Migration `065_phase6_automatisation.sql`

- `cuisine_recettes`, `cuisine_recette_lignes`, `cuisine_ordres_production`
- `erp_evenements`
- `stock_mouvements.source_type`, `source_id` (extension)

### Service `cuisine-production.service.ts`

- CRUD fiches, lignes nomenclature
- `validerRecette` → coût revient + marge + événement
- `createOrdreProduction` / `executerOrdreProduction` → `stocks.createMouvement` (sortie)

### UI

- Route `/cuisine` — `CuisinePage.tsx`
- Onglets : Fiches techniques | Ordres de production

### IPC

`cuisine:recettes:*`, `cuisine:ordres:*`

---

## Lot 2 — Pointeuses RH (livré MVP)

### Tables

- `rh_pointeuses`, `rh_raw_punches`
- `rh_employes.pointeuse_badge_id`
- `rh_pointages.source` (`manuel` | `pointeuse`)

### Service `rh-pointeuse.service.ts`

- Enregistrement appareils (ZKTeco, Hikvision…)
- `parseCsvPunches` + `importPunches` (dédoublonnage hash SHA-256)
- `traiterRawPunches` → paires entrée/sortie → `rh_pointages` brouillon

### UI

- `/rh/temps/pointeuse` — `PointeuseTab.tsx` (RH manage only)

### Conformité Algérie

- **Loi 90-11** : heures sup calculées en paie (inchangé)
- **ANPDP** : pas de stockage biométrique dans Raqmi — badge + timestamp uniquement
- **Audit** : corrections manuelles tracées via `writeAuditLog`

---

## Lot 3 — Phase 6 bis (livré)

| Fonctionnalité | Statut |
|----------------|--------|
| Sync ZKTeco TCP 4370 (`node-zklib`, auto 5 min) | ✅ |
| Vente POS → décrémentation BOM (`cuisine-pos.service`) | ✅ |
| Écriture comptable variation stock SCF (`stocks-compta.service`) | ✅ |
| Alertes absence H+15 | 📋 Phase 7 |
| IoT température frigo | 📋 Phase 7 |

Voir `docs/erp/phase6-bis-livraison.md`.

---

## Tests

- `electron/services/phase6-cuisine.test.ts`
- `electron/services/phase6-pointeuse.test.ts`
- `electron/services/phase6bis-pos.test.ts`
- `electron/services/phase6bis-compta.test.ts`

---

## Prompt agent Phase 6 bis (copier-coller)

```markdown
Mission : Phase 6 bis — sync pointeuse temps réel + lien POS restauration

Prérequis : migration 065, tests Phase 6 OK.

1. Service `rh-pointeuse-sync.service.ts` — poll ZKTeco toutes les 5 min (offline)
2. Module POS ou saisie ventes restauration → `executerConsommationRecette(recetteId, qty)`
3. Ne pas stocker empreintes — badge_id seulement
```
