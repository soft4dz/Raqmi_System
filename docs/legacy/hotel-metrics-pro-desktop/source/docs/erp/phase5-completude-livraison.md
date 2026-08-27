# Phase 5 — Complétude modules MVP (livraison)

**Application :** Raqmi System v0.8.0  
**Date :** juillet 2026  
**Statut :** livré (219 tests, `validate:certification` OK)

---

## Matrice maturité avant / après

| Module | Avant | Après | Livrables clés |
|--------|-------|-------|----------------|
| Achats | ~40 % | **~75 %** | `envoyerBon`, `livrerBon`, stock + TVA achats, UI fournisseurs |
| Stocks | ~45 % | **~70 %** | Entrées auto depuis réception BC |
| Maintenance | ~50 % | **~70 %** | Onglet équipements + lien intervention |
| Parking / Plage | ~50 % | **~70 %** | Onglets paramétrage |
| Commercial | ~35 % | **~65 %** | CRUD partenaires, lien opportunités |
| Alertes | ~30 % | **~70 %** | Backend `notifications`, cloche navbar, règles IPC |
| Décisions | ~55 % | **~75 %** | Destinataires création, lu/non-lu, filtre non lues |
| Créances | ~72 % | **~85 %** | Créance auto à validation, relances programmées |
| Hébergement | ~75 % | **~85 %** | Folios, fiche police check-in, taxe séjour check-out |
| Contrats | ~55 % | **~70 %** | `contrats_hotel` CRUD, route `/contrats`, alertes J-30 |
| Workflow | — | **intégré** | Seuils facture / BC dans `validerFacture` / `validerBon` |

**Global exploitation cible : ~85 %**

---

## Migrations SQL

| Fichier | Contenu |
|---------|---------|
| `062_phase5_achats_stocks_chain.sql` | Chaîne réception BC |
| `063_phase5_notifications.sql` | Notifications, règles, deliveries |
| `064_phase5_folio_contrats.sql` | Folios, contrats hôtel, seuils workflow |

---

## Tests ajoutés (`phase5-*.test.ts`)

| Fichier | Tests |
|---------|-------|
| `phase5-achats-stocks.test.ts` | 2 |
| `phase5-notifications.test.ts` | 2 |
| `phase5-contrats.test.ts` | 1 |
| `phase5-creances.test.ts` | 1 |
| `phase5-workflow.test.ts` | 1 |
| `phase5-decisions.test.ts` | 2 |
| `phase5-folio.test.ts` | 1 |
| `phase5-facturation-creance.test.ts` | 1 |
| `phase5-mvp-services.test.ts` | 4 |

**Total Phase 5 : 15 tests unitaires** (+212 tests existants → **227**)

---

## Scénarios de test manuel

1. **Achats → stock :** valider BC → envoyer → réception partielle → vérifier mouvement stock + ligne TVA achats  
2. **Créances :** valider facture impayée → créance auto ; activer relances auto → exécuter  
3. **Hébergement :** check-in réservation → fiche police + folio ; check-out → taxe séjour ; facturer folio  
4. **Workflow :** facture > 500 k DA ou client entreprise → workflow requis avant validation  
5. **Contrats :** créer convention → alerte J-30 sur liste  

---

## Écarts P2 (Phase 6 suggérée)

- UI folio détaillée sur fiche réservation (lignes extras interactives)
- WorkflowHistoryPanel sur pages facture/achats (composant existe)
- Channel manager, relevé bancaire, GMAO préventive
