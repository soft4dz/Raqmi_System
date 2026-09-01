# 2. Cartographie actuelle

## 2.1 Vue en couches

```text
RaqmiSystem.Desktop (WPF)  --HTTP/JSON-->  RaqmiSystem.Api (Minimal APIs, JWT, politiques)
                                                  |
                                                  v
                                      RaqmiSystem.Application (contrats, DTO, ports)
                                                  |
                                                  v
                                       RaqmiSystem.Domain (entités, invariants)
                                                  ^
                                                  |
                     RaqmiSystem.Infrastructure (EF Core / Npgsql, services, sécurité)
                                                  |
                                                  v
                                 PostgreSQL (1 base, 19 schémas, 105 tables)
```

Le dépôt est un **monolithe modulaire** : un seul `RaqmiDbContext`, un seul hôte API, un seul client. Les
contextes sont séparés par namespace et par schéma PostgreSQL, pas par projet.

## 2.2 Matrice contexte × couche

Légende : ✔ présent · ◐ partiel · — absent · (n) nombre de fichiers.

| Contexte | Domain | Application | Infrastructure | API (préfixe) | Schéma | Permissions | Écran WPF (onglet) | Tests | Doc module |
|---|---|---|---|---|---|---|---|---|---|
| Identity / Security | ✔ (9) | ✔ (20+10) | ✔ | `/security`, `/users`, `/account`, `/auth`, `/me` | security | `users.*`, `roles.*`, `security.seed` | UsersView (10) | ✔ 12 fichiers | security.md |
| Audit | ✔ (1) | ✔ | ✔ | `/audit` | audit | `audit.read` | inline (4) | ✔ | — |
| Organization | ✔ (2) | ✔ (4) | ✔ | `/organization/hotel-units` | organization | `units.*` | inline (1) | ✔ | unites-hotelieres.md |
| Settings | ✔ (1) | ✔ (3) | ✔ | `/settings` | settings | `settings.*` | SettingsView (9) | ✔ | — |
| Revenue | ✔ (2) | ✔ (12) | ✔ | `/revenue/daily` | exploitation | `revenue.*`, `dashboard.read` | inline (2, 3) | ✔ | recettes-journalieres.md |
| Closing | ✔ (2) | ✔ (5) | ✔ | `/closing/daily` | exploitation | `closing.*` | ClosingView (5) | ✔ | — |
| Treasury | ✔ (6) | ✔ (12) | ✔ | `/treasury/*` | finance | `treasury.*` | TreasuryView (6) | ✔ | — |
| Accounting | ✔ (9) | ✔ (18) | ✔ | `/accounting/*` | accounting | `accounting.*` (7) | AccountingView (11) | ✔ 4 fichiers | comptabilite-scf.md |
| Budgeting | ✔ (4) | ✔ (13) | ✔ | `/budget/*` | budgeting | `budget.*` | BudgetView (12) | ✔ | — |
| Billing | ✔ (5) | ✔ (10) | ✔ | `/billing/*` | finance | `customers.*`, `invoices.*` | CustomersView (7), InvoicesView (8) | ✔ | — |
| Receivables | ✔ (5) | ✔ (7) | ✔ | `/receivables/*` | finance | `receivables.*` | ReceivablesView (13) | ✔ | — |
| Tariffs | ✔ (4) | ✔ (12) | ✔ | `/tariffs/*` | tariffs | `tariffs.*` | TariffsView (14) | ✔ | — |
| Lodging | ✔ (39) | ✔ (84) | ✔ (37) | `/lodging/*` (96 routes) | lodging | `lodging.*` (14) | LodgingView (15), PmsView (30) | ✔ 12 fichiers | pms-hebergement.md |
| Channels | — | ✔ (11) | ◐ registre vide | — | — | — | — | — | (dans pms-hebergement.md) |
| Housekeeping | ✔ (7) | ✔ (21) | ✔ | `/housekeeping/*` | housekeeping | `housekeeping.*` | HousekeepingView (21) | ✔ | — |
| Crm | ✔ (15) | ✔ (29) | ✔ | `/crm/*` | crm | `crm.*` | CrmView (23) | ✔ | crm-experience-client.md |
| Mice | ✔ (8) | ✔ (19) | ✔ | `/mice/*` | lodging | `mice.*` | MiceView (28) | ✔ | — |
| Inventory | ✔ (8) | ✔ (22) | ✔ | `/inventory/*` | inventory | `inventory.*` | InventoryView (24) | ✔ 4 fichiers | — |
| Purchasing | ✔ (5) | ✔ (11) | ✔ | `/purchasing/*` | purchasing | `purchasing.*` | PurchasingView (25) | ✔ | — |
| Kitchen | ✔ (5) | ✔ (13) | ✔ | `/kitchen/*` | kitchen | `kitchen.*` | KitchenView (26) | ✔ | — |
| HumanResources | ✔ (26) | ✔ (31) | ✔ (13) | `/hr/*` | hr | `hr.*` (4) | HumanResourcesView (22) | ✔ | ressources-humaines.md |
| Approvals | ✔ (7) | ✔ (12) | ✔ | `/approvals/*` | approvals | `approvals.*` | ApprovalsView (16) | ✔ | — |
| Kpi | ✔ (24) | ✔ (58) | ✔ | `/kpis/*` | kpi | `dashboard.read` + clés sources, `kpi.admin` | KpiView (29) | ✔ 9 fichiers | bibliotheque-kpi.md |
| Pilotage | — | ✔ (27) | ✔ | `/pilotage/*` | — | `dashboard.read` | GroupDashboardView (19), DecCockpitView (20) | ✔ | — |
| Reporting | ✔ (5) | ✔ (7) | ✔ | `/reporting/*` | reporting | `reports.*` | ReportsView (17) | ✔ | — |
| Maintenance (backup) | — | ✔ (6) | ✔ | `/maintenance/backups` | — | `maintenance.*` | BackupView (18) | ✔ | deployment*.md |
| Sync | ✔ (3) | ✔ (8) | ✔ | `/sync/*` | security, audit | `sync.read` | SyncView (27) | ✔ | (modules-catalog.md §29) |
| Navigation (lot 0) | — | ✔ (1) | — | — | — | — | accueil, barre latérale | ✔ (5 tests) | architecture-fonctionnelle-cible.md |

## 2.3 Catalogue WPF actuel (11 groupes → 50 entrées)

| Groupe actuel | Entrées | Disponibles | Planifiées |
|---|---|---:|---:|
| Socle | 1, 2, 3 | 3 | 0 |
| Finance | 4, 4.5, 5, 5.2, 5.4, 6, 8, 9, 9.2 | 8 | 1 (5.4) |
| Exploitation | 10, 10.1, 10.2, 10.4, 10.6, 11, 11.5, 11.6, 12, 12.5, 13, 13.5, 14.5, 18 | 9 | 5 (11.6, 12.5, 13, 13.5, 18) |
| Juridique & commercial | 20, 20.2 | 0 | 2 |
| Ressources humaines | 21, 21.2 | 1 | 1 (21.2) |
| Contrôle | 22, 22.2, 22.4, 22.6, 22.8 | 2 | 3 |
| Conformité & légal | 23, 23.2, 23.4, 23.6 | 0 | 4 |
| Pilotage | 24, 24.2, 24.4, 25, 25.2, 25.4 | 5 | 1 (25.2) |
| Spécifique | 26 | 0 | 1 |
| Système documentaire | 27 | 0 | 1 |
| Système | 28, 29, 30 | 3 | 0 |
| **Total** | **50** | **31** | **19** |

Ce catalogue est **plat** : un groupe → des cartes → un onglet. Il n'existe ni niveau Module, ni niveau
Sous-module, ni filtrage par unité, établissement ou licence. Le filtrage se limite à : statut, priorité,
recherche texte et (lot 0) domaine cible.

## 2.4 Services transversaux existants

| Service | Réalisation actuelle | Consommateurs | Limite |
|---|---|---|---|
| Sécurité / RBAC | JWT + claims `permission`, politiques générées, alias PMS | toutes les routes | pas de périmètre, pas de délégation |
| Audit | `AuditLog` + `IAuditLogWriter`, `AuditableEntity` (créé/modifié par) | services sensibles | pas de vue « mon activité » |
| Workflow | `Approvals` : circuits, étapes par rôle, instances figées, `IApprovalGate` | Treasury (ordre de paiement) | un seul sujet, pas de retour/délégation/escalade/échéance |
| Paramétrage | `ApplicationSettings` | Billing (émetteur), Desktop | pas de paramètres par unité, pas de numérotation centrale |
| Résolution tarifaire | `ITariffResolutionService` | Lodging | — |
| Opérations de stock | `IStockOperationService`, `IStockCostProvider` | Purchasing, Kitchen | — |
| KPI Engine | `KpiEngine`, `KpiFactLoader` (lecture multi-contextes), snapshots | Kpi, dashboards | pas d'alerte poussée |
| Reporting | catalogue, exécution CSV, journal | ReportsView | pas de planification |
| Distribution | `IChannelManagerProvider`, `IChannelManagerRegistry` | aucun | aucun connecteur |
| Supervision postes | heartbeat, dérive de version, erreurs clients | SyncView | pas de journal d'interfaces |
| Sauvegarde | `IBackupService`, `BackupPolicy` | BackupView | restauration manuelle |

## 2.5 Sources de vérité de fait

| Donnée | Propriétaire actuel | Preuve dans le code |
|---|---|---|
| Inventaire hôtelier | Lodging (`AvailabilityCalculator`, `LodgingService.GetAllotmentHoldsAsync`) | Mice et Housekeeping lisent, ne recalculent pas |
| Client | Billing.Customer | Crm.GuestProfile 1-1 sur `CustomerCode` ; Tariffs, Mice, Lodging référencent le code client |
| Fournisseur | Purchasing.Supplier | — |
| Tiers comptable | Accounting.Party | **sans lien explicite** vers Customer/Supplier |
| Document de vente | Billing.Invoice | MICE et Lodging facturent via `IBillingService` |
| Écriture comptable | Accounting.JournalEntry | aucune génération automatique depuis un autre module |
| Quantité physique | Inventory.StockMovement | Purchasing et Kitchen passent par les ports |
| Salarié | HumanResources.Employee | — |
| Taux de TVA autorisés | Billing.InvoiceLine | Lodging.FolioCharge, Purchasing.Supplier, Settings l'appellent |
| Date métier hôtelière | Lodging.BusinessDay | night audit, Closing |
| KPI | Kpi (snapshots) | dashboards lisent le moteur |
