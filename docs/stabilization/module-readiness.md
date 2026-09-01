# Stabilisation de l'existant — Module Readiness

Date de référence : 2026-09-01
Branche de stabilisation : `stabilization/module-readiness`
Catalogue de référence : **50 modules / 31 Disponibles / 19 Planifiés**.

## Gel fonctionnel temporaire

Jusqu'à la levée explicite de cette phase, **aucun nouveau module fonctionnel ne doit passer à `Disponible`**.
Les changements autorisés sont :

- correction de bugs ;
- sécurité et permissions ;
- navigation et ergonomie ;
- fiabilisation API/DB ;
- tests ;
- documentation ;
- performance ;
- sauvegarde, déploiement et observabilité.

Toute exception doit modifier volontairement cette matrice et satisfaire le garde automatique `tools/check-module-readiness.ps1`.

## Définition de "Disponible"

Un module ne peut rester `Disponible` que si les critères suivants sont satisfaits :

| Critère | Exigence |
|---|---|
| Domain | Les règles métier nécessaires au périmètre annoncé existent hors UI. |
| API | Le module expose son service au client ou est explicitement un module d'agrégation local. |
| DB | La persistance requise existe ; aucune donnée métier critique n'est simulée en mémoire en production. |
| RBAC | Une permission de lecture existe et protège l'accès. |
| Desktop | Un écran WPF réel est déclaré et compilable. |
| Navigation | L'entrée du catalogue pointe vers un onglet réel et unique. |
| Tests | Les règles critiques disposent de tests automatisés et la suite générale reste verte. |
| Documentation | Le périmètre livré et les limites sont documentés. |
| Smoke test | L'écran peut être ouvert par un profil autorisé sans exception ni écran vide. |

## Garde automatique

Le script `tools/check-module-readiness.ps1` vérifie pour **chaque module Disponible** :

1. présence d'une `PermissionCatalog.*` ;
2. présence d'un `TabIndex` ;
3. unicité de l'onglet ;
4. existence réelle de cet onglet dans `MainWindow.xaml` ;
5. présence d'un `x:Name` sur l'onglet ;
6. existence de la constante de permission ;
7. câblage exact `ApplyModuleAccess(PermissionCatalog.X, TabItem)` ;
8. cohérence des totaux `ExpectedTotal` / `ExpectedAvailable`.

La workflow `.github/workflows/stabilization.yml` exécute ce garde sous Windows, compile le client WPF et lance toute la suite de tests.

## Matrice des 31 modules Disponibles

Légende :

- **AUTO** : vérifié par `check-module-readiness.ps1` à chaque CI ;
- **CI** : vérifié par compilation/tests automatisés ;
- **DOC** : documenté dans le catalogue et/ou une documentation de module ;
- **SMOKE** : doit être validé à l'exécution sur le client WPF ;
- **N/A** : non applicable au périmètre du module.

| # | Module | Domain | API | DB | RBAC | Desktop | Tests | Documentation | Smoke test |
|---|---|---|---|---|---|---|---|---|---|
| 1 | Administration & utilisateurs | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 2 | Paramétrage global | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 3 | Unités hôtelières | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 4 | CA journalier (ERP) | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 4.5 | Clôture journalière & Night Audit | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 5 | Encaissements & trésorerie | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 5.2 | Comptabilité SCF | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 6 | Budget & prévisions | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 8 | Facturation | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 9 | Créances & recouvrement | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 9.2 | Clients | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 10 | Hébergement & occupation | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 10.1 | PMS front office | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 10.2 | Housekeeping & chambres | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 10.4 | CRM & expérience client | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 10.6 | Groupes & MICE | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 11 | Stocks & consommations | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 11.5 | Cuisine, production & qualité | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 12 | Achats & approvisionnements | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 14.5 | Tarifs & conventions | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 21 | RH & paie | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 22 | Audit & contrôle interne | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 22.2 | Workflows & validations | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 24 | Tableaux de bord directionnels | CI | CI | N/A | AUTO | AUTO | CI | DOC | SMOKE |
| 24.2 | Dashboard PDG | CI | CI | N/A | AUTO | AUTO | CI | DOC | SMOKE |
| 24.4 | Cockpit DEC | CI | CI | N/A | AUTO | AUTO | CI | DOC | SMOKE |
| 25 | Rapports automatiques | CI | CI | N/A | AUTO | AUTO | CI | DOC | SMOKE |
| 25.4 | Comparatif inter-unités / KPI | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 28 | Sauvegarde & restauration | CI | CI | N/A | AUTO | AUTO | CI | DOC | SMOKE |
| 29 | Registre des postes & erreurs clients | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |
| 30 | Journalisation & traçabilité | CI | CI | CI | AUTO | AUTO | CI | DOC | SMOKE |

## Smoke test d'acceptation

Le smoke test manuel/automatisé doit utiliser au minimum deux profils :

1. **administrateur** : tous les 31 écrans doivent être ouvrables ;
2. **profil restreint** : les modules sans permission doivent être verrouillés et impossibles à ouvrir, y compris par raccourci clavier.

Pour chaque écran :

- ouverture depuis l'accueil ;
- ouverture depuis la barre latérale ;
- chargement sans exception ;
- absence d'écran vide ;
- actualisation (`F5`) si disponible ;
- navigation clavier vers module précédent/suivant ;
- déconnexion/reconnexion avec changement de profil ;
- aucun droit conservé depuis l'ancien JWT.

## Critère de sortie de la phase de stabilisation

Le gel est levé seulement lorsque :

- le garde readiness est vert ;
- la compilation WPF Release est verte ;
- la suite de tests est verte ;
- les 31 lignes de la colonne `Smoke test` ont été validées sur une build candidate ;
- aucun bug bloquant ou critique de navigation/RBAC n'est ouvert.
