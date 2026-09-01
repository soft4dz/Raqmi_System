# 1. Audit de l'existant

Date : 1er septembre 2026. Branche : `feature/accounting-scf-core` (commit `0b9f841`, = `main` `ad467d9`
+ 8 commits de stabilisation + 2 commits comptabilité). Méthode : lecture exhaustive des arborescences,
des points d'entrée API, du catalogue de permissions, des migrations EF, du catalogue WPF, des tests et de la
documentation, puis vérification par exécution (build, tests, garde readiness).

## 1.1 Vérifications exécutées

| Vérification | Commande | Résultat |
|---|---|---|
| Compilation | `dotnet build RaqmiSystem.sln -c Release` | 0 erreur, 0 avertissement |
| Tests | `dotnet test tests/RaqmiSystem.Tests -c Release` | **914 réussis, 0 échec, 0 ignoré** (37 s) |
| Readiness | `tools/check-module-readiness.ps1` | **31/31** modules Disponibles câblés Navigation + RBAC + Desktop |
| État git | `git status` | 7 fichiers modifiés, 4 non suivis (lot 0, voir §1.11) |

## 1.2 Chiffres clés vérifiés

| Axe | Mesure |
|---|---|
| Projets | 5 projets `src/` + 1 projet de tests + 1 outil (`tools/RaqmiSystem.DocShots`) |
| Code | 1 125 fichiers `.cs` sous `src/` |
| Domain | 24 contextes bornés, ≈ 200 fichiers (entités, énumérations, calculateurs purs) |
| Application | 28 dossiers de contrats (dont `Channels`, `Maintenance`, `Pilotage`, `Security`, `Navigation` sans Domain propre) |
| Infrastructure | 30 dossiers, `RaqmiDbContext` avec 99 `DbSet`, 22 migrations EF Core |
| PostgreSQL | **105 tables** réparties sur **19 schémas** |
| API | 31 fichiers d'endpoints, ≈ 90 groupes de routes, **≈ 430 routes** sous `/api/v1`, toutes protégées par politique de permission sauf `/health` et `/auth/*` |
| RBAC | **83 clés de permission**, **7 rôles système**, politiques générées automatiquement depuis `PermissionCatalog.All` |
| Desktop | 30 vues WPF + 3 fenêtres, 31 onglets (Accueil + 30 écrans), `MainWindow` = 4 040 lignes (XAML + partiels) |
| Catalogue | 50 entrées : 31 `Disponible`, 19 `Planifié` (0 `ApiPrête`, 0 `Partiel`) |
| Tests | 111 fichiers, 816 `[Fact]` + 22 `[Theory]` (98 `[InlineData]`) = 914 cas |
| Documentation | 20 documents actifs à la racine `docs/`, 7 fiches `docs/modules/`, 1 matrice `docs/stabilization/`, 85 documents legacy importés |
| CI | `dotnet.yml` (build API + tests Linux, build WPF Windows, image GHCR sur tag) ; `stabilization.yml` (readiness + WPF + tests) |
| Exploitation | Dockerfile, docker-compose dev/prod, Caddy, scripts on-premise PowerShell, backup systemd, installeur Inno Setup |

## 1.3 Inventaire Domain (24 contextes)

| Contexte | Entités et règles principales | Observations |
|---|---|---|
| Accounting | `ChartAccount`, `AccountingJournal`, `JournalEntry`/`Line`, `EntryStatus`, `AccountClassCatalog` (SCF), `AccountingCoreModels` (exercices, périodes, tiers, lettrage) | partie double, immuabilité après `post`, contre-passation |
| Approvals | `ApprovalCircuit`/`Step`, `ApprovalInstance`/`InstanceStep`/`Decision`, `ApprovalSubjectType` | **un seul sujet : `PaymentOrder`** ; règle des rôles décideurs portée par le Domain |
| Audit | `AuditLog` | journal technique transversal |
| Billing | `Customer`, `CustomerType`, `Invoice`/`Line`/`Status` | référentiel client **et** référentiel des taux de TVA (`InvoiceLine.RequireAllowedVatRate`) |
| Budgeting | `BudgetPlan`/`Line`/`Category`/`Status` | dépend de Revenue et Billing |
| Closing | `DailyClosing`, `ClosingStatus` | clôture journalière d'unité |
| Crm | `GuestProfile` (étend `Customer` 1-1), `CustomerSegment`, `LoyaltyTier`/`Transaction`, `Campaign`, `SatisfactionEntry`, `GuestInteraction` | pas de prospect, pas de réclamation |
| Housekeeping | `RoomCondition`, `HousekeepingTask`, `MinibarItem`/`Consumption` | référence les chambres de Lodging |
| HumanResources | `Employee`, `EmploymentContract`, `Position`, `Department`, `TimeEntry`, `AbsenceRequest`, `PayrollPeriod`/`Payslip`/`Bonus`/`ParameterSet`/`TaxBracket`, `AlgerianPayrollEngine` | paie algérienne (IRG, CNAS) ; `Department` est RH, pas organisationnel |
| Identity | `User`, `Role`, `Permission`, `RolePermission`, `UserRole`, `RefreshToken`, `PermissionCatalog`, `RoleCatalog` | **aucune affectation utilisateur ↔ unité** |
| Inventory | `Warehouse`, `StockItem`/`Category`, `StockMovement`/`Kind`, `InventoryCount`/`Line` | PMP ; pas de lot, pas d'expiration, pas d'emplacement |
| Kitchen | `RecipeSheet`, `RecipeIngredient`, `RecipeCategory`, `TemperatureCheckpoint`/`Reading` | coût matière lu depuis Inventory |
| Kpi | `KpiCatalog` (1 727 lignes), `KpiDefinition`, `KpiSnapshot`, `KpiThreshold`, `KpiAccountMapping`, `KpiSourceModule`, `KpiMath` | moteur analytique ; chaque KPI porte son module source et sa permission |
| Lodging | 39 fichiers : `Room`, `RoomType`, `RoomBed`, `RoomBlock` (OOO/OOS), `Reservation`, `ReservationStatus`, `StayRoomAssignment`, `Folio`/`Charge`/`Kind`, `Deposit`, `ExtraItem`, `Package`, `CancellationPolicy`, `RateRestriction`, `YieldRule`, `OverbookingAllowance`, `NightAuditRun`, `BusinessDay`, `AvailabilityCalculator` | **source unique de l'inventaire hôtelier** (calcul pur partagé) |
| Mice | `FunctionSpace`, `EventBooking`/`Line`/`ScheduleItem`, `RoomAllotment` | tables physiquement dans le schéma `lodging` |
| Organization | `HotelUnit`, `HotelUnitType` | pas d'entreprise, établissement, direction, service, centre de coûts |
| Purchasing | `Supplier`, `SupplierType`, `PurchaseOrder`/`Line`/`Status` | pas de demande d'achat, ni facture fournisseur |
| Receivables | `AgingBucket`, `AgingCalculator`, `Reminder`/`Level`/`Channel` | calcul sur les factures de Billing |
| Reporting | `ReportCatalog`, `ReportDefinition`, `ReportExecution` | catalogue de rapports, export CSV |
| Revenue | `DailyRevenue`, `DailyRevenueStatus` | CA journalier déclaratif (brouillon → soumis → validé/rejeté) |
| Settings | `ApplicationSettings` | identité de l'établissement émetteur |
| Sync | `Workstation`, `WorkstationFailure` | registre des postes, aucune file de synchronisation |
| Tariffs | `RatePlan`, `RatePeriod`, `CustomerConvention`, `BoardType` | résolution tarifaire consommée par Lodging |
| Treasury | `BankAccount`, `CashReceipt`, `PaymentOrder`, `PaymentMethod` | ordre de paiement soumis au workflow d'approbation |

## 1.4 Inventaire Application (contrats)

Un service d'orchestration par contexte (`IXxxService`) et des contrats de port entre contextes :
`IApprovalGate` (Treasury → Approvals), `IStockOperationService` et `IStockCostProvider`
(Purchasing/Kitchen → Inventory), `ITariffResolutionService` (Lodging → Tariffs), `IDailyClosingReadService`
(Revenue → Closing), `IChannelManagerProvider` + `IChannelManagerRegistry` (frontière distribution, **aucune
implémentation**), `IAuditLogWriter`, `ITokenService`, `ISecuritySeeder`, `IBackupService`.
`Pilotage` (`GroupDashboardCalculator`, `DecCockpitCalculator`) et `Kpi` (`KpiEngine`, cinq calculateurs)
sont des agrégateurs en lecture. Résultat commun : `ApplicationResult<T>` (`NotFound`, `Conflict`,
`Validation`), `PagedResult<T>`.

## 1.5 Inventaire API

Toutes les routes sont sous `/api/v1` ; chaque route porte `RequireAuthorization("<clé>")`. Les politiques
sont créées en boucle depuis `PermissionCatalog.All` ; huit **politiques d'alias** existent déjà pour les clés
fines PMS (la clé fine **ou** la clé historique donne accès), ce qui constitue le mécanisme réutilisable
pour la migration vers `domaine.ressource.action`.

| Préfixe | Fichier | Routes | Périmètre |
|---|---|---:|---|
| `/account` | AccountEndpoints | 1 | changement de mot de passe |
| `/security`, `/users` | SecurityEndpoints | 11 | permissions, rôles, utilisateurs, activation, reset |
| `/audit` | AuditEndpoints | 2 | consultation, purge |
| `/settings` | SettingsEndpoints | 2 | paramètres globaux |
| `/organization/hotel-units` | OrganizationEndpoints | 6 | unités |
| `/revenue/daily` | RevenueEndpoints | 9 | CA journalier, workflow, dashboard, summary |
| `/closing/daily` | ClosingEndpoints | 4 | clôture, réouverture |
| `/treasury/*` | TreasuryEndpoints | 19 | comptes, encaissements, ordres de paiement |
| `/accounting/*` | AccountingEndpoints | 33 | classes, comptes, journaux, écritures, exercices, périodes, tiers, lettrage, balances, grand livre, seed SCF |
| `/budget/*` | BudgetEndpoints | 10 | plans, lignes, approbation, écarts |
| `/billing/*` | BillingEndpoints | 13 | clients, factures (issue, pay, cancel) |
| `/receivables/*` | ReceivablesEndpoints | 5 | balance âgée, relances, risque |
| `/tariffs/*` | TariffsEndpoints | 18 | plans, périodes, conventions, résolution |
| `/lodging/*` | Lodging, LodgingCatalog, LodgingInventory, LodgingOperations | 96 | disponibilité, réservations, front-desk, folios, acomptes, extras, forfaits, politiques, restrictions, yield, overbooking, blocs OOO/OOS, night audit, tape chart, prévisionnel |
| `/housekeeping/*` | HousekeepingEndpoints | 19 | tableau, tâches, inspection, minibar |
| `/crm/*` | CrmEndpoints | 31 | profils, 360, segments, fidélité, campagnes, satisfaction, interactions |
| `/mice/*` | MiceEndpoints | 23 | espaces, événements, devis, BEO, facturation, allotements, rooming lists |
| `/inventory/*` | InventoryEndpoints | 20 | magasins, articles, mouvements, transferts, inventaires, stock bas |
| `/purchasing/*` | PurchasingEndpoints | 13 | fournisseurs, commandes, approbation, réception |
| `/kitchen/*` | KitchenEndpoints | 15 | fiches techniques, coût, HACCP, relevés |
| `/hr/*` | HumanResourcesEndpoints | 41 | départements, postes, salariés, contrats, pointages, absences, paie |
| `/approvals/*` | ApprovalsEndpoints | 13 | circuits, instances, décision |
| `/kpis/*` | KpiEndpoints | 16 | catalogue, dashboard, comparaison, historique, seuils, mappings, snapshots |
| `/pilotage/*` | PilotageEndpoints | 2 | dashboard groupe, cockpit DEC |
| `/reporting/*` | ReportingEndpoints | 3 | catalogue, exécution, journal |
| `/maintenance/backups` | MaintenanceEndpoints | 3 | état, déclenchement |
| `/sync/*` | SyncEndpoints | 4 | postes, heartbeat, erreurs |

## 1.6 Inventaire PostgreSQL

Schéma par défaut `raqmi` ; 22 migrations EF (`InitialSchema` 28/08 → `AccountingAuxiliaryLedger` 01/09).
Les scripts `database/postgres/001-004` sont historiques et ne doivent plus être exécutés
(`docs/postgresql.md`).

| Schéma | Tables | Contexte propriétaire |
|---|---|---|
| security | users, roles, permissions, role_permissions, user_roles, refresh_tokens, workstations | Identity, Sync |
| audit | audit_logs, workstation_failures | Audit, Sync |
| organization | hotel_units | Organization |
| settings | application_settings | Settings |
| exploitation | daily_revenues, daily_closings | Revenue, Closing |
| finance | customers, invoices, invoice_lines, bank_accounts, cash_receipts, payment_orders, reminders | Billing, Treasury, Receivables |
| accounting | chart_accounts, journals, journal_sequences, journal_entries, journal_entry_lines, fiscal_years, periods, parties, reconciliations, reconciliation_allocations | Accounting |
| budgeting | budget_plans, budget_lines | Budgeting |
| tariffs | rate_plans, rate_periods, customer_conventions | Tariffs |
| lodging | room_types, rooms, room_beds, room_type_beds, room_blocks, reservations, reservation_events, reservation_extras, stay_room_assignments, folios, folio_charges, deposits, extra_items, packages, package_components, cancellation_policies, cancellation_policy_rules, rate_restrictions, yield_rules, overbooking_allowances, lodging_policies, night_audit_runs, **function_spaces, event_bookings, event_booking_lines, event_schedule_items, room_allotments** | Lodging (+ Mice hébergé dans le même schéma) |
| housekeeping | room_conditions, housekeeping_tasks, minibar_items, minibar_consumptions | Housekeeping |
| crm | guest_profiles, customer_segments, loyalty_tiers, loyalty_transactions, campaigns, satisfaction_entries, guest_interactions | Crm |
| inventory | warehouses, stock_items, stock_movements, inventory_counts, inventory_count_lines | Inventory |
| purchasing | suppliers, purchase_orders, purchase_order_lines | Purchasing |
| kitchen | recipe_sheets, recipe_ingredients, temperature_checkpoints, temperature_readings | Kitchen |
| hr | departments, positions, employees, employment_contracts, time_entries, absences, payroll_parameter_sets, payroll_tax_brackets, payroll_periods, payroll_bonuses, payslips | HumanResources |
| approvals | approval_circuits, approval_steps, approval_instances, approval_instance_steps, approval_decisions | Approvals |
| kpi | kpi_snapshots, kpi_thresholds, kpi_account_mappings | Kpi |
| reporting | report_executions | Reporting |

## 1.7 Inventaire RBAC

83 clés, groupées par préfixe : `users`, `roles`, `units`, `revenue`, `dashboard`, `treasury`, `audit`,
`reports`, `security`, `closing`, `customers`, `invoices`, `settings`, `accounting` (7 clés dont `post`,
`reconcile`, `close`, `reverse`, `admin`), `budget`, `receivables`, `tariffs`, `lodging` (14 clés dont 11
fines : `reserve`, `checkout`, `change_rate`, `room_move`, `override_restriction`, `overbooking`, `noshow`,
`cancel`, `manage_rooms`, `manage_rates`, `night_audit`), `housekeeping`, `crm`, `approvals`, `maintenance`,
`sync`, `mice`, `hr` (dont `hr.payroll.close`), `inventory`, `purchasing`, `kitchen`, `kpi.admin`.

Rôles système (`RoleCatalog`) : `system.administrator`, `direction`, `exploitation.control`, `unit.manager`,
`cashier`, `reader`, `hr.manager`. Seuls les quatre premiers portent `approvals.decide` (règle vérifiée par
le Domain et par `SecuritySeederTests`).

Constat structurant : le JWT ne porte que des claims `permission`. **Il n'existe ni entité d'affectation
utilisateur ↔ unité, ni claim de périmètre** ; les routes reçoivent `hotelUnitCode` en paramètre sans
contrôle d'appartenance. Le « périmètre par unité » annoncé par le module 1 n'est pas implémenté.

## 1.8 Inventaire Desktop

Client WPF sans persistance métier locale ; `RaqmiApiClient` en 29 partiels. Quatre écrans (Unités, Recettes,
Tableau de bord, Journal d'audit) sont écrits **inline dans `MainWindow.xaml`** ; les 26 autres sont des
`UserControl` sous `Views/` chargés paresseusement via `ModuleViewContext`. La navigation est un ruban de 31
onglets masqué, piloté par l'accueil (catalogue de cartes) et une barre latérale.

| Onglet | Écran | Entrées du catalogue | Permission de lecture |
|---:|---|---|---|
| 1 | Unités (inline) | 3 | `units.read` |
| 2 | Recettes (inline) | 4 | `revenue.read` |
| 3 | Tableau de bord (inline) | 24 | `dashboard.read` |
| 4 | Journal d'audit (inline) | 22, 30 | `audit.read` |
| 5 | ClosingView | 4.5 | `closing.read` |
| 6 | TreasuryView | 5 | `treasury.read` |
| 7 | CustomersView | 9.2 | `customers.read` |
| 8 | InvoicesView | 8 | `invoices.read` |
| 9 | SettingsView | 2 | `settings.read` |
| 10 | UsersView | 1 | `users.read` |
| 11 | AccountingView | 5.2 | `accounting.read` |
| 12 | BudgetView | 6 | `budget.read` |
| 13 | ReceivablesView | 9 | `receivables.read` |
| 14 | TariffsView | 14.5 | `tariffs.read` |
| 15 | LodgingView | 10 | `lodging.read` |
| 16 | ApprovalsView | 22.2 | `approvals.read` |
| 17 | ReportsView | 25 | `reports.read` |
| 18 | BackupView | 28 | `maintenance.read` |
| 19 | GroupDashboardView | 24.2 | `dashboard.read` |
| 20 | DecCockpitView | 24.4 | `dashboard.read` |
| 21 | HousekeepingView | 10.2 | `housekeeping.read` |
| 22 | HumanResourcesView | 21 | `hr.read` |
| 23 | CrmView | 10.4 | `crm.read` |
| 24 | InventoryView | 11 | `inventory.read` |
| 25 | PurchasingView | 12 | `purchasing.read` |
| 26 | KitchenView | 11.5 | `kitchen.read` |
| 27 | SyncView | 29 | `sync.read` |
| 28 | MiceView | 10.6 | `mice.read` |
| 29 | KpiView | 25.4 | `dashboard.read` |
| 30 | PmsView | 10.1 | `lodging.read` |

## 1.9 Inventaire des tests

Familles : Domain (`AccountingTests`, `LodgingTests`, `HumanResourcesDomainTests`, `KpiMathTests`…),
Application/Infrastructure sur SQLite/InMemory (`*ServiceTests`, `*ConcurrencyTests`), API via
`RaqmiApiFactory` (`*EndpointTests`, matrice RBAC par route), calculateurs KPI/Pilotage, seeder de sécurité,
harness PMS (`PmsHarness`, `TestReservations`). Aucun test PostgreSQL réel, aucun test WPF, aucun scénario
E2E transversal jusqu'à la comptabilité.

## 1.10 Livré vs annoncé

| Bloc | Livré et testé | Non livré (à ne pas confondre avec du fonctionnel) |
|---|---|---|
| Sécurité | JWT, refresh rotation, verrouillage, anti-lockout, audit, politiques par permission | périmètre unité, sessions, délégations, profils |
| Organisation | unités hôtelières | entreprise, établissements, directions, services, centres de coûts |
| Recettes/clôture | CA journalier avec workflow, clôture journalière avec réouverture motivée | — |
| Finance | trésorerie avec approbation, facturation (émission numérotée, paiement, annulation), créances, budget, comptabilité SCF générale et auxiliaire, exercices/périodes | avoirs (aucune route observée), rapprochement bancaire, fiscalité, analytique, états financiers, réouverture d'exercice, déversement automatique des modules |
| PMS | inventaire unique, restrictions, réservations par type, séjour, folios multiples, acomptes, night audit idempotent, planning, prévisionnel | channel manager (interface seule), booking engine |
| Housekeeping | états, tâches, inspection, minibar | linge, objets trouvés, incidents, productivité |
| CRM | 360, segments, fidélité, campagnes, NPS, interactions, consentement | prospects, opportunités, réclamations |
| MICE | espaces, événements, devis, BEO, facturation, allotements, rooming lists | tarifs groupes dédiés, planning personnel/matériel |
| Stocks | magasins, articles, mouvements PMP, transferts, inventaires, stock bas | lots, expiration, FEFO, emplacements, rotation |
| Achats | fournisseurs, commandes approuvées, réception → stock | demande d'achat, consultation, facture fournisseur, 3-way matching, marchés |
| Cuisine | fiches techniques, coût matière, HACCP températures | POS, KDS, menu engineering, allergènes, traçabilité |
| RH/paie | dossiers, contrats, pointages, absences, paie algérienne, clôture de période | badgeuses, soldes de congés, STC, prêts, formation, discipline, santé, génération comptable |
| Workflow | circuits, instances, décision par rôle | sujets autres que l'ordre de paiement, retour correction, délégation, escalade, échéance |
| Pilotage | dashboards unité/groupe/DEC, bibliothèque KPI, seuils, snapshots, comparatif, rapports CSV | Data Warehouse, rapports planifiés, drill-down généralisé |
| Système | sauvegarde à la demande, registre des postes, santé API/DB, déploiement scripté | restauration applicative, monitoring, updates/rollback, licence |
| Absents | — | Mon Espace, notifications, messagerie, GED, juridique, maintenance métier, PortMaster, parking, POS, intégrations matérielles |

## 1.11 Constats de qualité et d'hygiène

1. **Classes volumineuses** : `CrmService` 2 132 lignes, `LodgingView.xaml.cs` 2 061, `MainWindow.xaml.cs`
   1 912 (+ 1 878 de XAML), `InventoryService` 1 730, `KpiCatalog` 1 727, `KitchenView` 1 720,
   `InventoryView` 1 705, `PurchasingView` 1 661, `HumanResourcesService` 1 567. La réorganisation doit
   extraire par cas d'usage, jamais réécrire.
2. **Couplages circulaires en Infrastructure** (via `RaqmiDbContext` partagé) : Lodging ↔ Housekeeping,
   Lodging ↔ Mice, Lodging ↔ Crm, Revenue ↔ Closing (détail dans le livrable 5).
3. **Référentiels transverses logés dans un module** : taux de TVA dans `Billing.InvoiceLine` (utilisé par
   Lodging, Purchasing, Settings) ; `Department` dans HR ; tiers comptables (`accounting.parties`) distincts
   de `finance.customers` et `purchasing.suppliers` sans lien explicite observé.
4. **Absence de clé d'idempotence** sur les routes d'écriture (encaissement, facture, écriture) : documenté
   dans `docs/modules-catalog.md` §29 ; bloquant pour un moteur comptable événementiel et pour tout rejeu.
5. **Tests sur SQLite/InMemory** : les contraintes, index et transactions PostgreSQL ne sont pas prouvés.
6. **Statut `Disponible` ≠ Production Ready** : le garde vérifie le câblage, pas la maturité.
7. **Solution réécrite** : `RaqmiSystem.sln` a été réécrit par Visual Studio 18 (GUID de type de projet
   `9A19103F-…-9A1E7A4F7556F` malformé → `FAE04EC0-…`, tabulations). Changement bénin et correctif ; à
   commiter séparément.
8. **Répertoires vides parasites** `UsersHPAppDataLocalTempclauderq-verify2 2Debug/net10.0` dans Domain,
   Application, Desktop (sortie d'un build avec chemin mal formé) : 0 fichier, non suivis, à supprimer.
9. **Documentation legacy** (85 fichiers) décrivant l'ancien produit Electron/SQLite : référence
   fonctionnelle, pas preuve de livraison ; `parking.md`, `plage-piscine.md`, `portmaster*.md` existent
   pour des domaines sans aucune implémentation .NET.
10. **Lot 0 non commité** : `FunctionalArchitectureCatalog` (22 domaines, mapping des 50 ordres),
    `FunctionalDomainOption`, filtre de domaine sur l'accueil, barre latérale reconstruite par domaine,
    `SidebarLayout` `[Obsolete]`, `FunctionalArchitectureCatalogTests` (5 tests). Compatible et vert, mais
    écrit avant validation de la cartographie ; deux comportements à arbitrer (README, décision 7).
