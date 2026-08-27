# Utilisateurs, hôtels/unités, rôles, rubriques

## 1. Présentation

Module socle d'administration : gestion des comptes utilisateurs, des unités hôtelières (hôtels/sites), des rôles/permissions et des rubriques de recettes (arbre hiérarchique servant à la saisie du CA journalier). C'est le point d'entrée pour tout paramétrage organisationnel de l'installation.

Composants : `src/pages/administration/UsersPage.tsx`, `UserFormPage.tsx`, `HotelsPage.tsx`, `HotelFormPage.tsx`, `RolesPage.tsx`, `RubriquesPage.tsx`. Routes : `/admin/users` (+`/new`, `/:id`), `/admin/hotels` (+`/new`, `/:id`), `/admin/roles`, `/admin/rubriques`.

Guide utilisateur associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md) — ce module est presque exclusivement opéré par les profils SUPERADMIN et ADMIN_DEC.

## 2. Prérequis & accès

- Authentification requise (`RequireAuth`), mot de passe déjà changé (`RequirePasswordChanged`).
- Toutes les routes sont protégées par `RequirePermission` (`src/routes/AppRoutes.tsx`) :
  - `/admin/users`, `/admin/users/new`, `/admin/users/:id`, `/admin/roles` → permission `PERMISSIONS.USERS_MANAGE` (`users.manage`, fonction `canManageUsers` dans `src/shared/permissions.ts`).
  - `/admin/hotels`, `/admin/hotels/new`, `/admin/hotels/:id`, `/admin/rubriques` → permission `PERMISSIONS.HOTELS_MANAGE` (`hotels.manage`, fonction `canManageHotels`).
- `isAdminRole()` (SUPERADMIN ou ADMIN_DEC) a systématiquement toutes les permissions (`hasPermission()`). Les autres rôles n'ont ces permissions que si un administrateur les leur attribue via `/admin/roles`.
- Modification des permissions d'un rôle (`RolesPage`) : réservée aux rôles SUPERADMIN/ADMIN_DEC (`canManageRolePermissions`) — vérifié aussi côté service (`assertCanManageRolePermissions` dans `electron/services/roles.service.ts`).
- Seul un SUPERADMIN peut créer, modifier ou désactiver un compte ayant le rôle SUPERADMIN, ou attribuer ce rôle à un utilisateur (`electron/services/users.service.ts`).
- Le module `administration-utilisateurs` fait partie des « modules socle » (`PROTECTED_MODULE_IDS` dans `src/shared/constants/configuredModules.ts`) : il ne peut pas être désactivé via **Activation des modules** (voir [`modules-activation.md`](modules-activation.md)).

## 3. Écrans & champs

### Utilisateurs (`UsersPage.tsx` / `/admin/users`)
- Tableau : nom + e-mail, rôle (badge, icône couronne si SUPERADMIN), unités (`hotelsLabel` — libellé calculé selon accès mono/multi/toutes-unités), statut (`Actif`/`Inactif`/`En attente d'activation`), actions.
- Barre d'outils : recherche par nom/e-mail, filtre « En attente » (avec compteur `pendingCount`), bouton Actualiser, bouton « Nouvel utilisateur ».
- Actions par ligne : activer un compte en attente (icône `UserCheck`), modifier (icône crayon, lien vers `UserFormPage`), désactiver (icône `UserX`, demande un motif obligatoire via `window.prompt`). Les actions sur un compte SUPERADMIN sont désactivées si l'acteur connecté n'est pas lui-même SUPERADMIN.

### Formulaire utilisateur (`UserFormPage.tsx` / `/admin/users/new`, `/admin/users/:id`)
- Champs : nom complet, e-mail, mot de passe (requis à la création, optionnel en édition — « laisser vide pour ne pas changer »), rôle (liste déroulante alimentée par `roles.listForSelect`, le rôle SUPERADMIN n'est visible que si l'acteur est lui-même SUPERADMIN), case « Accès à toutes les unités », liste à cocher des unités assignées (masquée si « toutes les unités » est coché, exclut l'unité `SIEGE`), case « Compte actif ».
- Validation front : mot de passe requis à la création ; au moins une unité cochée ou « toutes les unités » activé.

### Hôtels / unités (`HotelsPage.tsx` / `/admin/hotels`)
- Tableau : logo (ou pastille avec les 2 premières lettres du code), code, nom, ville, nombre d'utilisateurs rattachés, statut actif/inactif, actions (modifier, désactiver — masqué pour l'unité `SIEGE`).

### Formulaire hôtel (`HotelFormPage.tsx` / `/admin/hotels/new`, `/admin/hotels/:id`)
- Section « Informations générales » : code unité (forcé en majuscules), nom, ville, case « Unité active ».
- Section « Logo de l'unité » (visible en édition seulement) : upload/suppression via sélecteur de fichier natif (PNG/JPG/WEBP/SVG, max 512 Ko), stocké dans `data/logos/hotels/`.
- Section « Rubriques actives » (visible en édition seulement, si des rubriques existent) : cases à cocher groupées par rubrique principale — détermine quelles rubriques de recettes s'appliquent à cette unité (si toutes sont cochées, envoi d'un tableau vide côté API = toutes les rubriques actives s'appliquent automatiquement).

### Rôles et permissions (`RolesPage.tsx` / `/admin/roles`)
- Une carte par rôle : libellé, code, description, nombre d'utilisateurs et de permissions. Rôles SUPERADMIN/ADMIN_DEC affichés avec la mention « Accès total permanent ».
- En mode édition (bouton « Attribuer les permissions », visible seulement si `canEdit` et `role.editable`) : liste de cases à cocher groupées par module (`administration`, `recettes`, `portmaster`, `audit`, `sync`, `rapports`).
- Les rôles SUPERADMIN et ADMIN_DEC ne sont **pas éditables** (`editable: false` côté service) : leurs permissions sont toujours la liste complète des permissions existantes.

### Rubriques de recettes (`RubriquesPage.tsx` / `/admin/rubriques`)
- Arbre hiérarchique deux niveaux (`RubriqueTreeView`) avec glisser-déposer pour réordonner/reparenter (`computeReorder`, `src/lib/rubriqueTree.ts`).
- Formulaire création/édition : code (majuscules, underscores), libellé, ordre de tri, rubrique parente (uniquement des rubriques principales), case active (en édition).
- Actions par ligne : éditer, activer/désactiver, supprimer.

## 4. Workflows standards

**Créer un utilisateur** : `/admin/users/new` → saisie → `ipcClient.users.create` → `users:create` (`electron/ipc/users.ipc.ts`) → `createUser()` (`electron/services/users.service.ts`) :
- Vérifie que seul un SUPERADMIN peut assigner le rôle SUPERADMIN.
- Valide l'e-mail (`isValidEmail`), le nom, la politique de mot de passe (`validatePasswordStrength` : 8 caractères min., 1 majuscule, 1 chiffre, 1 caractère spécial), l'unicité de l'e-mail.
- Hash bcrypt (12 rounds), insertion, puis synchronise les accès hôtels (`syncUserHotelAccess`). Certains rôles (`SUPERADMIN`, `ADMIN_DEC`, `PDG`, `AUDIT_INTERNE`, `COMPTABILITE`) obtiennent l'accès « toutes les unités » par défaut si non précisé.
- Écrit une entrée d'audit `CREATE` / module `administration`.

**Activer un compte en attente** : bouton « Activer le compte » → `ipcClient.users.activatePending` → `users:activatePending` → passe `account_status` à `actif` et `is_active` à 1.

**Désactiver un utilisateur** : motif obligatoire → `ipcClient.users.deactivate` → `users:deactivate` → `deactivateUser()` : refuse l'auto-désactivation, refuse la désactivation du compte système `[REDACTED_LEGACY_ADMIN_EMAIL]`, exige un SUPERADMIN pour désactiver un autre SUPERADMIN ; effectue une suppression logique (`deleted_at`, `sync_status = pending_delete`).

**Créer/modifier un hôtel** : `ipcClient.hotels.create` / `hotels.update` → `hotels:create` / `hotels:update` → vérifie l'unicité du code, met à jour `hotel_rubriques` si des rubriques sont transmises.

**Désactiver un hôtel** : `hotels:deactivate` → `deactivateHotel()` refuse la désactivation du siège (code `SIEGE`) et refuse si des utilisateurs restent rattachés (`hotel_id`).

**Attribuer des permissions à un rôle** : `ipcClient.roles.updatePermissions` → `roles:updatePermissions` → `updateRolePermissions()` : refuse les rôles protégés (SUPERADMIN/ADMIN_DEC), exige au moins une permission, remplace intégralement `role_permissions` en transaction.

**Gérer les rubriques** : create/update/delete/reorder via `ipcClient.rubriques.*` → `electron/ipc/rubriques.ipc.ts` → `electron/services/rubriques.service.ts` : une rubrique parente doit être une rubrique principale (pas de 3ᵉ niveau), suppression impossible si des sous-rubriques ou des recettes journalières/mensuelles y sont déjà rattachées.

## 5. Règles métier DZ

Aucune règle fiscale/légale algérienne spécifique à ce module. La politique de mot de passe et la structure des rubriques sont des règles internes de l'application (pas d'exigence réglementaire DZ identifiée dans le code).

## 6. Interconnexions

- **Paramétrage global** ([`parametrage-global.md`](parametrage-global.md)) : `SettingsPage` (`/settings`) affiche un raccourci « Modules activés » et « Synchronisation » selon les mêmes permissions ; les paramètres de sécurité (tentatives de connexion, verrouillage) y sont partagés avec `/settings/securite`.
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : toute création/modification/désactivation d'utilisateur, d'hôtel, de rôle ou de rubrique écrit une entrée dans `audit_log` (module `administration`), consultable sur `/audit/logs`.
- **Recettes journalières** (`recettes-journalieres.md`) : les rubriques actives par hôtel déterminent les lignes de saisie du CA quotidien ; la suppression d'une rubrique déjà utilisée dans une recette est bloquée.
- **Toutes les pages avec sélecteur d'hôtel** : la liste des unités actives vient de `hotels:list`, filtrée côté service par les droits d'accès de l'acteur (`actorCanAccessHotel`) sauf pour les rôles ayant accès à toutes les unités.
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `administration-utilisateurs`) référence aussi les Tableaux de bord directionnels comme dépendants de ce module (source des filtres par unité/rôle).

## 7. Dépannage

- **« Seul un super-administrateur peut créer/modifier/désactiver ce compte »** : le compte ciblé (ou le rôle demandé) est SUPERADMIN et l'acteur connecté a le rôle ADMIN_DEC ou un autre rôle — connectez-vous avec un compte SUPERADMIN.
- **« Vous ne pouvez pas désactiver votre propre compte »** : protection anti-verrouillage — désactivation à faire par un autre administrateur.
- **« Le compte administrateur système ne peut pas être désactivé »** : le compte `[REDACTED_LEGACY_ADMIN_EMAIL]` est protégé en dur dans `users.service.ts`.
- **« Ce code hôtel existe déjà » / « Cet e-mail est déjà utilisé »** : contrainte d'unicité vérifiée côté service (pas seulement en base) — choisir un code/e-mail distinct.
- **« Le siège ne peut pas être supprimé » / « Des utilisateurs sont rattachés à cet hôtel »** : détacher ou désactiver d'abord les comptes utilisateurs liés avant de désactiver un hôtel.
- **Rôle SUPERADMIN absent de la liste déroulante à la création d'utilisateur** : normal si l'acteur connecté n'est pas lui-même SUPERADMIN (`UserFormPage.tsx` filtre `roles` sur `r.code !== 'SUPERADMIN'`).
- **Rubrique impossible à supprimer** : vérifier qu'elle n'a plus de sous-rubriques (`childCount`) ni de recettes journalières/mensuelles déjà enregistrées avec cette rubrique.
- **Permissions d'un rôle non modifiables** : les rôles SUPERADMIN et ADMIN_DEC sont volontairement non éditables (`editable: false`) — leur périmètre est fixe (accès total).
