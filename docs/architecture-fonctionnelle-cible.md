# Architecture fonctionnelle cible — audit et plan de migration

## 1. Objet et statut du document

Ce document constitue le dossier préalable obligatoire à la réorganisation fonctionnelle de Raqmi System.
Il ne déclenche aucun renommage de code, aucune migration de données et aucun changement d'API. Son objectif
est de faire valider la cartographie avant le premier lot d'implémentation.

Référence d'audit : branche `feature/accounting-scf-core`, état observé le 1er septembre 2026.

Dossier détaillé (audit chiffré et vérifié par exécution, cartographies actuelle et cible, mapping des 50
entrées et des namespaces/routes/schémas/permissions, dépendances, registre des risques, plan par lots) :
`docs/reorganisation/README.md`. Ce document en est la synthèse de décision.

Décision directrice : la transformation se fera par **rattachement fonctionnel**, puis par **compatibilité et
renommage contrôlé**. Les tables, identifiants, routes et permissions existants restent stables tant qu'un plan
de transition testé n'a pas été livré.

## 2. Résumé exécutif

Le dépôt est un monolithe modulaire .NET 10 composé de cinq couches : Domain, Application, Infrastructure,
API ASP.NET Core et client WPF. Il possède déjà un cœur ERP significatif : sécurité, unités, recettes,
trésorerie, comptabilité SCF, budget, facturation, créances, PMS, housekeeping, CRM, MICE, stocks, achats,
cuisine, RH/paie, workflow, reporting, KPI et administration système.

Le catalogue WPF actuel contient 50 entrées : 31 `Disponible` et 19 `Planifie`. Le garde de readiness vérifie
le câblage Navigation/RBAC/Desktop des 31 entrées disponibles. La suite automatisée compte 914 tests verts au
moment de l'audit (build Release sans avertissement, garde readiness 31/31). Cela prouve une base fonctionnelle solide, mais pas encore un statut « Production Ready » :
les tests PostgreSQL réels, les smoke tests WPF et les scénarios E2E transversaux restent à industrialiser.

La cible à 22 domaines ne nécessite pas 22 projets .NET ni 22 bases de données. Elle doit d'abord devenir une
taxonomie fonctionnelle stable au-dessus du monolithe modulaire existant. Une extraction physique ultérieure
ne sera justifiée que par une contrainte mesurée de déploiement, de charge ou d'autonomie d'équipe.

## 3. Sources auditées

| Axe | Sources de vérité examinées |
|---|---|
| Architecture | `README.md`, `docs/architecture.md`, fichiers `.csproj`, `Directory.Build.props` |
| Catalogue actuel | `src/RaqmiSystem.Desktop/ModuleCatalog.cs`, `docs/modules-catalog.md` |
| Domaine | répertoires et entités sous `src/RaqmiSystem.Domain` |
| Application | contrats et DTO sous `src/RaqmiSystem.Application` |
| Infrastructure | services, configurations EF et `RaqmiDbContext` |
| API | `Program.cs` et les fichiers de `src/RaqmiSystem.Api/Endpoints` |
| PostgreSQL | 22 migrations EF Core et les scripts de `database/postgres` |
| WPF | `MainWindow`, `ModuleCatalog`, vues de `src/RaqmiSystem.Desktop/Views` |
| RBAC | `Domain/Identity/PermissionCatalog.cs`, politiques API et seeder |
| Tests | 111 fichiers de test, 914 tests exécutés avec succès (816 `[Fact]`, 22 `[Theory]`) |
| Exploitation | Docker, Caddy, scripts on-premise, sauvegarde et workflows GitHub Actions |
| Documentation | documentation active sous `docs` et références legacy isolées sous `docs/legacy` |

## 4. Cartographie technique actuelle

### 4.1 Couches

| Couche | Responsabilité observée | Décision cible |
|---|---|---|
| Domain | entités, états et invariants métier | conserver ; classer les nouveaux types par domaine cible |
| Application | contrats de services, requêtes, réponses et contexte d'opération | conserver ; introduire les services transversaux ici |
| Infrastructure | EF Core, PostgreSQL, sécurité et implémentations | conserver ; interdire les accès directs entre propriétaires de données |
| API | Minimal APIs, JWT et politiques de permissions | conserver comme autorité de sécurité |
| Desktop | client WPF, catalogue et navigation par onglets | remplacer progressivement le catalogue plat par un arbre dynamique |

### 4.2 Capacités réellement implémentées

| Bloc actuel | État observé | Éléments principaux |
|---|---|---|
| Identité et sécurité | Fonctionnel | utilisateurs, rôles, permissions, JWT, refresh rotation, changement de mot de passe, audit |
| Organisation | Fonctionnel limité | unités hôtelières ; directions/services/centres de coûts non généralisés |
| Recettes et clôture | Fonctionnel | CA journalier, soumission/validation/rejet, clôture et réouverture |
| Finance | Fonctionnel partiel | trésorerie, facturation client, créances, budget, comptabilité SCF et auxiliaire |
| PMS | Fonctionnel avancé | inventaire unique, restrictions, réservations, séjours, folios, acomptes, night audit, planning |
| Distribution | Technical Preview | abstraction de channel manager sans fournisseur ; booking engine absent |
| Housekeeping | Fonctionnel | états chambre, tâches, inspection, minibar |
| CRM | Fonctionnel | client 360, segmentation, fidélité, campagnes, NPS et interactions |
| MICE | Fonctionnel ciblé | espaces, événements, devis/BEO, allotements et rooming lists |
| Stocks | Fonctionnel ciblé | articles, magasins, mouvements, PMP et inventaires |
| Achats | Fonctionnel ciblé | fournisseurs, commandes, approbation et réception vers stock |
| Cuisine | Fonctionnel ciblé | fiches techniques, coût matière et contrôles HACCP |
| RH/paie | Fonctionnel ciblé | dossiers, contrats, temps, absences et paie algérienne |
| Workflow | Fonctionnel ciblé | circuits, instances, étapes, approbation et rejet |
| Pilotage | Fonctionnel | dashboards, cockpit, bibliothèque KPI, snapshots et rapports |
| Système | Fonctionnel ciblé | sauvegarde, registre postes/erreurs, santé API/DB et audit |

### 4.3 Limites connues à ne pas confondre avec du fonctionnel livré

- Aucun POS complet.
- Aucun moteur de notifications central ni portail « Mon Espace ».
- Aucune messagerie interne.
- Aucune affectation utilisateur ↔ unité ni claim de périmètre dans le JWT : le « périmètre par unité »
  annoncé par le module 1 n'est pas implémenté ; les routes reçoivent `hotelUnitCode` sans contrôle
  d'appartenance.
- Trois référentiels de tiers (`finance.customers`, `purchasing.suppliers`, `accounting.parties`) sans lien
  explicite entre eux.
- Le workflow d'approbation ne couvre qu'un sujet (`PaymentOrder`).
- Aucun moteur transversal de génération comptable depuis les événements métier.
- Aucun connecteur Channel Manager livré ; seule la frontière technique existe.
- Aucun Booking Engine.
- GED, juridique, PortMaster, parking et maintenance métier restent planifiés.
- Les achats ne couvrent pas encore demande d'achat, consultation, facture fournisseur et three-way matching.
- Les stocks ne couvrent pas encore complètement lots, expiration et FEFO.
- Les tests utilisent principalement SQLite/InMemory ; PostgreSQL réel n'est pas encore un gate complet.
- Le readiness actuel emploie quatre statuts techniques et ne prouve pas à lui seul la maturité production.

## 5. Architecture fonctionnelle cible

| # | Domaine cible | Propriétaire fonctionnel | Noyau actuel réutilisé | Maturité initiale cible |
|---:|---|---|---|---|
| 01 | Mon Espace | expérience personnelle | compte, validations, audit | Planned |
| 02 | Administration & Socle ERP | identité et organisation | Identity, Organization, Settings | Functional |
| 03 | Finance & Comptabilité | écritures et référentiels financiers | Accounting, Treasury, Budgeting, Receivables | Functional |
| 04 | Commercial, Clients & CRM | relation et connaissance client | Billing.Customer, CRM, Tariffs | Functional |
| 05 | Facturation & Ventes | documents de vente | Billing | Functional |
| 06 | PMS / Hébergement | inventaire hôtelier et séjour | Lodging, Closing | Functional |
| 07 | Revenue Management & Distribution | prix, restrictions et canaux | Tariffs, yield/restrictions PMS, Channels | Technical Preview |
| 08 | Housekeeping | état opérationnel des chambres | Housekeeping | Functional |
| 09 | Groupes, MICE & Événementiel | groupes et événements | Mice, RoomAllotment | Functional |
| 10 | F&B / Restauration | ventes et production restauration | Kitchen ; POS absent | Technical Preview |
| 11 | Stocks & Économat | quantité physique et valorisation | Inventory | Functional |
| 12 | Achats & Fournisseurs | engagement fournisseur | Purchasing, Approvals | Technical Preview |
| 13 | Ressources Humaines & Paie | salarié et paie | HumanResources | Functional |
| 14 | Maintenance & Patrimoine | actifs et interventions | sauvegarde exclue ; métier absent | Planned |
| 15 | Qualité, Audit & Contrôle interne | contrôles et actions correctives | Audit, Approvals ; métier à étendre | Technical Preview |
| 16 | Juridique & Conformité | obligations et contrats | consentements CRM limités | Planned |
| 17 | GED / Gestion documentaire | document et version | aucune persistance métier actuelle | Planned |
| 18 | PortMaster / Marina | inventaire portuaire | aucune implémentation .NET | Planned |
| 19 | Parking & Contrôle d'accès | accès et stationnement | aucune implémentation .NET | Planned |
| 20 | Pilotage, KPI & BI | calcul analytique et restitution | Pilotage, Kpi, Reporting | Functional |
| 21 | Intégrations & Matériels | adaptateurs fournisseurs | Channels, Sync | Technical Preview |
| 22 | Administration Système | exploitation technique | Maintenance backup, Sync, health/deploy | Functional |

`Functional` signifie ici que le domaine possède un parcours utilisable dans le périmètre annoncé. Il ne
signifie pas que tous les sous-modules de la cible sont déjà développés.

## 6. Mapping des 50 entrées actuelles vers les 22 domaines

| Entrée actuelle | Nouveau domaine | Action de migration | Compatibilité |
|---|---|---|---|
| Administration & utilisateurs | 02 Socle ERP | rattacher à Utilisateurs/Sécurité | conserver routes et clés |
| Paramétrage global | 02 Socle ERP | rattacher à Paramétrage | conserver écran pendant transition |
| Unités hôtelières | 02 Socle ERP | étendre vers Organisation | conserver IDs et `units.*` |
| CA journalier | 03 Finance | rattacher à Contrôle financier/Recettes | conserver API `revenue` |
| Clôture journalière & Night Audit | 06 PMS | séparer vue métier PMS de la clôture financière future | alias de navigation, API stable |
| Encaissements & trésorerie | 03 Finance | rattacher à Trésorerie | conserver |
| Comptabilité SCF | 03 Finance | consolider comme cœur comptable | conserver tables et routes |
| Fiscalité DGI & SIFEC | 03 Finance | créer ultérieurement Fiscalité | planifié |
| Budget & prévisions | 03 Finance | rattacher à Budget/Forecast | conserver |
| Facturation | 05 Facturation & Ventes | désimbriquer visuellement de Finance | conserver propriétaire `Billing` |
| Créances & recouvrement | 03 Finance | rattacher à Créances | conserver |
| Clients | 04 Commercial/CRM | présenter le référentiel `Customer` via le domaine CRM | aucune copie de client |
| Hébergement & occupation | 06 PMS | rattacher à Inventaire/Réservations/Folios | conserver moteur unique |
| PMS front office | 06 PMS | rattacher à Front Office/Planning/Contrôle | fusion de navigation, pas de services |
| Housekeeping & chambres | 08 Housekeeping | rattacher sans duplication des chambres | référence aux chambres PMS |
| CRM & expérience client | 04 Commercial/CRM | consolider fiche 360 | lire le client propriétaire via contrat |
| Groupes & MICE | 09 Groupes/MICE | rattacher | conserver partage de disponibilité PMS |
| Stocks & consommations | 11 Stocks | rattacher | propriétaire unique des quantités |
| Cuisine, production & qualité | 10 F&B | rattacher à Cuisine/Fiches techniques/Hygiène | coût lu depuis Stocks |
| Points de vente (POS) | 10 F&B | développer ultérieurement | imposer folio/facturation/compta partagés |
| Achats & approvisionnements | 12 Achats | rattacher puis compléter | réception via contrat Stocks |
| Appels d'offres | 12 Achats | rattacher à Marchés | planifié |
| Maintenance & interventions | 14 Maintenance | développer comme domaine métier | ne pas confondre avec backup système |
| Intégrations matérielles | 21 Intégrations | rattacher à Integration Hub | planifié |
| Tarifs & conventions | 07 Revenue/Distribution | rattacher Tarification ; exposer conventions au CRM | conserver résolution tarifaire |
| Qualité & réclamations clients | 04 et 15 | CRM propriétaire de la réclamation ; Qualité pilote l'action | une référence, pas deux dossiers |
| Contrats & conventions | 16 Juridique | rattacher ; référencer CRM/tarifs | planifié |
| Commercial & partenariats | 04 Commercial/CRM | rattacher | planifié |
| RH & paie | 13 RH/Paie | rattacher | salarié source unique RH |
| Pointeuses & badgeuses | 21 Intégrations + 13 RH | adaptateur dans 21, données validées dans 13 | planifié |
| Audit & contrôle interne | 15 Qualité/Audit | distinguer audit métier du journal technique | audit trail reste transversal |
| Workflows & validations | 01 Mon Espace (Mes validations) + service transversal | retirer du rang de domaine autonome ; paramétrage des circuits rendu dans 02 | API compatible, consommé par tous ; mapping primaire `22.2 → 01` dans le catalogue |
| Checklists de contrôle | 15 Qualité/Audit | rattacher | planifié |
| Journal des anomalies | 15 Qualité/Audit | rattacher | planifié |
| Décisions & instructions | 15 Qualité/Audit | rattacher | planifié |
| Conformité hôtelière | 16 Juridique/Conformité | rattacher | planifié |
| Protection des données | 16 Juridique/Conformité | rattacher ; consentement CRM par référence | planifié |
| Modules légaux | 03, 14 et 16 | répartir fiscalité/immobilisations/conformité | planifié, pas de module fourre-tout |
| Veille juridique & réglementaire | 16 Juridique/Conformité | rattacher | planifié |
| Tableaux de bord directionnels | 20 Pilotage/KPI | rattacher | conserver agrégations |
| Dashboard PDG | 20 Pilotage/KPI | sous-module Dashboards/Groupe | écran compatible |
| Cockpit DEC | 20 Pilotage/KPI | sous-module Dashboards/Exploitation | écran compatible |
| Rapports automatiques | 20 Pilotage/KPI | rattacher à BI/Rapports | conserver catalogue |
| Alertes & notifications | service transversal + 01 | moteur transversal, rendu dans Mon Espace | planifié |
| Comparatif inter-unités | 20 Pilotage/KPI | rattacher à Analyse | conserver KPI Engine |
| PortMaster | 18 PortMaster | rattacher | planifié |
| Gestion documentaire | 17 GED | rattacher | planifié |
| Sauvegarde & restauration | 22 Administration Système | rattacher | conserver service/outillage |
| Registre des postes & erreurs clients | 22 + 21 | diagnostic dans 22, interfaces dans 21 | conserver API Sync |
| Journalisation & traçabilité | service Audit + 22 | vue technique dans 22, service transversal | conserver `AuditLog` |

## 7. Services transversaux et sources uniques de vérité

| Donnée/capacité | Propriétaire unique | Consommateurs autorisés | Interdiction |
|---|---|---|---|
| Inventaire hôtelier | PMS/Lodging | MICE, Revenue, Booking Engine, Channels | recompter les chambres ailleurs |
| Client commercial | Commercial/CRM, sur le référentiel Customer existant | PMS, Facturation, MICE, Créances | créer un second fichier client |
| Document de vente | Facturation/Billing | PMS, CRM, Trésorerie, Comptabilité | stocker une copie dans le folio |
| Écriture comptable | Finance/Accounting | états, KPI, audit | générer des journaux propres à chaque module |
| Quantité physique | Stocks/Inventory | Achats, Cuisine, POS, Maintenance | modifier le stock sans opération publiée |
| Salarié | RH/HumanResources | workflow, paie, planning, audit | dupliquer l'identité RH dans un autre domaine |
| Document | GED | tous les domaines par référence | recopier le contenu dans les objets métier |
| KPI | KPI Engine | dashboards, Mon Espace, alertes | recalcul divergent dans chaque écran |
| Validation | Workflow transversal | achats, RH, finance, audit, maintenance, juridique | valider par messagerie ou simple booléen local |
| Notification | Notification Service | Mon Espace et futurs canaux | implémenter une boîte par module |
| Conversation | Messaging | tous les domaines via `BusinessObjectReference` | recopier l'objet métier dans les messages |
| Fournisseur externe | Integration Hub | modules via ports Application | dépendance directe depuis Domain/Infrastructure métier |

## 8. Dépendances cibles

Flux autorisés :

```text
WPF -> API -> Application -> Domain
                     |          ^
                     v          |
               Infrastructure --+

Domaines métier -> ports Application transversaux
Infrastructure -> implémentations des ports
Integration Hub -> adaptateurs fournisseurs
```

Flux métier prioritaires :

```text
PMS -> Business Event -> Accounting Posting Engine -> Journal Entry SCF
Billing -> Business Event -> Accounting Posting Engine -> Journal Entry SCF
Purchasing -> Receipt -> Inventory Operation
Purchasing -> Supplier Invoice -> Accounting Posting Engine
Payroll -> Payroll Posted -> Accounting Posting Engine
Any domain -> Approval Request -> Workflow
Any domain -> Notification Request -> Notification Service -> Mon Espace
Any domain -> BusinessObjectReference <- Messaging/GED
```

Les événements métier seront d'abord transactionnels et internes au monolithe. Une outbox PostgreSQL sera
introduite avant tout transport asynchrone afin d'éviter un état métier validé sans événement correspondant.

## 9. Stratégie RBAC

Le format cible est `domaine.ressource.action`, mais les clés existantes ne doivent pas être supprimées.

Exemples de transition :

| Clé actuelle | Clé cible | Stratégie |
|---|---|---|
| `lodging.read` | `lodging.reservation.read` et autres lectures fines | la clé historique vaut les nouvelles lectures pendant une version |
| `lodging.checkin` | `lodging.checkin.execute` | alias serveur explicite et audité |
| `accounting.post` | `finance.entry.post` | conserver la politique ancienne comme alias |
| `accounting.close` | `finance.period.close` | alias temporaire, migration des rôles personnalisés |
| `purchasing.approve` | `purchasing.order.approve` | alias temporaire |
| `hr.payroll` | `hr.payroll.process` | scinder lecture, préparation, validation et clôture |

Règles :

1. L'API reste l'autorité ; le WPF ne fait que projeter les droits accordés.
2. Chaque nouvelle permission possède une ressource, une action, une description et un propriétaire.
3. Les rôles système sont migrés par le seeder ; les rôles personnalisés font l'objet d'un rapport préalable.
4. Une clé historique n'est retirée qu'après une version de compatibilité et mesure d'usage.
5. Le périmètre unité/établissement est contrôlé dans le service métier, pas seulement dans la navigation.

## 10. Navigation WPF cible

Le nouveau modèle de navigation sera un catalogue hiérarchique immutable :

```text
DomainNode
  ModuleNode
    SubmoduleNode
      ScreenNode
```

Chaque nœud portera : identifiant stable, libellé, ordre, icône, permission de lecture, périmètre, niveau de
maturité, écran cible et éventuelle fonctionnalité de licence. Les 31 `TabIndex` actuels seront maintenus par
un adaptateur temporaire ; ils ne deviendront pas les identifiants fonctionnels de la nouvelle navigation.

Filtrage :

1. licence éventuelle ;
2. permissions JWT ;
3. périmètre entreprise/établissement/unité ;
4. profil et préférences ;
5. disponibilité réelle du module.

Le domaine « Mon Espace » sera la page d'accueil. Il agrégera uniquement des projections autorisées et ne
possédera aucune copie de données métier.

## 11. Nouveau modèle de readiness

| Niveau | Définition minimale |
|---|---|
| Planned | périmètre et dépendances documentés, aucun engagement de disponibilité |
| Technical Preview | noyau technique ou parcours incomplet, données non critiques uniquement |
| Functional | parcours annoncé utilisable, API/DB/RBAC/Desktop/tests/documentation présents |
| Production Ready | PostgreSQL réel, E2E, smoke WPF, sécurité, exploitation et homologation validés |

Chaque `ScreenNode` et chaque capacité sans écran doit posséder une fiche contenant : Domain, Application,
API, PostgreSQL, RBAC, Desktop, tests, documentation et smoke test. Le statut se calcule depuis ces preuves ;
il ne doit pas être saisi librement dans le catalogue.

## 12. Risques et parades

| Risque | Impact | Parade |
|---|---|---|
| réorganisation « big bang » | régressions et blocage long | lots verticaux, build vert à chaque commit |
| renommage immédiat des routes/permissions | rupture WPF et rôles clients | alias versionnés et télémétrie d'usage |
| confusion Customer/Guest/Party | doublons de tiers | identité métier explicite et références croisées |
| double comptabilisation | comptes faux | idempotency key et moteur comptable unique |
| événement perdu après commit | désynchronisation | transactional outbox |
| workflow contourné | audit invalide | garde serveur sur transition métier |
| navigation considérée comme sécurité | élévation de privilège | autorisation systématique API/service |
| tests SQLite trompeurs | défaut PostgreSQL en production | gate PostgreSQL réel |
| statuts trop optimistes | faux sentiment de disponibilité | readiness fondé sur preuves |
| services/classes déjà volumineux | régressions difficiles | extraction par cas d'usage, sans réécriture |
| documentation legacy interprétée comme livré | sur-promesse | bannière « référence historique » et catalogue actuel généré |

## 13. Plan de migration

Le découpage détaillé par lots, aligné sur les six phases imposées (réorganiser sans régression,
stabiliser, Accounting & Integration Core, workflows métier, distribution et intégrations, BI), est dans
`docs/reorganisation/07-plan-migration.md`. Les phases ci-dessous en sont la lecture condensée.

### Phase 0 — Validation de la cartographie

- Faire valider ce document par produit, finance, exploitation, sécurité et technique.
- Trancher le propriétaire du référentiel client : conservation recommandée de `Billing.Customer`, exposé par
  un contrat Commercial/CRM, jusqu'à une migration dédiée justifiée.
- Trancher la frontière Clôture journalière PMS / clôture financière.
- Valider les 22 identifiants stables de domaine.

Critères : mapping des 50 entrées approuvé ; aucune fonctionnalité sans destination ; propriétaires de données
et frontières litigieuses signés.

### Phase 1 — Catalogue cible et compatibilité de navigation

- Introduire les modèles de catalogue hiérarchique sans supprimer `ModuleCatalog`.
- Générer l'arbre cible à partir d'une définition unique.
- Adapter accueil et barre latérale ; conserver les onglets existants.
- Ajouter des tests de filtrage par permission, périmètre et statut.
- Remplacer les statuts par le modèle à quatre niveaux, avec conversion explicite de l'ancien catalogue.

Critères : 31 écrans actuels toujours accessibles aux mêmes profils ; aucun écran supplémentaire déclaré
Production Ready ; readiness et tests existants verts.

### Phase 2 — Migration RBAC progressive

- Ajouter le registre structuré `domaine.ressource.action`.
- Définir les alias des permissions historiques.
- Migrer d'abord Finance, PMS, Achats et RH.
- Produire un rapport des rôles personnalisés avant application.
- Tester chaque endpoint avec droit absent, historique et nouveau droit.

Critères : aucune perte d'accès autorisé ; aucune extension silencieuse ; API reste la source d'autorité.

### Phase 3 — Mon Espace, Workflow et Notifications

- Étendre le workflow existant : retour correction, délégation, escalade, échéance et commentaires.
- Ajouter une projection `MyWorkItem` alimentée par références métier.
- Créer le service central de notifications et les préférences.
- Construire Mon Espace : tâches, validations, alertes, activité et demandes.
- Ajouter délégations temporaires avec date, périmètre et audit.

Critères : un utilisateur ne voit que ses éléments autorisés ; toute action métier repasse par le service
propriétaire ; aucune donnée métier n'est copiée dans Mon Espace.

### Phase 4 — Accounting Posting Engine et événements métier

- Définir enveloppe d'événement, idempotence, corrélation et outbox.
- Créer le registre de règles de comptabilisation versionnées.
- Intégrer d'abord Facturation, Trésorerie et PMS.
- Ajouter Achats/Stocks et Paie après homologation du premier lot.
- Rapprocher automatiquement événements, règles et écritures générées.

Critères : une émission répétée ne produit qu'une écriture ; débit = crédit ; période fermée refusée ; toute
écriture générée pointe vers son événement et sa règle ; contre-passation auditée uniquement.

### Phase 5 — Compléter les chaînes P0

- PMS : réservation → séjour → night audit → checkout → facture → paiement → comptabilité.
- Achats : demande → validation → commande → réception → facture fournisseur → paiement → comptabilité.
- Stocks : lots/expiration/FEFO selon besoin homologué.
- Administration système : migrations, backups testés, health checks, logs, versions et rollback.

Critères : quatre scénarios E2E P0/P1 verts sur PostgreSQL ; sauvegarde/restauration démontrée ; smoke WPF
administrateur et profil restreint vert.

### Phase 6 — Messagerie et GED

- Implémenter `Conversation`, membres, messages, accusés, pièces jointes et références métier.
- Autoriser une référence uniquement si l'utilisateur peut lire l'objet cible.
- Stocker les fichiers et versions dans GED ; la messagerie ne garde qu'un identifiant de document.
- Ajouter SignalR seulement après validation du modèle transactionnel et des droits.

Critères : aucune validation par message ; révocation d'accès appliquée ; recherche et archivage testés ;
pièces jointes contrôlées et auditées.

### Phase 7 — Domaines P1/P2/P3

- P1 : F&B/POS, Housekeeping étendu, CRM, RH, Maintenance, Pilotage et MICE.
- P2 : GED, Juridique, PortMaster, Parking et Revenue Distribution.
- P3 : Data Warehouse, BI avancée et automatisations.
- Chaque domaine passe séparément Planned → Technical Preview → Functional → Production Ready.

## 14. Stratégie de tests

| Niveau | Exigence |
|---|---|
| Domain | invariants et transitions d'état |
| Application | orchestration, idempotence, périmètre et dépendances |
| API | contrats HTTP, validation et matrice RBAC |
| PostgreSQL | migrations, contraintes, concurrence, index et transactions |
| Navigation | arbre, filtrage, alias et absence de doublons |
| WPF | chargement, erreurs, reconnexion et changement de droits |
| E2E | chaînes PMS, Achats, F&B et RH jusqu'à la comptabilité |
| Exploitation | backup/restore, update/rollback et observabilité |

Un lot ne peut être déclaré `Production Ready` sans preuve PostgreSQL, smoke WPF et E2E correspondant au
périmètre annoncé.

## 15. Décisions à valider avant codage

1. Approuver les 22 domaines et leurs identifiants stables.
2. Approuver le mapping des 50 entrées actuelles.
3. Confirmer `Billing.Customer` comme référentiel client transitoire unique.
4. Confirmer Workflow, Notifications, Messaging, Audit, Accounting Engine, KPI Engine et Integration Hub
   comme services transversaux, et non comme copies de domaines métier.
5. Approuver la compatibilité d'une version minimum pour routes et permissions historiques.
6. Approuver la séquence Phase 1 Catalogue/Navigation, Phase 2 RBAC, Phase 3 Mon Espace/Workflow, Phase 4
   Accounting Engine.
7. Confirmer qu'aucun nouveau module ne passe `Functional` pendant le gel de stabilisation sans modifier
   explicitement la matrice de readiness.

Après validation de ces décisions, le premier lot de code recommandé est limité au **catalogue fonctionnel
hiérarchique et à son adaptateur de compatibilité**, sans renommage de table, d'endpoint ni de permission.

Décisions complémentaires issues de l'audit détaillé — modèle de périmètre unité, tiers unifiés
(`Party` ↔ `Customer`/`Supplier`), sort du lot 0 déjà écrit sur la branche, rôle Réception — :
`docs/reorganisation/README.md`.
