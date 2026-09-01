# 5. Dépendances

## 5.1 Graphe observé — niveau Domain (références de namespace)

```text
Approvals      -> Identity (rôles décideurs)
Billing        -> Organization
Budgeting      -> Billing, Organization, Revenue
Closing        -> Organization
Crm            -> Billing (Customer), Organization
Housekeeping   -> Lodging (Room), Organization
HumanResources -> Organization
Inventory      -> Organization
Kpi            -> Accounting, Identity, Organization
Lodging        -> Billing (VAT), Organization
Mice           -> Billing, Lodging, Organization
Purchasing     -> Billing (VAT)
Receivables    -> Billing
Revenue        -> Organization
Settings       -> Billing (VAT)
Tariffs        -> Billing, Lodging, Organization
Treasury       -> Organization
```

Lecture : le Domain est **acyclique** ; `Organization` et `Billing` sont les deux nœuds partagés. `Billing`
l'est pour deux raisons distinctes — le client (légitime) et les taux de TVA (référentiel mal placé).

## 5.2 Graphe observé — niveau Infrastructure (services et requêtes EF)

```text
Billing        -> Organization, Settings
Budgeting      -> Organization, Revenue
Closing        -> Organization, Revenue
Crm            -> Billing, Lodging, Organization
Housekeeping   -> Lodging, Organization
HumanResources -> Organization
Inventory      -> Organization
Kitchen        -> Inventory (port IStockCostProvider)
Kpi            -> Accounting, Billing, Budgeting, Crm, Housekeeping, HumanResources, Inventory,
                  Lodging, Organization, Revenue, Treasury (lecture)
Lodging        -> Billing, Closing, Crm, Housekeeping, Mice, Organization, Tariffs
Mice           -> Billing, Lodging, Organization
Pilotage       -> Billing, Budgeting, Closing, Lodging, Organization, Revenue, Treasury (lecture)
Purchasing     -> Inventory (port IStockOperationService)
Receivables    -> Billing
Reporting      -> Billing, Lodging, Receivables, Revenue, Treasury (lecture)
Revenue        -> Closing (port IDailyClosingReadService), Organization
Tariffs        -> Billing, Organization
Treasury       -> Approvals (port IApprovalGate), Organization
```

Cycles à casser (tous introduits par des requêtes directes sur `RaqmiDbContext`) :

| Cycle | Cause probable | Port cible |
|---|---|---|
| Lodging ↔ Housekeeping | Lodging lit `RoomCondition` (chambre propre à l'arrivée) ; Housekeeping lit `Room`/séjours | `IRoomReadinessReader` (Housekeeping → Lodging en lecture) ; Lodging ne connaît que le contrat |
| Lodging ↔ Mice | Lodging lit `RoomAllotment` pour la disponibilité ; Mice crée des réservations sur bloc | `IAllotmentHoldProvider` fourni par Mice, consommé par `AvailabilityCalculator` |
| Lodging ↔ Crm | Lodging enrichit la réservation avec le profil ; Crm lit les séjours pour la 360 | `IGuestSnapshotReader` (Lodging → Crm) et `IStayHistoryReader` (Crm → Lodging) |
| Revenue ↔ Closing | Revenue refuse la saisie sur journée close ; Closing agrège les recettes | déjà porté par `IDailyClosingReadService` côté Revenue ; Closing → Revenue à porter |

Les agrégateurs en lecture (`Kpi`, `Pilotage`, `Reporting`) sont autorisés à lire plusieurs contextes ; ils
doivent le faire par **lecteurs** (`IXxxFactReader`) et non par requêtes EF ad hoc, pour que la migration
d'un schéma ne casse pas un dashboard.

## 5.3 Composition observée au niveau API

| Endpoint | Services composés | Règle observée |
|---|---|---|
| `POST /mice/events/{id}/invoice` | `IMiceService` + `IBillingService` | exige `mice.write` **et** `invoices.write` |
| `POST /purchasing/orders/{id}/receive` | `IPurchasingService` → `IStockOperationService` | réception = entrée de stock |
| `GET /kitchen/recipes/{code}/cost` | `IKitchenService` → `IStockCostProvider` | coût lu, jamais copié |
| `POST /treasury/payment-orders/{id}/approve` | `ITreasuryService` → `IApprovalGate` | approbation par le workflow |
| `POST /lodging/reservations` | `ILodgingService` → `ITariffResolutionService`, allotements | prix résolu par Tarifs, inventaire par PMS |
| `POST /lodging/reservations/{id}/check-out` | folios + factures | solde nul exigé sur tous les folios |

## 5.4 Flux cibles autorisés

```text
WPF -> API -> Application -> Domain
                     |          ^
                     v          |
               Infrastructure --+

Domaine métier  -> ports Application des services transversaux
Infrastructure  -> implémentations des ports
Integration Hub -> adaptateurs fournisseurs (jamais l'inverse)
```

Flux métier :

```text
PMS / POS / Facturation / Achats / Stocks / Paie / Immobilisations / Trésorerie
        -> Business Event (enveloppe, idempotency key, corrélation)
        -> Transactional Outbox (PostgreSQL, même transaction que l'état métier)
        -> Accounting Posting Engine
        -> Posting Rules (versionnées, par événement et par unité)
        -> Journal Entry (Σ D = Σ C, période ouverte, immuable après post)
        -> SCF

Tout domaine -> Approval Request -> Workflow -> décision -> callback vers le service propriétaire
Tout domaine -> Notification Request -> Notification Service -> Mon Espace
Tout domaine -> BusinessObjectReference <- Messagerie / GED (référence, jamais copie)
```

## 5.5 Matrice cible des dépendances entre domaines

Colonne = peut dépendre de (par contrat Application uniquement).

| Domaine | Dépendances autorisées |
|---|---|
| 02 Socle | — (fondation) |
| 03 Finance | 02, 04 (client), 12 (fournisseur) par référence ; reçoit des événements de tous |
| 04 CRM | 02, 06 (séjours, lecture), 05 (factures, lecture) |
| 05 Facturation | 02, 04, 03 (événements sortants) |
| 06 PMS | 02, 04, 05, 07 (tarifs/restrictions), 09 (allotements en lecture), 08 (état chambre en lecture) |
| 07 Revenue | 02, 06 (inventaire en lecture), 21 (canaux) |
| 08 Housekeeping | 02, 06 (chambres en lecture), 14 (incidents) |
| 09 MICE | 02, 04, 05, 06 |
| 10 F&B | 02, 06 (folio), 05, 11 (stock), 03 (événements) |
| 11 Stocks | 02, 03 (événements) |
| 12 Achats | 02, 11, 03, Workflow |
| 13 RH | 02, 03 (événements paie), Workflow |
| 14 Maintenance | 02, 06 (chambres), 11 (pièces), 12 (prestataires), 03 (immobilisations) |
| 15 Audit | 02, tous en lecture par référence |
| 16 Juridique | 02, 04, 12, 13, 17 |
| 17 GED | 02, tous par référence |
| 18, 19 | 02, 04, 05, 03, 21 |
| 20 Pilotage | tous en lecture par lecteurs de faits |
| 21 Intégrations | ports fournis par les domaines ; aucun domaine ne dépend d'un fournisseur |
| 22 Système | 02 |
| 01 Mon Espace | projections de Workflow, Notifications, Messagerie, Audit, KPI ; **aucune donnée propre** |

## 5.6 Ordre d'implémentation imposé par les dépendances

1. Catalogue fonctionnel hiérarchique (rien n'en dépend, tout le rendu en dépend).
2. Registre RBAC `domaine.ressource.action` + alias (préalable au filtrage de navigation et à Mon Espace).
3. Modèle d'affectation utilisateur ↔ unité/établissement (préalable au périmètre).
4. Extension du Workflow (sujets, délégation, échéance) — préalable à Mes validations et aux Achats.
5. Notifications (préalable à Mon Espace, aux alertes KPI, aux échéances).
6. Mon Espace (projections uniquement).
7. Événements métier + outbox + idempotence (préalable au Posting Engine).
8. Accounting Posting Engine + règles (Facturation, Trésorerie, PMS d'abord).
9. Facture fournisseur + 3-way matching (Achats → Stocks → Finance).
10. Messagerie + GED (références métier).
11. Domaines P1/P2/P3.
