# Phase 6 — Livraison automatisation opérationnelle

**Date :** 24 juillet 2026  
**Migration :** `065_phase6_automatisation.sql`

---

## Résumé

| Lot | Contenu | Statut |
|-----|---------|--------|
| Lot 1 | Fiches techniques + ordres production + bus événements | ✅ Livré |
| Lot 2 | Pointeuses CSV → raw punches → pointages RH | ✅ Livré MVP |
| Lot 3 | Sync ZKTeco live + POS + compta stock SCF | ✅ Livré (Phase 6 bis) |

---

## Fichiers créés

| Fichier | Rôle |
|---------|------|
| `electron/database/migrations/065_phase6_automatisation.sql` | Schéma cuisine + pointeuses + événements |
| `electron/services/event-bus.service.ts` | Bus événements métier |
| `electron/services/cuisine-production.service.ts` | Fiches techniques → production → stock |
| `electron/services/rh-pointeuse.service.ts` | Import CSV pointeuse → pointages |
| `electron/ipc/cuisine.ipc.ts` | IPC cuisine |
| `src/pages/cuisine/CuisinePage.tsx` | UI production |
| `src/pages/rh/PointeuseTab.tsx` | UI pointeuses |
| `src/shared/types/cuisine.ts` | Types partagés |
| `docs/erp/phase6-automatisation-production-rh.md` | Spécification |

---

## Fichiers modifiés

- `electron/main.ts`, `electron/preload.ts`, `electron/ipc/rh.ipc.ts`
- `src/shared/types/ipc.ts`, `src/shared/types/rh.ts`, `src/lib/ipcClient.ts`
- `src/routes/AppRoutes.tsx`, `src/layouts/sidebarModules.ts`
- `src/pages/rh/rhNavigation.ts`, `RhHubContent.tsx`
- `src/modules/moduleCatalog.ts` (+2 modules)

---

## Routes

| Route | Module |
|-------|--------|
| `/cuisine` | Production & fiches techniques |
| `/rh/temps/pointeuse` | Pointeuses & badgeuses |

---

## Tests ajoutés

- `phase6-cuisine.test.ts` (validation recette, exécution ordre → stock)
- `phase6-pointeuse.test.ts` (parse CSV, génération pointages)

---

## Utilisation terrain

### Fiche technique

1. **Cuisine → Nouvelle fiche** — code, nom, prix vente
2. Ajouter ingrédients (produits stock existants)
3. **Valider** — coût matière et marge affichés
4. **Ordres de production** — planifier puis **Exécuter → stock**

### Pointeuse

1. **RH → Temps → Pointeuses** — enregistrer l'appareil
2. Mapper `pointeuse_badge_id` sur fiche employé
3. Coller export CSV → **Importer** → **Générer pointages RH**
4. Workflow N+1 + validation RH inchangés

---

## Complétude ERP estimée

**~85 % → ~88 %** (production restauration MVP + pointeuse CSV)
