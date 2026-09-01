# Stabilisation de l'existant — Module Readiness (par écran)

Date de référence : 2026-09-01
Branche : `reorg/phase-1` (lot 1.3 du [plan de migration](../reorganisation/07-plan-migration.md)), après
`stabilization/module-readiness` et `feature/accounting-scf-core`.
Catalogue de référence : **50 modules / 31 Disponibles / 19 Planifiés**, servis par **30 écrans** (un écran
par onglet `x:Name` de `MainWindow.xaml` ; l'onglet 4 sert les deux entrées 22 et 30).
Preuves lisibles par machine : [`tools/readiness/screens.json`](../../tools/readiness/screens.json).
Garde automatique : [`tools/check-module-readiness.ps1`](../../tools/check-module-readiness.ps1).

## Gel fonctionnel temporaire

Jusqu'à la levée explicite de cette phase, **aucun nouveau module fonctionnel ne doit passer à `Disponible`**
et **aucun écran ne doit passer à un niveau de readiness supérieur** sans modification volontaire de
`screens.json`. Les changements autorisés sont :

- correction de bugs ;
- sécurité et permissions ;
- navigation et ergonomie ;
- fiabilisation API/DB ;
- tests ;
- documentation ;
- performance ;
- sauvegarde, déploiement et observabilité.

Toute exception doit modifier volontairement `tools/readiness/screens.json` (et cette matrice) et satisfaire
le garde automatique. Le garde échoue dès qu'un niveau déclaré dépasse le niveau prouvé.

## Modèle de readiness

Le modèle est celui du plan de migration (section « Modèle de readiness »). Il remplace l'ancien statut
binaire de cette matrice :

| Ancien statut (`ModuleStatus`) | Niveau de readiness |
|---|---|
| `Disponible` | **Functional** |
| `Planifié` | **Planned** |
| `ApiPrête`, `Partiel` (aucune occurrence aujourd'hui) | **Technical Preview** |

| Niveau | Définition | Preuves minimales |
|---|---|---|
| Planned | périmètre et dépendances documentés | fiche de sous-module |
| Technical Preview | noyau technique ou parcours incomplet, données non critiques | Domain + API + tests unitaires |
| Functional | parcours annoncé utilisable | Domain, Application, API, PostgreSQL (migration), RBAC, Desktop, tests, documentation |
| Production Ready | exploitable en production | + PostgreSQL réel en CI, E2E du parcours, smoke WPF, revue sécurité, exploitation (backup/restore), homologation |

Règles :

1. **Le niveau est calculé** par le garde depuis les preuves de `screens.json` ; il n'est jamais saisi. Le
   champ `declared` de chaque écran est une revendication que le garde confronte au niveau prouvé.
2. **Production Ready est impossible** sans preuve PostgreSQL réel en CI (`productionReady.postgresqlCi`),
   E2E du parcours (`productionReady.e2e`) et smoke test WPF joué (`evidence.smoke`). Aucune de ces trois
   preuves n'existe aujourd'hui (audit, section 1.9) : **aucun écran n'est Production Ready**. Les preuves
   « revue sécurité », « exploitation » et « homologation » seront ajoutées au bloc `productionReady` quand la
   phase 2 les définira ; d'ici là, le niveau reste inatteignable par construction.
3. `Disponible` ≠ Production Ready : la conversion ci-dessus donne `Functional`, pas davantage.

## Définition des neuf critères

| Critère | Exigence | Preuve dans `screens.json` | Vérification |
|---|---|---|---|
| Domain | Les règles métier nécessaires au périmètre annoncé existent hors UI. | `evidence.domain` : dossier `src/RaqmiSystem.Domain/<Contexte>` ; `n/a` justifié pour une agrégation pure sans entité (les règles vivent alors dans Application). | existence du chemin |
| Application | L'orchestration existe : contrat `src/RaqmiSystem.Application/<Contexte>` **et** implémentation `src/RaqmiSystem.Infrastructure/<Contexte>`. | `evidence.application` | existence des chemins |
| API | L'écran est servi par au moins un fichier `src/RaqmiSystem.Api/Endpoints/*.cs`, protégé (`RequireAuthorization`). Jamais `n/a`. | `evidence.api` | existence + présence de `RequireAuthorization` |
| PostgreSQL | La persistance requise existe sous forme de migration EF (`Persistence/Migrations/*.cs`) ; aucune donnée métier critique simulée en mémoire. `n/a` justifié pour les vues sans persistance propre (dashboards, sauvegarde). | `evidence.postgresql` | existence du chemin ou justification |
| RBAC | Une permission de lecture existe et protège l'accès : constante `PermissionCatalog.*` (contrôle 6), câblage `ApplyModuleAccess` (contrôle 7) **et** politique API référençant, dans un endpoint déclaré, cette constante **ou une clé cible `domaine.ressource.action` qui la couvre** (couverture lue dans `PermissionRegistry.cs` : une route retaguée vers la clé cible reste ouverte au porteur de la clé historique). | `permission` (constante) ; le reste est **dérivé**, jamais saisi | contrôles 6, 7 + lecture des endpoints et du registre |
| Desktop | Un onglet réel avec `x:Name` existe dans `MainWindow.xaml` (contrôles 3 à 5) et la vue est déclarée (`Views/*.xaml` ou inline). | `evidence.desktop` | contrôles 3-5 + existence du chemin |
| Tests | Les règles critiques disposent de tests automatisés (`tests/RaqmiSystem.Tests/*.cs`) et la suite reste verte. | `evidence.tests` | existence des chemins ; la suite est exécutée par la CI |
| Documentation | Le périmètre livré et les limites sont documentés dans une fiche (`docs/modules/*.md` ; `docs/security.md` admis pour l'administration). `null` = preuve manquante. | `evidence.documentation` | existence du chemin ; période de grâce (voir plus bas) |
| Smoke | L'écran a été ouvert sur une build candidate selon le protocole ci-dessous, avec au moins deux profils. | `evidence.smoke` : `null` ou `{ validatedOn, build, profiles[] }` | forme du compte rendu |

Calcul du niveau :

- **Technical Preview** = Domain + API + Tests ;
- **Functional** = Technical Preview + Application + PostgreSQL + RBAC + Desktop + Documentation (ou grâce active) ;
- **Production Ready** = Functional sans grâce + Smoke + `postgresqlCi` + `e2e`.

Un chemin déclaré mais introuvable est une **erreur de saisie** : le garde échoue immédiatement. Une preuve
`null` est un **manque honnête** : elle abaisse le niveau prouvé, et le garde échoue seulement si le niveau
déclaré devient supérieur.

## Garde automatique

`tools/check-module-readiness.ps1` (Windows PowerShell 5.1 et PowerShell 7) lit le code, ne le modifie jamais
et ne compile rien. Contrôles :

1. présence d'une `PermissionCatalog.*` pour chaque module Disponible ;
2. présence d'un `TabIndex` ;
3. unicité de l'onglet (partage admis uniquement sous la même permission) ;
4. existence réelle de cet onglet dans `MainWindow.xaml` ;
5. présence d'un `x:Name` sur l'onglet ;
6. existence de la constante de permission ;
7. câblage exact `ApplyModuleAccess(PermissionCatalog.X, TabItem)` dans un partiel `MainWindow*.cs` ;
8. cohérence des totaux `ExpectedTotal` / `ExpectedAvailable` ;
9. **catalogue fonctionnel** (`FunctionalArchitectureCatalog.cs`) : exactement 22 domaines, identifiants
   `01`…`22` uniques, chaque ordre Disponible rattaché à exactement un domaine. Le parse s'appuie sur le seul
   motif `Domain("NN", "Nom", "IconKey", FunctionalMaturity.X, "ordres"...)` et ignore tout autre nœud ;
10. **preuves par écran** (`tools/readiness/screens.json`) : chaque onglet Disponible y figure et aucun écran
    orphelin n'y traîne ; ordres et permission identiques au catalogue ; chaque preuve existe ; niveau
    déclaré ≤ niveau prouvé.

Sortie : ligne de câblage (`31/31`), ligne de domaines (`22/22`), tableau par écran (onglet, écran, ordres,
domaine cible, permission, niveau déclaré, niveau prouvé, preuves manquantes), résumé par niveau, état de la
grâce. Le même tableau est écrit en Markdown dans `GITHUB_STEP_SUMMARY` (ou `-MarkdownSummaryPath`). Code de
sortie non nul sur tout échec, messages en français. `-AsOf AAAA-MM-JJ` simule une autre date de référence.

La workflow `.github/workflows/stabilization.yml` exécute ce garde **avant** la compilation, sur chaque PR et
sur chaque push `stabilization/**` et `reorg/**`, publie le tableau dans le résumé du job, puis compile le
client WPF et lance toute la suite de tests.

## Matrice des 30 écrans

Sortie du garde au 2026-09-01. Légende : **OK** preuve présente et vérifiée ; **n/a** critère non applicable,
justifié dans `screens.json` ; **manquante (grâce)** preuve absente couverte par la période de grâce ;
**à valider** smoke test non joué.

| Onglet | Écran | Ordres | Domaine cible | Domain | Application | API | PostgreSQL | RBAC | Desktop | Tests | Documentation | Smoke | Déclaré | Prouvé |
|---:|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | UnitsTabItem | 3 | 02 | OK | OK | OK | OK | OK | OK (inline) | OK | OK (`unites-hotelieres.md`) | à valider | Functional | Functional |
| 2 | RevenueTabItem | 4 | 03 | OK | OK | OK | OK | OK | OK (inline) | OK | OK (`recettes-journalieres.md`) | à valider | Functional | Functional |
| 3 | DashboardTabItem | 24 | 20 | OK | OK | OK | n/a | OK | OK (inline) | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 4 | AuditTabItem | 22, 30 | 15, 22 | OK | OK | OK | OK | OK | OK (inline) | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 5 | ClosingTabItem | 4.5 | 06 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 6 | TreasuryTabItem | 5 | 03 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 7 | CustomersTabItem | 9.2 | 04 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 8 | InvoicesTabItem | 8 | 05 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 9 | SettingsTabItem | 2 | 02 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 10 | UsersTabItem | 1 | 02 | OK | OK | OK | OK | OK | OK | OK | OK (`docs/security.md`) | à valider | Functional | Functional |
| 11 | AccountingTabItem | 5.2 | 03 | OK | OK | OK | OK | OK | OK | OK | OK (`comptabilite-scf.md`) | à valider | Functional | Functional |
| 12 | BudgetTabItem | 6 | 03 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 13 | ReceivablesTabItem | 9 | 03 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 14 | TariffsTabItem | 14.5 | 07 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 15 | LodgingTabItem | 10 | 06 | OK | OK | OK | OK | OK | OK | OK | OK (`pms-hebergement.md`) | à valider | Functional | Functional |
| 16 | ApprovalsTabItem | 22.2 | 01 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 17 | ReportsTabItem | 25 | 20 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 18 | BackupTabItem | 28 | 22 | n/a | OK | OK | n/a | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 19 | GroupDashboardTabItem | 24.2 | 20 | n/a | OK | OK | n/a | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 20 | DecCockpitTabItem | 24.4 | 20 | n/a | OK | OK | n/a | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 21 | HousekeepingTabItem | 10.2 | 08 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 22 | HumanResourcesTabItem | 21 | 13 | OK | OK | OK | OK | OK | OK | OK | OK (`ressources-humaines.md`) | à valider | Functional | Functional |
| 23 | CrmTabItem | 10.4 | 04 | OK | OK | OK | OK | OK | OK | OK | OK (`crm-experience-client.md`) | à valider | Functional | Functional |
| 24 | InventoryTabItem | 11 | 11 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 25 | PurchasingTabItem | 12 | 12 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 26 | KitchenTabItem | 11.5 | 10 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 27 | SyncTabItem | 29 | 22 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 28 | MiceTabItem | 10.6 | 09 | OK | OK | OK | OK | OK | OK | OK | manquante (grâce) | à valider | Functional | Functional (grâce doc) |
| 29 | KpiTabItem | 25.4 | 20 | OK | OK | OK | OK | OK | OK | OK | OK (`bibliotheque-kpi.md`) | à valider | Functional | Functional |
| 30 | PmsTabItem | 10.1 | 06 | OK | OK | OK | OK | OK | OK | OK | OK (`pms-hebergement.md`) | à valider | Functional | Functional |

Bilan : 30 écrans Functional (9 avec fiche, 21 en grâce documentation), 0 Technical Preview, 0 Planned,
**0 Production Ready**. Les entrées `Planifié` du catalogue (19) sont Planned et n'ont pas d'écran ; elles ne
figurent pas dans `screens.json`.

Précisions issues de la relecture du code :

- `ReportsTabItem` : l'ancienne matrice marquait DB = N/A ; c'est inexact, le journal des exécutions est
  persisté dans `reporting.report_executions`. Corrigé.
- `AuditTabItem` : les tests cités prouvent l'écriture du journal comme effet de bord ; aucun test ne couvre
  la route de consultation `GET /audit` ni la purge. À compléter avant tout passage à Production Ready.
- `UnitsTabItem`, `ReceivablesTabItem` : aucun test d'endpoint dédié ; seuls les tests de domaine/service
  existent.
- `BackupTabItem`, `GroupDashboardTabItem`, `DecCockpitTabItem` : Domain `n/a` justifié (règles portées par
  `Application.Maintenance` / `Application.Pilotage`, sans entité propre).
- Le lot `feature/accounting-scf-core` étend `AccountingTabItem` (5.2) sans créer de module : exercices,
  périodes, tiers, lettrage, grand livre et clôture restent dans l'écran et la permission de lecture
  existants, avec des permissions d'action fines.
- Les domaines cibles 07, 10 et 12 sont Technical Preview **au niveau domaine** (distribution, POS,
  achats complets absents) ; les écrans qu'ils contiennent aujourd'hui sont Functional pour le parcours
  qu'ils annoncent. Le rattachement de `RevenueTabItem` (03) et d'`ApprovalsTabItem` (01) reste à valider
  (README du dossier de réorganisation, décision 2).

## Période de grâce documentation

21 écrans Disponibles avant le lot 1.3 n'ont pas de fiche `docs/modules/*.md`. Leur preuve Documentation est
**manquante** — ce n'est pas une erreur de saisie, et la matrice ne le maquille pas. Pour ne pas casser la CI
sur ce seul motif, `screens.json` porte une grâce explicite et datée :

- `documentationGrace.until` : **2026-12-31** ;
- `documentationGrace.screens` : DashboardTabItem, AuditTabItem, ClosingTabItem, TreasuryTabItem,
  CustomersTabItem, InvoicesTabItem, SettingsTabItem, BudgetTabItem, ReceivablesTabItem, TariffsTabItem,
  ApprovalsTabItem, ReportsTabItem, BackupTabItem, GroupDashboardTabItem, DecCockpitTabItem,
  HousekeepingTabItem, InventoryTabItem, PurchasingTabItem, KitchenTabItem, SyncTabItem, MiceTabItem.

Règles : la grâce ne s'applique qu'aux écrans listés ; un écran nouveau sans fiche n'en bénéficie pas ; passé
la date, chaque écran encore listé retombe en Technical Preview et le garde échoue (vérifiable dès aujourd'hui
avec `-AsOf 2027-01-01`) ; prolonger la date exige un commit motivé sur `screens.json` ; un écran qui reçoit
sa fiche doit être retiré de la liste (le garde l'avertit). Le lot 1.4 (documentation) a la charge de vider
cette liste.

## Protocole de smoke test

Le smoke test se joue sur une build candidate (Release), avec au minimum deux profils :

1. **administrateur** (`system.administrator`) : les 30 écrans doivent être ouvrables ;
2. **profil restreint** (par exemple `reader` amputé de plusieurs clés `*.read`, ou `cashier`) : les écrans
   sans permission doivent être verrouillés et impossibles à ouvrir.

Comportement attendu d'un écran verrouillé (décision validée) :

- **visible et cadenassé sur l'accueil** (carte grisée, cadenas, info-bulle indiquant la permission
  manquante) ;
- **absent de la barre latérale** ;
- **impossible à ouvrir par raccourci clavier** (`Ctrl+Tab`, `Ctrl+Shift+Tab`, raccourcis de navigation) : le
  cycle saute l'onglet désactivé et aucun repli transitoire vers l'écran ne doit être observé.

Pour chaque écran ouvrable par le profil :

- ouverture depuis l'accueil ;
- ouverture depuis la barre latérale ;
- **fil d'Ariane** `Domaine → Module → Sous-module → Écran` affiché et cohérent avec le domaine cible de la
  matrice, à chaque ouverture et quel que soit le chemin (accueil, barre latérale, clavier) ;
- chargement sans exception ;
- absence d'écran vide ;
- actualisation (`F5`) si disponible ;
- navigation clavier vers module précédent/suivant ;
- déconnexion/reconnexion avec changement de profil : cartes, barre latérale et onglets recalculés ;
- aucun droit conservé depuis l'ancien JWT (un écran autorisé au profil précédent devient verrouillé).

Compte rendu : pour chaque écran validé, remplacer `"smoke": null` par
`{ "validatedOn": "AAAA-MM-JJ", "build": "<tag ou hash>", "profiles": ["administrateur", "restreint"] }` dans
`screens.json`, dans le même commit que la mise à jour de la colonne Smoke ci-dessus.

## Critère de sortie de la phase de stabilisation

Le gel est levé seulement lorsque :

- le garde readiness est vert (contrôles 1 à 10) ;
- la compilation WPF Release est verte ;
- la suite de tests est verte ;
- les 30 lignes de la colonne `Smoke` ont été validées sur une build candidate et reportées dans
  `screens.json` ;
- aucun bug bloquant ou critique de navigation/RBAC n'est ouvert.

La levée du gel ne rend aucun écran Production Ready : ce niveau exige en plus la gate PostgreSQL réel
(phase 2, lot 2.4) et les scénarios E2E (phase 3+).
