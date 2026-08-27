# Phase 6 bis — Livraison POS, sync pointeuse, compta stock SCF

**Date :** 24 juillet 2026  
**Migration :** `066_phase6_bis_pos_sync_compta.sql`  
**Dépendance :** `node-zklib` (sync ZKTeco TCP)

---

## Lots livrés

| Lot | Fonctionnalité | Route / déclencheur |
|-----|----------------|---------------------|
| 1 | **Vente POS → stock BOM** | `/cuisine` → onglet Ventes POS |
| 2 | **Sync pointeuse temps réel** | `/rh/temps/pointeuse` → Sync maintenant / Auto 5 min |
| 3 | **Écriture stock → SCF** | Auto sur chaque mouvement entree/sortie/perte |

---

## Chaîne vente POS

```
Vente POS → consommerStockRecette (BOM) → stock_mouvements
         → postComptaForMouvement → OD 601000 / 311000
         → événement POS_SALE_RECORDED + STOCK_COMPTA_POSTED
```

## Chaîne sync pointeuse

```
ZKTeco TCP :4370 → node-zklib getAttendances → raw_punches
Scheduler 5 min (sync_auto=1) → importPunches → traiterRawPunches (manuel)
```

## Écritures comptables SCF

| Mouvement | Journal | Débit | Crédit |
|-----------|---------|-------|--------|
| Sortie / perte / vente POS | OD | 601000 Achats consommés | 311000 Stocks |
| Entrée réception BC | AC | 311000 Stocks | 401000 Fournisseurs |
| Entrée autre | OD | 311000 | 601000 |

---

## Tests

- `phase6bis-pos.test.ts`
- `phase6bis-compta.test.ts`
- `phase6-cuisine.test.ts` (mis à jour)

---

## Configuration pointeuse

1. Enregistrer l'appareil avec **IP réseau**
2. Cocher **Auto 5 min** ou cliquer **Sync maintenant**
3. Mapper badges employés → **Générer pointages RH**
