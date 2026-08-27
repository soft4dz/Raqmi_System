# Phase 7 — Module POS restauration (flux unifié)

**Migration :** `067_pos_module.sql`  
**Route :** `/pos`

## Flux opérationnel (Night Audit)

```
1. /pos — Ouvrir session faction → tickets → encaisser
2. /pos — Clôturer faction (rapport Z)
3. /pos — Clôturer journée POS (par point de vente)
   → sync auto recettes_journalieres (RESTAURATION)
4. /recettes/cloture — Préremplir → soumettre → valider → clôturer hôtel
   (bloqué si POS non clôturé)
```

## Règles métier

| Règle | Détail |
|-------|--------|
| Vente unique | `/cuisine` = production uniquement ; ventes via `/pos` |
| CA restauration | Alimenté auto à la clôture journée POS |
| Saisie manuelle | Ligne `[POS auto]` non écrasée par saisie recettes |
| Clôture hôtel | Refusée si PDV actifs non clôturés |

## Tests

- `phase7-pos.test.ts`
- `pos-recettes-sync.test.ts`
- `phase6bis-pos.test.ts` (dépréciation cuisine POS)
