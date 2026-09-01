# 4. Mapping existant → cible

Vocabulaire des actions : **conserver** (rien ne change sauf l'étiquette de navigation), **rattacher**
(changement de domaine de navigation, code intact), **scinder** (une entrée alimente plusieurs domaines,
un seul domaine primaire), **renommer** (libellé ou clé, avec alias), **développer** (planifié, rien
n'existe), **retirer du rang de domaine** (devient service transversal).

## 4.A Les 50 entrées du catalogue actuel

Source de vérité du mapping primaire : `FunctionalArchitectureCatalog.Domains` (un ordre historique → un
domaine, testé). Les rattachements secondaires sont indiqués entre parenthèses.

| Ordre | Fonction actuelle | Domaine actuel (groupe) | Nouveau domaine | Action |
|---:|---|---|---|---|
| 1 | Administration & utilisateurs | Socle | 02 Administration & Socle ERP | conserver (Utilisateurs, Sécurité) |
| 2 | Paramétrage global | Socle | 02 | conserver (Paramétrage) |
| 3 | Unités hôtelières | Socle | 02 | conserver, étendre vers Organisation |
| 4 | CA journalier (ERP) | Finance | 03 Finance & Comptabilité | rattacher (contrôle des recettes) — **à valider** |
| 4.5 | Clôture journalière & Night Audit | Finance | 06 PMS / Hébergement | rattacher (Contrôle) ; alias de navigation depuis 03 |
| 5 | Encaissements & trésorerie | Finance | 03 | conserver (Trésorerie) |
| 5.2 | Comptabilité SCF | Finance | 03 | conserver (cœur comptable) |
| 5.4 | Fiscalité DGI & SIFEC | Finance | 03 | développer (Fiscalité) |
| 6 | Budget & prévisions | Finance | 03 | conserver (Budget) |
| 8 | Facturation | Finance | 05 Facturation & Ventes | rattacher |
| 9 | Créances & recouvrement | Finance | 03 | conserver (Créances) |
| 9.2 | Clients | Finance | 04 Commercial, Clients & CRM | rattacher (fichier unique `Customer`) |
| 10 | Hébergement & occupation | Exploitation | 06 | conserver (Inventaire, Réservations, Folios) |
| 10.1 | PMS front office | Exploitation | 06 | conserver (Front Office, Planning, Contrôle) |
| 10.2 | Housekeeping & chambres | Exploitation | 08 Housekeeping | rattacher |
| 10.4 | CRM & expérience client | Exploitation | 04 | rattacher (fiche 360) |
| 10.6 | Groupes & MICE | Exploitation | 09 Groupes, MICE & Événementiel | rattacher |
| 11 | Stocks & consommations | Exploitation | 11 Stocks & Économat | rattacher |
| 11.5 | Cuisine, production & qualité | Exploitation | 10 F&B / Restauration | rattacher (Fiches techniques, Hygiène) |
| 11.6 | Points de vente (POS) | Exploitation | 10 | développer (folio, facturation et compta partagés) |
| 12 | Achats & approvisionnements | Exploitation | 12 Achats & Fournisseurs | rattacher, puis compléter |
| 12.5 | Appels d'offres | Exploitation | 12 | développer (Marchés) |
| 13 | Maintenance & interventions | Exploitation | 14 Maintenance & Patrimoine | développer (≠ sauvegarde système) |
| 13.5 | Intégrations matérielles | Exploitation | 21 Intégrations & Matériels | développer (Integration Hub) |
| 14.5 | Tarifs & conventions | Exploitation | 07 Revenue Management & Distribution | rattacher (Tarification) ; conventions exposées à 04 |
| 18 | Qualité & réclamations clients | Exploitation | 04 | développer (réclamation CRM) ; action corrective pilotée par 15 |
| 20 | Contrats & conventions | Juridique & commercial | 16 Juridique & Conformité | développer ; référence CRM/tarifs |
| 20.2 | Commercial & partenariats | Juridique & commercial | 04 | développer |
| 21 | RH & paie | Ressources humaines | 13 Ressources Humaines & Paie | rattacher |
| 21.2 | Pointeuses & badgeuses | Ressources humaines | 21 (adaptateur) + 13 (données validées) | développer |
| 22 | Audit & contrôle interne | Contrôle | 15 Qualité, Audit & Contrôle interne | rattacher (vue métier) ; journal technique reste transversal |
| 22.2 | Workflows & validations | Contrôle | 01 Mon Espace (Mes validations) | retirer du rang de domaine : service transversal ; configuration des circuits → 02 |
| 22.4 | Checklists de contrôle | Contrôle | 15 | développer |
| 22.6 | Journal des anomalies | Contrôle | 15 | développer |
| 22.8 | Décisions & instructions | Contrôle | 15 | développer |
| 23 | Conformité hôtelière | Conformité & légal | 16 | développer |
| 23.2 | Protection des données | Conformité & légal | 16 | développer ; consentement CRM par référence |
| 23.4 | Modules légaux | Conformité & légal | 14 (immobilisations) ; scindé vers 03 (fiscal) et 16 (légal) | scinder |
| 23.6 | Veille juridique & réglementaire | Conformité & légal | 16 | développer |
| 24 | Tableaux de bord directionnels | Pilotage | 20 Pilotage, KPI & BI | conserver (Dashboards) |
| 24.2 | Dashboard PDG | Pilotage | 20 | conserver (Dashboards / Groupe) |
| 24.4 | Cockpit DEC | Pilotage | 20 | conserver (Dashboards / Exploitation) |
| 25 | Rapports automatiques | Pilotage | 20 | conserver (BI / Rapports) |
| 25.2 | Alertes & notifications | Pilotage | 01 (rendu) + service Notifications | retirer du rang de domaine : service transversal |
| 25.4 | Comparatif inter-unités / KPI | Pilotage | 20 | conserver (KPI Engine, Analyse) |
| 26 | PortMaster | Spécifique | 18 PortMaster / Marina | développer |
| 27 | Gestion documentaire | Système documentaire | 17 GED | développer |
| 28 | Sauvegarde & restauration | Système | 22 Administration Système | conserver (Maintenance) |
| 29 | Registre des postes & erreurs clients | Système | 22 (Diagnostic) + 21 (journal interfaces) | conserver |
| 30 | Journalisation & traçabilité | Système | 22 (vue technique) ; service Audit transversal | conserver |
| — | (aucune entrée) | — | 19 Parking & Contrôle d'accès | développer |

Contrôle de complétude : 50 entrées, 50 destinations primaires, 0 fonctionnalité sans destination ; le domaine
19 est le seul sans module historique (`FunctionalArchitectureCatalogTests`).

## 4.B Namespaces de code → domaine cible

Aucun namespace n'est renommé en phase 1. Le tableau fixe la destination pour les lots ultérieurs.

| Namespace (Domain / Application / Infrastructure) | Domaine cible | Action |
|---|---|---|
| `Identity`, `Security` | 02 | conserver ; ajouter affectations/périmètres |
| `Organization`, `Settings` | 02 | conserver ; étendre l'organisation |
| `Accounting`, `Treasury`, `Budgeting`, `Receivables` | 03 | conserver |
| `Revenue` | 03 (recettes) | conserver le namespace |
| `Closing` | 06 (clôture journalière) ; 03 pour la clôture financière future | conserver |
| `Billing` | 05 ; `Customer` exposé par 04 ; taux TVA → 02 Référentiels | conserver ; extraire le référentiel TVA plus tard |
| `Crm` | 04 | conserver |
| `Tariffs` | 07 ; conventions exposées à 04 | conserver |
| `Lodging` | 06 ; restrictions/yield/overbooking → 07 (navigation) | conserver |
| `Mice` | 09 | conserver ; schéma `lodging` inchangé |
| `Housekeeping` | 08 | conserver |
| `Kitchen` | 10 | conserver |
| `Inventory` | 11 | conserver |
| `Purchasing` | 12 | conserver |
| `HumanResources` | 13 | conserver |
| `Approvals` | service Workflow | conserver le namespace ; enrichir |
| `Audit` | service Audit | conserver |
| `Kpi`, `Pilotage`, `Reporting` | 20 | conserver |
| `Channels`, `Sync` (supervision) | 21 / 22 | conserver ; `Channels` devient un port de l'Integration Hub |
| `Maintenance` (backup) | 22 | **renommer** en `SystemMaintenance`/`Operations` avant de créer le domaine 14 |
| `Navigation` (lot 0) | socle navigation | conserver, étendre en arbre |

## 4.C Routes API → domaine cible

Toutes les routes actuelles sont **conservées**. Un éventuel préfixe par domaine (`/api/v2/finance/...`)
ne sera introduit qu'avec alias, et seulement si un client externe l'exige.

| Préfixe actuel | Domaine cible |
|---|---|
| `/security`, `/users`, `/account`, `/auth`, `/me`, `/settings`, `/organization` | 02 |
| `/accounting`, `/treasury`, `/budget`, `/receivables`, `/revenue` | 03 |
| `/billing/customers`, `/crm` | 04 |
| `/billing/invoices` | 05 |
| `/lodging` (hors restrictions/yield/overbooking), `/closing` | 06 |
| `/tariffs`, `/lodging/restrictions`, `/lodging/yield-rules`, `/lodging/overbooking` | 07 |
| `/housekeeping` | 08 |
| `/mice` | 09 |
| `/kitchen` | 10 |
| `/inventory` | 11 |
| `/purchasing` | 12 |
| `/hr` | 13 |
| `/approvals` | Workflow (rendu 01/02) |
| `/audit` | Audit (rendu 15/22) |
| `/kpis`, `/pilotage`, `/reporting` | 20 |
| `/sync` | 21/22 |
| `/maintenance/backups`, `/health` | 22 |

## 4.D Schémas PostgreSQL → domaine cible

Aucune table n'est renommée ni déplacée. Les identifiants (UUID) restent stables.

| Schéma | Domaine cible | Remarque |
|---|---|---|
| security, audit, organization, settings | 02 / Audit / 22 | `workstations`, `workstation_failures` → 22 |
| exploitation | 03 (`daily_revenues`), 06 (`daily_closings`) | — |
| finance | 03 (banques, encaissements, ordres, relances), 04 (`customers`), 05 (`invoices`, `invoice_lines`) | — |
| accounting, budgeting | 03 | — |
| tariffs | 07 | — |
| lodging | 06 ; 07 (`rate_restrictions`, `yield_rules`, `overbooking_allowances`) ; 09 (`function_spaces`, `event_*`, `room_allotments`) | tables MICE laissées dans `lodging` |
| housekeeping | 08 | — |
| crm | 04 | — |
| inventory | 11 | — |
| purchasing | 12 | — |
| kitchen | 10 | — |
| hr | 13 | — |
| approvals | Workflow | — |
| kpi, reporting | 20 | — |

## 4.E Permissions → `domaine.ressource.action`

Règle : la clé historique reste valide **au moins une version** ; la nouvelle clé est ajoutée au catalogue ;
la politique accepte l'une ou l'autre (mécanisme d'alias déjà utilisé pour `lodging.*`) ; les rôles système
sont migrés par le seeder ; les rôles personnalisés font l'objet d'un rapport avant migration.

| Préfixe actuel | Préfixe cible | Exemples de correspondance |
|---|---|---|
| `users.*`, `roles.*`, `security.seed` | `admin.user.*`, `admin.role.*`, `admin.security.seed` | `users.write` → `admin.user.create` + `admin.user.update` + `admin.user.deactivate` |
| `units.*`, `settings.*` | `admin.unit.*`, `admin.settings.*` | — |
| `revenue.*`, `closing.*` | `finance.revenue.*`, `lodging.closing.*` | `revenue.validate` → `finance.revenue.validate` ; `closing.close` → `lodging.closing.close` |
| `treasury.*` | `finance.treasury.*` | `treasury.approve` → `finance.payment_order.approve` |
| `accounting.*` | `finance.entry.*`, `finance.period.*`, `finance.party.*` | `accounting.post` → `finance.entry.post` ; `accounting.close` → `finance.period.close` ; `accounting.reverse` → `finance.entry.reverse` |
| `budget.*`, `receivables.*` | `finance.budget.*`, `finance.receivable.*` | — |
| `customers.*`, `crm.*` | `crm.customer.*`, `crm.guest.*`, `crm.loyalty.*` | `crm.loyalty` → `crm.loyalty.post` |
| `invoices.*` | `billing.invoice.*` | `invoices.issue` → `billing.invoice.issue` |
| `tariffs.*` | `revenue.rate.*` | — |
| `lodging.*` (14) | `lodging.reservation.*`, `lodging.checkin.execute`, `lodging.checkout.execute`, `lodging.room.*`, `lodging.rate.*`, `lodging.night_audit.execute` | `lodging.reserve` → `lodging.reservation.create` ; `lodging.checkin` → `lodging.checkin.execute` |
| `housekeeping.*` | `housekeeping.task.*`, `housekeeping.room.inspect` | — |
| `mice.*` | `mice.event.*`, `mice.allotment.*` | — |
| `inventory.*` | `inventory.item.*`, `inventory.movement.*`, `inventory.count.validate` | — |
| `purchasing.*` | `purchasing.order.*`, `purchasing.supplier.*`, `purchasing.receipt.execute` | `purchasing.approve` → `purchasing.order.approve` |
| `kitchen.*` | `fnb.recipe.*`, `fnb.haccp.*` | — |
| `hr.*` | `hr.employee.*`, `hr.time.*`, `hr.payroll.process`, `hr.payroll.close` | `hr.payroll` → `hr.payroll.process` |
| `approvals.*` | `workflow.circuit.*`, `workflow.request.decide` | — |
| `dashboard.read`, `reports.*`, `kpi.admin` | `pilotage.dashboard.read`, `pilotage.report.*`, `pilotage.kpi.admin` | — |
| `audit.read`, `maintenance.*`, `sync.read` | `audit.log.read`, `system.backup.*`, `system.workstation.read` | — |

Le registre complet (83 → clés cibles, avec ressource, action, description et propriétaire) est un livrable
du lot 2.1 (plan, phase 2).

## 4.F Onglets WPF → chemin de navigation cible

| Onglet actuel | Chemin cible Domaine → Module → Sous-module |
|---|---|
| UnitsTabItem | 02 → Organisation → Unités |
| UsersTabItem | 02 → Utilisateurs → Comptes / Rôles |
| SettingsTabItem | 02 → Paramétrage → Paramètres globaux |
| RevenueTabItem | 03 → Recettes → CA journalier |
| TreasuryTabItem | 03 → Trésorerie → Banques / Encaissements / Ordres de paiement |
| AccountingTabItem | 03 → Comptabilité → Plan / Journaux / Écritures / Exercices / Auxiliaire / États |
| BudgetTabItem | 03 → Budget → Budget vs réalisé |
| ReceivablesTabItem | 03 → Créances → Balance âgée / Relances |
| CustomersTabItem | 04 → Clients → Fichier clients |
| CrmTabItem | 04 → CRM → Fiche 360 / Segmentation / Fidélité / Campagnes / Satisfaction |
| InvoicesTabItem | 05 → Factures → Factures / Paiements |
| LodgingTabItem | 06 → Inventaire / Réservations / Folios |
| PmsTabItem | 06 → Front Office / Planning / Contrôle (night audit) |
| ClosingTabItem | 06 → Contrôle → Clôture journalière |
| TariffsTabItem | 07 → Tarification → Rate plans / Conventions |
| HousekeepingTabItem | 08 → Housekeeping → Planning / Inspections / Minibar |
| MiceTabItem | 09 → Groupes / Événements |
| KitchenTabItem | 10 → Fiches techniques / Hygiène |
| InventoryTabItem | 11 → Stocks → Articles / Mouvements / Inventaires |
| PurchasingTabItem | 12 → Fournisseurs / Commandes / Réception |
| HumanResourcesTabItem | 13 → Personnel / Temps / Congés / Paie |
| ApprovalsTabItem | 01 → Mes validations (instances) ; 02 → Paramétrage → Circuits |
| AuditTabItem | 22 → Maintenance → Journal d'audit ; 15 → Audit (vue métier) |
| DashboardTabItem | 20 → Dashboards → Unité |
| GroupDashboardTabItem | 20 → Dashboards → Groupe |
| DecCockpitTabItem | 20 → Dashboards → Exploitation |
| KpiTabItem | 20 → KPI Engine / Analyse |
| ReportsTabItem | 20 → BI → Rapports |
| BackupTabItem | 22 → Maintenance → Sauvegarde |
| SyncTabItem | 22 → Diagnostic → Postes |
