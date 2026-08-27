# RH & productivité — hub, organisation, fiches de poste

## 1. Présentation

Le module RH est le point d'entrée unique vers l'ensemble des fonctions ressources humaines de l'ERP : pilotage, dossiers collaborateurs, temps & présence, paie/légal DZ, recrutement/talents, validations N+1 et espace personnel. Cette fiche documente :

- le **hub d'accueil RH** (`/rh`) et sa navigation par onglets/sous-onglets (`/rh/:hub/:sub`) ;
- deux écrans transverses routés en dehors du système d'onglets : **Organisation EGT** (organigramme et effectifs cibles) et **Fiches de poste** (référentiel missions/compétences) ;
- la table complète des hubs RH, avec renvoi vers les fiches détaillées [`rh-paie-declarations.md`](rh-paie-declarations.md) (paie, DLG, déclarations DZ) et [`rh-recrutement-pointeuses.md`](rh-recrutement-pointeuses.md) (ATS, pointeuses/badgeuses).

Ce module s'adresse principalement au **responsable RH** — voir le profil correspondant dans [`docs/guides-utilisateurs/07-rh-manager.md`](../guides-utilisateurs/07-rh-manager.md) — ainsi qu'aux chefs de département (validations d'équipe) et à tout collaborateur pour son espace personnel.

## 2. Prérequis & accès

L'accès RH repose sur trois fonctions de `src/shared/permissions.ts`, dérivées de la table `ROLE_PERMISSIONS` :

| Fonction | Rôles couverts | Portée |
|---|---|---|
| `canManageRh(role)` | `RH_MANAGER`, `SUPERADMIN`, `ADMIN_DEC` (via `isAdminRole`) | Gestion complète (collaborateurs, paie, référentiel, talents) |
| `canValidateRhTeam(role)` | `canManageRh` + `CHEF_DEPARTEMENT` | Validation N+1 (absences, pointages, documents d'équipe) |
| `canAccessRhSelf(role)` | `canValidateRhTeam` + `RECEPTIONNISTE` (tout rôle disposant de `rh.self`) | Espace personnel, ses propres pointages/absences |

La route `/rh` (et toutes les sous-routes `/rh/*`) est protégée par le composant `src/routes/RequireRh.tsx` : si aucune des trois fonctions ci-dessus n'est vraie pour l'utilisateur, il est redirigé vers `/dashboard`.

Routes d'entrée déclarées dans `src/routes/AppRoutes.tsx` :

```
/rh                      → RhHubPage (launcher)
/rh/paie/cloture         → RhPaieCloturePage (route explicite, hors système d'onglets)
/rh/organisation/egt     → RhOrganisationEgtPage
/rh/fiches-poste         → RhFichesPostePage
/rh/:hub                 → RhPage (onglet par défaut du hub)
/rh/:hub/:sub            → RhPage (sous-onglet ciblé)
```

## 3. Écrans & champs

### 3.1 Hub d'accueil (`/rh` — `src/pages/rh/RhHubPage.tsx`)

`RhHubPage` affiche un **lanceur d'applications** (`OdooAppLauncher`, `src/components/apps/OdooAppLauncher`) plutôt qu'un tableau de bord classique. Les « applications » proposées sont construites par `buildRhAppsForRole(role)` dans `src/pages/rh/rhApps.config.ts` : une carte par hub RH visible pour le rôle courant (`getRhHubsForRole`), avec icône, couleur et groupe (`Pilotage`, `Personnel`, `Opérations`, `Talents`, `Espace personnel`). Un champ de recherche (« Rechercher dans RH… ») filtre les cartes. Chaque carte pointe vers `hubEntryPath(hub)`, soit le premier sous-onglet du hub, soit le hub lui-même s'il n'a qu'un seul sous-onglet.

### 3.2 Navigation par hub (`/rh/:hub[/:sub]` — `src/pages/rh/RhPage.tsx` + `RhHubContent.tsx`)

`RhPage` résout le hub (`resolveRhHub`) et le sous-onglet (`resolveRhSub`) à partir de l'URL et du rôle, gère les **redirections legacy** (anciens chemins `/rh/employes`, `/rh/planning`, etc. — table `RH_LEGACY_REDIRECTS` dans `src/pages/rh/rhNavigation.ts`) et affiche une barre d'onglets horizontale scrollable quand le hub a plusieurs sous-onglets visibles. Le rendu du contenu est délégué à `RhHubContent`, qui fait un `switch` sur l'identifiant du hub et du sous-onglet pour instancier le bon composant (`EmployesTab`, `PaieTab`, `PlanningTab`, etc.), avec contrôle de visibilité par sous-onglet (`canManage` / `canTeam` / `canSelf`).

### 3.3 Organisation EGT (`/rh/organisation/egt` — `src/pages/rh/RhOrganisationEgtPage.tsx`)

Écran indépendant (pas dans la barre d'onglets RH) affichant l'**organigramme et les effectifs cibles/réels** :
- Sélecteur d'hôtel (« Tous les hôtels » ou unité précise).
- Cartes de synthèse par direction : `effectifReel / effectifCible` et écart coloré (vert si ≥ 0, rouge sinon) — alimentées par `ipcClient.rh.getEffectifsEgt(hotelId)`.
- Table détaillée par nœud d'organigramme (`ipcClient.rh.getOrganigrammeEgt(hotelId)`) : colonnes Type, Libellé, Cible, Réel, Écart.
- Bouton **Export CSV** (`ipcClient.rh.exportOrganigrammeCsv`) téléchargeant `organigramme-egt[-hotel-N].csv`.

« EGT » désigne l'effectif global théorique/cible par direction, comparé à l'effectif réel calculé côté serveur.

### 3.4 Fiches de poste (`/rh/fiches-poste` — `src/pages/rh/RhFichesPostePage.tsx`)

Référentiel des fiches de poste (une par poste, versionnées — champ `version`) :
- Liste des fiches (`ipcClient.rh.listFichesPoste(posteIdFilter)`) avec filtre optionnel par ID de poste, libellé du poste, version et aperçu de la mission principale (`missionPrincipale` ou champ legacy `missions`).
- Formulaire **Nouvelle fiche** (modal) : ID poste (obligatoire), mission principale, responsabilités, compétences requises, indicateurs de performance. Enregistrement via `ipcClient.rh.upsertFichePoste` (`upsert` : une fiche par `posteId`).

### 3.5 Table des hubs RH (`src/pages/rh/rhNavigation.ts` — `RH_HUBS`)

| Hub (`id`) | Chemin | Sous-onglets | Visible si | Détail |
|---|---|---|---|---|
| `pilotage` | `/rh/pilotage` | Tableau de bord, Analyses IA, Prévisions, Comparatif, Onboarding, PortMaster | `canManageRh` | Cette fiche (KPIs RH généraux) |
| `collaborateurs` | `/rh/collaborateurs` | Annuaire, Contrats, Organigramme, Affectations | `canManageRh` | Cette fiche (dossiers collaborateurs) |
| `referentiel` | `/rh/referentiel` | — (pas de sous-onglet) | `canManageRh` | Cette fiche (directions/départements/postes) |
| `temps` | `/rh/temps` | Planning, Pointages, Absences & congés, Réconciliation (manage), **Pointeuses** (manage) | `canManageRh \|\| canValidateRhTeam \|\| canAccessRhSelf` | [rh-recrutement-pointeuses.md](rh-recrutement-pointeuses.md) pour le sous-onglet Pointeuses |
| `paie` | `/rh/paie` | Pré-paie, Primes variables, Passerelle DLG PC PAIE, Déclarations DZ, Registres légaux, Conformité DZ | `canManageRh` | [rh-paie-declarations.md](rh-paie-declarations.md) |
| `talents` | `/rh/talents` | **Recrutements**, Formations, Compétences, GPEC & Évaluations, Entretiens | `canManageRh` | [rh-recrutement-pointeuses.md](rh-recrutement-pointeuses.md) pour le sous-onglet Recrutements |
| `validations` | `/rh/validations` | Absences, Pointages, Documents | `canValidateRhTeam \|\| canManageRh` | Cette fiche (centre d'approbations N+1) |
| `mon-espace` | `/rh/mon-espace` | — (contenu direct, pas de sous-onglet) | `canAccessRhSelf` | Cette fiche (profil, demandes et documents personnels) |

Note : pour le hub `temps`, un utilisateur avec seulement `canAccessRhSelf` (ex. réceptionniste) ne voit que les sous-onglets `pointages` et `absences` (ses propres données) ; `reconciliation` et `pointeuse` restent réservés à `canManageRh`.

## 4. Workflows standards

### 4.1 Naviguer depuis le lanceur

1. Ouvrir `/rh` → grille d'applications RH filtrée par rôle.
2. Cliquer sur une carte (ex. « Paie & Légal DZ ») → redirection vers `hubEntryPath`, ex. `/rh/paie/prepaie`.
3. Utiliser la barre d'onglets horizontale pour changer de sous-section sans revenir au lanceur.

### 4.2 Consulter l'organigramme EGT

1. `/rh/organisation/egt`.
2. Filtrer par hôtel si besoin.
3. Vérifier les écarts effectif réel/cible par direction.
4. Exporter en CSV pour partage ou reporting externe.

### 4.3 Gérer une fiche de poste

1. `/rh/fiches-poste`.
2. Filtrer par ID de poste si le référentiel est volumineux.
3. « Nouvelle fiche » → saisir l'ID de poste et le contenu (mission, responsabilités, compétences, indicateurs).
4. Enregistrer — la fiche est versionnée automatiquement côté serveur (`upsertFichePoste`).

## 5. Règles métier DZ

Aucune règle DZ spécifique à l'écran du hub lui-même. Les règles légales algériennes (CNAS, IRG, DADS-U, etc.) sont concentrées dans le sous-module Paie & Légal DZ — voir [rh-paie-declarations.md](rh-paie-declarations.md) — et dans les registres légaux / conformité DZ accessibles depuis le hub `paie`.

## 6. Interconnexions

- **Référentiel des postes** (`/rh/referentiel`) alimente le champ `posteId` utilisé par les fiches de poste (`/rh/fiches-poste`), l'organigramme EGT et le recrutement — voir [rh-recrutement-pointeuses.md](rh-recrutement-pointeuses.md).
- **Effectifs/organigramme EGT** croisent les affectations (`rh_affectations`) et hôtels/unités — voir [administration-utilisateurs.md](administration-utilisateurs.md) pour la gestion des hôtels.
- **Centre validations** (`/rh/validations`) traite les demandes issues des hubs `temps` (pointages, absences) et documents RH, avec compteur de validations en attente affiché sur la sidebar (`getRhSidebarItemsForRole`).
- **Espace personnel** (`/rh/mon-espace`) est le point d'entrée RH pour les rôles non-managers (ex. réceptionniste) — leurs actions (demande d'absence, consultation de pointages) remontent dans les hubs `temps` et `validations` côté RH manager.
- Le hub `paie` comptabilise les bulletins validés en trésorerie — voir [encaissements-tresorerie.md](encaissements-tresorerie.md).

## 7. Dépannage

- **Redirection immédiate vers `/dashboard` en accédant à `/rh`** : l'utilisateur n'a aucune des permissions `rh.manage` / `rh.team` / `rh.self` — vérifier son rôle dans `/admin/roles` (`ROLE_PERMISSIONS` dans `src/shared/permissions.ts`).
- **Un hub ou sous-onglet attendu n'apparaît pas** : sa fonction `visible()` dans `RH_HUBS` (`rhNavigation.ts`) renvoie `false` pour le rôle courant — comparer avec le tableau de la section 3.5.
- **Ancien lien RH ne fonctionne plus (ex. `/rh/employes`)** : ces chemins sont automatiquement redirigés via `RH_LEGACY_REDIRECTS` ; si la redirection échoue, vérifier que le segment existe bien dans cette table.
- **Export CSV organigramme vide** : vérifier qu'un organigramme a été renseigné dans `/rh/referentiel` et que le filtre hôtel n'exclut pas les données attendues.
- **Fiche de poste non enregistrée** : le champ ID poste est obligatoire (`disabled` sur le bouton Enregistrer tant qu'il est vide) — vérifier que le poste existe dans le référentiel.
