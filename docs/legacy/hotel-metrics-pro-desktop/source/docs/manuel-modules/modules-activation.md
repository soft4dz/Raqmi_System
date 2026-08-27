# Activation des modules

## 1. Présentation

Écran d'administration permettant d'activer ou de désactiver, installation par installation, chacun des modules métier configurables de l'application. Un module désactivé voit ses routes bloquées (redirection vers `/modules`) et disparaît de la navigation. Trois modules « socle » restent toujours actifs et ne peuvent pas être désactivés depuis cet écran.

Composant : `src/pages/system/ModulesAdminPage.tsx`. Route : `/settings/modules`. Référentiel des modules : `src/modules/moduleCatalog.ts` (métadonnées d'affichage : nom, groupe, statut, route, connexions) et `src/shared/constants/configuredModules.ts` (liste des identifiants réellement configurables + liste des modules protégés). Guide associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md).

## 2. Prérequis & accès

- Authentification requise. Route protégée par `RequireSystemAdmin` (`src/routes/RequireSystemAdmin.tsx`) → redirection vers `/settings` si `canManageUsers(role)` est faux (permission `users.manage`).
- Côté service, `listModulesConfigForUser()` et `setModuleEnabledForUser()` (`electron/services/modules.service.ts`) revérifient tous deux `assertPermission(actorUserId, 'users.manage')`.
- La lecture de la liste des modules **activés** (`modules:listEnabled`, utilisée par `RequireModuleEnabled` pour filtrer la navigation de tout utilisateur) est en revanche publique (`wrapIpcPublic`) — accessible sans permission particulière, car elle ne fait que déterminer l'accès aux routes, pas la configuration.
- Les trois modules socle (`administration-utilisateurs`, `parametrage-global`, `journalisation-tracabilite` — `PROTECTED_MODULE_IDS` dans `configuredModules.ts`) affichent un badge « Socle » et leur interrupteur est désactivé dans l'UI ; côté serveur, `setModuleEnabledForUser()` rejette également toute tentative de désactivation de ces identifiants.

## 3. Écrans & champs

- Cartes récapitulatives : « Modules actifs », « Total configurable », « Désactivés » (calculés côté client à partir de la configuration chargée).
- Barre d'outils : recherche par nom/identifiant/groupe, filtres « Tous » / « Actifs » / « Inactifs ».
- Une carte par groupe fonctionnel (`MODULE_GROUPS`, dérivé de `moduleCatalog.ts` — ex. Socle, Finance, Exploitation, Ressources humaines, Contrôle, Pilotage, Système, Juridique & commercial, Spécifique, Système documentaire), listant chaque module avec :
  - Nom, badge de statut (`Opérationnel` / `Socle prêt` / `À développer` — `MODULE_STATUS_LABELS`), badge « Socle » (bouclier) si protégé.
  - Route existante associée (`existingRoute`) ou route générique `/modules/:id`, date de dernière modification si disponible.
  - Interrupteur (`Switch`) actif/inactif, désactivé pendant une mutation en cours ou si le module est protégé.
- Note de bas de page rappelant que les modules « Socle » restent toujours actifs et que la désactivation prend effet immédiatement sur les routes et la sidebar.

## 4. Workflows standards

**Consulter l'état des modules** : chargement automatique (`useModulesConfig()`, React Query, `staleTime` 30 s) → `ipcClient.modules.listConfig()` → `modules:listConfig` (`electron/ipc/modules.ipc.ts`) → `listModulesConfigForUser()` → `listModulesConfig()` (`electron/services/modules.service.ts`) : parcourt `CONFIGURED_MODULE_IDS` (liste figée dans le code) et complète avec l'état enregistré dans la table `modules_config` — un module sans ligne en base est considéré actif par défaut (`isEnabled: true`).

**Activer/désactiver un module** : bascule de l'interrupteur → `useSetModuleEnabled()` → `ipcClient.modules.setEnabled(moduleId, enabled)` → `modules:setEnabled` → `setModuleEnabledForUser()` :
1. Vérifie que l'identifiant fait partie de `CONFIGURED_MODULE_ID_SET`.
2. Refuse la désactivation si le module est protégé (`PROTECTED_MODULE_IDS`).
3. Insère/met à jour la ligne dans `modules_config` (`INSERT ... ON CONFLICT DO UPDATE`).
4. Écrit une entrée d'audit `UPDATE` / module `modules`.
5. Invalide les caches React Query `modules-config` et `modules-enabled`, ce qui rafraîchit immédiatement la sidebar et les gardes de route pour tous les composants montés côté client.

**Application du blocage de route** : à chaque navigation, `RequireModuleEnabled` (`src/routes/RequireModuleEnabled.tsx`) résout l'identifiant de module correspondant au chemin (`resolveModuleIdForPath`) ; si ce module existe dans `CONFIGURED_MODULE_ID_SET` et n'est pas dans la liste des modules activés (`modules:listEnabled`), l'utilisateur est redirigé vers `/modules` avec le nom du module désactivé transmis en état de navigation.

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module — il s'agit d'un mécanisme purement technique de configuration de périmètre fonctionnel par installation.

## 6. Interconnexions

- **Tous les modules métier référencés dans `moduleCatalog.ts`** (32 identifiants dans `CONFIGURED_MODULE_IDS`) : chacun peut être activé/désactivé indépendamment, à l'exception des trois modules socle.
- **Administration & utilisateurs** ([`administration-utilisateurs.md`](administration-utilisateurs.md)) et **Paramétrage global** ([`parametrage-global.md`](parametrage-global.md)) : socle, toujours actifs ; **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : également socle.
- **Sidebar / navigation** (`src/layouts/SidebarNav.tsx`, `sidebarModules.ts`) : consomme la même liste de modules activés pour masquer les entrées de menu correspondant à un module désactivé.
- **Journalisation & traçabilité** : chaque activation/désactivation de module écrit une entrée `audit_log` (module `modules`).
- Le champ `connectedTo` de chaque `ModuleDefinition` dans `moduleCatalog.ts` documente, module par module, les dépendances fonctionnelles déclarées — utile pour anticiper l'impact d'une désactivation (ex. désactiver « Stocks & consommations » impacte potentiellement Achats, Production/fiches techniques, Maintenance et Budget & prévisions selon ce référentiel), mais ces dépendances **ne sont pas appliquées automatiquement** : désactiver un module ne désactive pas en cascade les modules qui en dépendent.

## 7. Dépannage

- **Interrupteur grisé sans explication** : le module est protégé (« Socle ») — passer la souris affiche le badge bouclier ; ces trois modules ne peuvent pas être désactivés par conception.
- **« Module inconnu ou non configurable » lors d'un appel direct** : l'identifiant transmis n'existe pas dans `CONFIGURED_MODULE_ID_SET` — vérifier l'orthographe exacte de l'identifiant dans `moduleCatalog.ts`/`configuredModules.ts`.
- **Un utilisateur est redirigé vers `/modules` de façon inattendue** : le module correspondant à la route visitée vient d'être désactivé par un administrateur — vérifier son état sur `/settings/modules` et le réactiver si nécessaire.
- **Redirection ne se produit pas alors qu'un module est désactivé** : `RequireModuleEnabled` ne s'applique qu'aux routes dont l'identifiant résolu (`resolveModuleIdForPath`) fait partie de `CONFIGURED_MODULE_ID_SET` — les routes hors de ce périmètre (pages système, PortMaster, etc. selon la résolution) ne sont pas concernées par ce garde.
- **Désactiver un module ne bloque pas un module qui en dépend fonctionnellement** : comportement attendu du code actuel — les relations `connectedTo` du catalogue sont informatives uniquement, il n'existe pas de désactivation en cascade ; réactiver manuellement les modules dépendants si un blocage fonctionnel est constaté.
