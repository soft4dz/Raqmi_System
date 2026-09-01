# 3. Cartographie cible

Les 22 domaines sont une **taxonomie fonctionnelle** posée au-dessus du monolithe modulaire existant. Ils ne
créent ni 22 projets, ni 22 bases. Chaque sous-module ci-dessous est qualifié par sa couverture actuelle :

- **Existant** : parcours utilisable aujourd'hui (Domain + API + DB + RBAC + Desktop + tests) ;
- **Partiel** : une partie du parcours existe, ou existe sous une autre forme ;
- **Absent** : rien dans le dépôt .NET (la documentation legacy ne compte pas).

Identifiants stables : `01`…`22` (`FunctionalArchitectureCatalog`). Maturité initiale = niveau de readiness
du livrable 7, calculée depuis la couverture, jamais saisie.

## 01 — Mon Espace (maturité initiale : Planned)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Tableau de bord personnel | Absent | l'accueil actuel est un catalogue de modules, pas un portail personnel |
| Mes tâches | Absent | aucune entité `Task` transverse ; tâches housekeeping propres au module |
| Mes validations | Partiel | `GET /approvals/instances/pending` filtré par rôle (ApprovalsView) ; un seul sujet |
| Notifications | Absent | — |
| Messagerie interne | Absent | — |
| Mon agenda | Absent | — |
| Mes documents | Absent | dépend de 17 GED |
| Mes favoris | Absent | — |
| Mon activité | Partiel | journal d'audit filtrable (`audit.read`), pas de vue personnelle |
| Mes demandes | Absent | absences RH et demandes d'approbation existent sans vue « mes demandes » |
| Mes délégations | Absent | — |
| Mon profil | Partiel | `GET /api/v1/me` |
| Mes préférences | Partiel | thème et URL API stockés localement (`DesktopSettings`, `ThemeManager`) |
| Ma sécurité | Partiel | changement de mot de passe, refresh tokens ; pas de liste de sessions |

## 02 — Administration & Socle ERP (Functional)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Organisation | Partiel | `HotelUnit` seul ; `hr.departments` est RH ; pas d'entreprise/établissement/direction/service/centre de coûts |
| Utilisateurs | Existant | comptes, rôles, permissions, activation, reset, verrouillage ; **absent** : profils, périmètres, affectations, délégations |
| Référentiels | Partiel | TVA (`Billing.InvoiceLine`), moyens de paiement (`Treasury.PaymentMethod`), banques (`BankAccount`), types de pension (`BoardType`), catégories (par module) ; **absent** : pays, wilayas, communes, devises, unités de mesure centrales |
| Paramétrage | Partiel | `ApplicationSettings` global ; numérotation par module (`journal_sequences`, factures, commandes) ; **absent** : paramètres unité, langues, formats |
| Sécurité | Existant | JWT, RBAC, rotation refresh, politique mot de passe, audit sécurité ; **absent** : sessions, politiques avancées |

## 03 — Finance & Comptabilité (Functional)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Comptabilité générale | Existant | plan SCF, journaux, pièces, écritures, lignes, `post`, `reverse`, `cancel`, seed SCF ; Σ D = Σ C imposé |
| Exercices | Existant | exercices, périodes, clôture ; **absent** : réouverture contrôlée |
| Comptabilité auxiliaire | Existant | tiers, lettrage partiel/total, balance auxiliaire |
| États comptables | Partiel | journaux, grand livre, balance générale/auxiliaire ; **absent** : bilan, compte de résultat |
| Comptabilité analytique | Absent | `KpiAccountMapping` rattache des comptes à des groupes de gestion, pas de centre de coûts |
| Trésorerie | Existant | banques, caisses, encaissements, décaissements, ordres de paiement avec approbation, résumé ; **absent** : rapprochement bancaire, prévisions |
| Créances | Existant | balance âgée, relances, risque ; **absent** : contentieux, provisions |
| Budget | Existant | budget, lignes, approbation, clôture, budget vs réalisé ; **absent** : révisions, engagements, forecast |
| Fiscalité | Absent | taux TVA sur lignes seulement |
| Recettes journalières (héritage) | Existant | `Revenue` : CA journalier déclaratif, validation unité/DEC — rattaché ici comme contrôle financier |

## 04 — Commercial, Clients & CRM (Functional)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Particuliers, sociétés, agences, institutions, TO | Existant | `Billing.Customer` typé ; fichier unique |
| Prospects, opportunités | Absent | — |
| Contacts, historique client | Partiel | `GuestInteraction`, 360 |
| Préférences, segmentation, fidélité, VIP | Existant | `GuestProfile`, `CustomerSegment`, `LoyaltyTier`/`Transaction` |
| Conventions, tarifs négociés | Existant | `Tariffs.CustomerConvention` (propriétaire 07, exposé ici) |
| Satisfaction, NPS | Existant | `SatisfactionEntry`, `/crm/satisfaction/nps` |
| Réclamations | Absent | — |
| Fiche client 360° | Existant | `GET /crm/guests/{code}/360` |

## 05 — Facturation & Ventes (Functional)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Devis, pro forma | Partiel | devis MICE (`EventBookingLine`) ; pas de devis générique |
| Factures | Existant | brouillon, lignes, émission numérotée, paiement, annulation |
| Avoirs, notes de débit | Absent | aucune route observée |
| Remises, taxes | Partiel | TVA par ligne ; pas de remise structurée |
| Échéanciers | Absent | — |
| Paiements, remboursements | Partiel | `pay` facture ; remboursement d'acompte PMS |
| Facturation société/agence/groupe | Partiel | folios typés PMS → facture ; facture MICE |
| Facturation consolidée | Absent | — |
| Alimentation du moteur comptable | Absent | aucun événement métier vers Accounting |

## 06 — PMS / Hébergement (Functional)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Inventaire | Existant | types, chambres, couchages, capacités, OOO/OOS datés ; **partiel** : bâtiments/étages ; **absent** : équipements |
| Réservations | Existant | disponibilité par type, option, confirmation, garantie, acompte, annulation à politique figée, no-show, walk-in |
| Front Office | Existant | arrivées, check-in, early check-in, in-house, prolongation, room move, upgrade/downgrade, late checkout, check-out, départs |
| Folios | Existant | folios client/société/agence/groupe, extras, forfaits, transferts, acomptes |
| Contrôle | Existant | night audit idempotent, business date, posting, clôture journalière (`Closing`) |
| Planning | Existant | tape chart, arrivées, départs, occupation, prévisionnel ADR/RevPAR |

Invariant conservé : **`AvailabilityCalculator` est l'unique calcul d'inventaire** ; MICE, Revenue,
Booking Engine et Channels le consomment.

## 07 — Revenue Management & Distribution (Technical Preview)

| Sous-module | Couverture | Source actuelle / remarque |
|---|---|---|
| Tarification | Existant | rate plans, périodes/saisons, conventions, forfaits (Lodging) ; **absent** : promotions |
| Restrictions | Existant | stop sell, MinLOS, MaxLOS, CTA, CTD, advance booking, combinées par la plus restrictive |
| Revenue Management | Partiel | `YieldRule`, `OverbookingAllowance`, prévisionnel ; **absent** : pickup, pace, ADR/RevPAR cibles |
| Distribution | Partiel | `IChannelManagerProvider` + registre, **aucun connecteur** |
| Booking Engine | Absent | par construction, doit passer par `ILodgingService` |

## 08 — Housekeeping (Functional)

| Sous-module | Couverture |
|---|---|
| Planning, états Clean/Dirty/Inspected/In Progress, affectations, tâches, inspections, minibar | Existant |
| Linge, Lost & Found, incidents, lien maintenance, qualité, productivité | Absent |

## 09 — Groupes, MICE & Événementiel (Functional)

| Sous-module | Couverture |
|---|---|
| Groupes, allotements, release dates, rooming lists | Existant (`RoomAllotment`, calcul partagé avec la disponibilité) |
| Tarifs groupes | Partiel (convention client) |
| Salles, événements, devis, disponibilité salles, BEO, planning | Existant |
| Restauration, matériel, personnel | Partiel (lignes d'événement) |
| Facturation groupe | Existant (via `IBillingService`, exige `mice.write` + `invoices.write`) |

## 10 — F&B / Restauration (Technical Preview)

| Sous-module | Couverture |
|---|---|
| Points de vente, POS, KDS | Absent |
| Fiches techniques (recettes, ingrédients, portions, costing) | Existant |
| Contrôle (food cost, beverage cost, gaspillage, menu engineering) | Partiel (coût matière théorique, KPI F&B) |
| Hygiène (HACCP, températures) | Existant ; allergènes et traçabilité : Absent |

## 11 — Stocks & Économat (Functional)

| Sous-module | Couverture |
|---|---|
| Articles, familles, unités, magasins, entrées, sorties, transferts, ajustements, inventaires, PMP, minimum, ruptures | Existant |
| Retours, consommations | Partiel (mouvements typés) |
| Emplacements, lots, expiration, FEFO, maximum, stock de sécurité, rotation, dormants | Absent |

## 12 — Achats & Fournisseurs (Technical Preview)

| Sous-module | Couverture |
|---|---|
| Fournisseurs (fiches, catégories) | Existant ; documents, évaluation : Absent |
| Besoins (expression, demande d'achat, validation) | Absent |
| Consultation (demande de prix, offres, comparatif) | Absent |
| Commandes (bon, validation, suivi) | Existant (approbation = numérotation + gel des lignes) |
| Réception (réception, contrôle, retour) | Existant (partielle/totale → stock) ; retour : Absent |
| Factures fournisseur, avoir, 3-way matching | Absent |
| Paiement (dette, échéances, propositions, comptabilisation) | Partiel (ordres de paiement Treasury non liés à une facture fournisseur) |
| Marchés (appels d'offres…) | Absent |

## 13 — Ressources Humaines & Paie (Functional)

| Sous-module | Couverture |
|---|---|
| Personnel (dossier, contrat, poste, affectation) | Existant ; carrière, documents : Absent |
| Temps (pointage manuel, présence, absences) | Partiel ; planning, badgeuses, retards, heures sup : Absent |
| Congés (demandes, validation) | Existant ; soldes, planning : Absent |
| Paie (variables, salaires, primes, retenues, IRG, CNAS, bulletins, clôture) | Existant ; acomptes, prêts, STC, génération comptable : Absent |
| Développement RH, discipline, santé | Absent |

## 14 — Maintenance & Patrimoine (Planned)

Tout est **Absent**. Le namespace `Application.Maintenance` actuel désigne la **sauvegarde de base** (22
Administration Système), pas la maintenance métier ; à renommer lors de la création du domaine métier.

## 15 — Qualité, Audit & Contrôle interne (Technical Preview)

| Sous-module | Couverture |
|---|---|
| Piste d'audit technique | Existant (`AuditLog`, service transversal) |
| Programme, missions, checklists, constats, rapports, recommandations, actions correctives, risques, anomalies, incidents, décisions, instructions, preuves | Absent |

## 16 — Juridique & Conformité (Planned)

Consentement marketing (CRM) : Partiel. Tout le reste : Absent.

## 17 — GED / Gestion documentaire (Planned)

Absent. Aucune persistance de document ni de pièce jointe dans le dépôt.

## 18 — PortMaster / Marina (Planned)

Absent (documentation legacy uniquement).

## 19 — Parking & Contrôle d'accès (Planned)

Absent (documentation legacy `parking.md`, `plage-piscine.md`) ; aucune entrée dans le catalogue des 50.

## 20 — Pilotage, KPI & BI (Functional)

| Sous-module | Couverture |
|---|---|
| Dashboards Groupe / PDG / Exploitation (DEC) / Unité / Finance / PMS | Existant |
| Dashboards F&B / RH / Maintenance | Partiel via KPI ; maintenance : Absent |
| KPI Engine hébergement, finance, F&B, RH | Existant (catalogue, snapshots, seuils, alertes, permissions par source) |
| KPI maintenance (MTTR, MTBF…) | Absent |
| Analyse N/N-1, budget/réalisé, benchmark inter-unités | Existant ; drill-down : Partiel |
| BI (Data Warehouse, historisation, rapports automatiques) | Partiel (snapshots, catalogue CSV) ; DW : Absent |

## 21 — Intégrations & Matériels (Technical Preview)

| Sous-module | Couverture |
|---|---|
| Channel Manager Providers | Partiel (interface, registre, aucun fournisseur) |
| Journal des interfaces | Partiel (`workstation_failures`) |
| API externes, webhooks, banques, TPE, CIB, serrures, PBX, badgeuses, KDS, imprimantes, scanners, ANPR, RFID, QR | Absent |

## 22 — Administration Système (Functional)

| Sous-module | Couverture |
|---|---|
| Serveur (PostgreSQL, API, services, configuration) | Partiel (`/health`, `/health/database`, options) |
| Déploiement (serveur, client, postes, versions) | Existant (Docker, Caddy, scripts, installeur, dérive de version) |
| Maintenance (migrations, sauvegarde, health checks) | Existant ; restauration applicative, logs, monitoring : Partiel |
| Updates (serveur, client, rollback) | Absent |
| Licence | Absent |
| Diagnostic | Partiel (registre des postes, erreurs clients) |

## 3.2 Services transversaux cibles

| Service | Germe existant | Ce qui manque | Rendu dans |
|---|---|---|---|
| Workflow | `Approvals` (circuits, étapes, instances, `IApprovalGate`) | sujets multiples, retour correction, délégation, escalade, échéance, commentaires, pièces jointes | 01 Mes validations, 02 Paramétrage des circuits |
| Notifications | — | `Notification`, `NotificationPreference`, référence métier, canaux | 01 Notifications |
| Messagerie | — | `Conversation`, `ConversationMember`, `Message`, `MessageAttachment`, `MessageReadReceipt`, `BusinessObjectReference` | 01 Messagerie |
| RBAC | `PermissionCatalog`, politiques, alias | registre `domaine.ressource.action`, périmètre unité, délégations | 02 Utilisateurs |
| Audit | `AuditLog`, `IAuditLogWriter` | vue personnelle, audit métier structuré | 01 Mon activité, 15, 22 |
| Accounting Posting Engine | `Accounting` (écritures, journaux, périodes) | événements métier, règles de comptabilisation versionnées, outbox, idempotence | 03 |
| KPI Engine | `KpiEngine`, `KpiFactLoader`, snapshots | alertes poussées vers Notifications | 20, 01 |
| Integration Hub | `IChannelManagerProvider`, registre, supervision postes | ports par famille d'équipement, journal d'interfaces, adaptateurs | 21 |

## 3.3 Sources uniques de vérité cibles

| Donnée | Propriétaire cible | Support actuel | Règle |
|---|---|---|---|
| Inventaire hôtelier | 06 PMS | `Lodging.AvailabilityCalculator` | aucun autre calcul de disponibilité |
| Client | 04 CRM, sur `Billing.Customer` | `Customer` + `GuestProfile` 1-1 | pas de second fichier ; `Accounting.Party` référence le client |
| Fournisseur | 12 Achats | `Purchasing.Supplier` | `Accounting.Party` référence le fournisseur |
| Document de vente | 05 Facturation | `Billing.Invoice` | PMS, MICE, POS facturent par `IBillingService` |
| Écriture comptable | 03 Finance | `Accounting.JournalEntry` | générée uniquement par le Posting Engine ou la saisie comptable |
| Quantité physique | 11 Stocks | `Inventory.StockMovement` | opérations via `IStockOperationService` |
| Salarié | 13 RH | `HumanResources.Employee` | — |
| Document | 17 GED | à créer | référencé, jamais recopié |
| KPI | 20 KPI Engine | `KpiSnapshot` | dashboards et Mon Espace lisent le moteur |
| Communication | Messagerie | à créer | référence métier, jamais copie |
| Taux de TVA, devises, pays | 02 Référentiels | `Billing.InvoiceLine` (TVA) | à extraire vers un référentiel partagé sans changer les valeurs |

## 3.4 Navigation cible

```text
Domaine (22)  →  Module  →  Sous-module  →  Écran
  06 PMS      →  Front Office  →  Arrivées     →  PmsView / onglet Arrivées
  03 Finance  →  Comptabilité  →  Grand livre  →  AccountingView / onglet Grand livre
```

Modèle : catalogue hiérarchique immutable (`DomainNode` → `ModuleNode` → `SubmoduleNode` → `ScreenNode`),
chaque nœud portant : identifiant stable, libellé, ordre, icône, permission de lecture, périmètre, maturité,
écran cible, fonctionnalité de licence éventuelle. Les 30 `TabIndex` actuels sont conservés par un
adaptateur ; ils ne sont pas les identifiants cibles.

Chaîne de filtrage à l'ouverture de session : licence → permissions JWT → périmètre (entreprise,
établissement, unité) → profil et préférences → disponibilité réelle (readiness). Le masquage WPF n'est
jamais une sécurité : chaque route reste protégée par sa politique.

## 3.5 Personnalisation par profil (projection sur les rôles actuels)

| Profil cible | Rôle actuel le plus proche | Domaines visibles | Permissions existantes mobilisées |
|---|---|---|---|
| Réception | **aucun** (`cashier` partiel) | 01, 06, 04, 08 (si `housekeeping.read`), 05 (si `invoices.read`) | `lodging.read/checkin/reserve/checkout/room_move`, `customers.read`, `crm.read` |
| Directeur d'unité | `unit.manager` | 01, 20, 06, 10, 11, 13, 14, 03 (partiel), 15 | `dashboard.read`, `lodging.*`, `inventory.*`, `hr.read`, `revenue.validate`, `closing.*`, `approvals.decide` |
| Direction générale | `direction` | 01, 20 (groupe), 03, 06/10/11 (lecture), 13, 15, 16 | `dashboard.read`, `accounting.read`, `treasury.approve`, `budget.approve`, `approvals.decide` |
| Contrôle d'exploitation | `exploitation.control` | 01, 03 (recettes/clôture), 20 (DEC), 15 | `revenue.validate`, `closing.*`, `audit.read`, `approvals.decide` |
| Caisse | `cashier` | 01, 03 (trésorerie), 05 | `treasury.*`, `invoices.*` |
| RH | `hr.manager` | 01, 13 | `hr.*` (jamais `approvals.decide`) |
| Administrateur | `system.administrator` | tous + 02, 22 | toutes les clés |
| Lecture seule | `reader` | selon clés `*.read` | `*.read` |

## 3.6 Maturité initiale par domaine

| Niveau | Domaines |
|---|---|
| Functional | 02, 03, 04, 05, 06, 08, 09, 11, 13, 20, 22 |
| Technical Preview | 07, 10, 12, 15, 21 |
| Planned | 01, 14, 16, 17, 18, 19 |
| Production Ready | aucun (tests PostgreSQL réels, smoke WPF et E2E à industrialiser) |
