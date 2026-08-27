# Synchronisation multi-postes

## 1. Présentation

Écran de supervision et de déclenchement manuel de la synchronisation entre la base SQLite locale d'un poste et une API centrale (optionnelle, à héberger séparément — `npm run server:dev`). Il expose l'état de connexion, la file d'attente des changements en attente d'envoi (`sync_queue`) et permet de lancer un cycle de synchronisation push/pull.

Composant : `src/pages/system/SyncPage.tsx`. Route : `/system/sync`. Guide associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md).

## 2. Prérequis & accès

- Authentification requise. La route est protégée par `RequireSync` (`src/routes/RequireSync.tsx`), qui redirige vers `/dashboard` si `canManageSync(role)` est faux.
- `canManageSync()` (`src/shared/permissions.ts`) s'appuie sur la permission `sync.full` — accordée automatiquement aux rôles SUPERADMIN/ADMIN_DEC (`isAdminRole`), et à tout autre rôle auquel un administrateur l'aurait explicitement attribuée depuis **Rôles et permissions** (`/admin/roles`, module `sync`).
- Côté service, `electron/services/sync.service.ts` revérifie systématiquement l'accès (`assertSync()` : permission `sync.full` ou rôle admin global) avant chaque opération — la protection n'est donc pas contournable même si un appel IPC est déclenché hors de l'UI protégée.
- Le module `synchronisation-multi-postes` est désactivable via **Activation des modules** (`/settings/modules`), sauf s'il fait partie des modules socle — il n'y figure pas (voir [`modules-activation.md`](modules-activation.md)), donc peut être désactivé par un administrateur.

## 3. Écrans & champs

- **Cartes de statut** (si `status` chargé) :
  - État de connexion : « API en ligne »/« API hors ligne » (icône nuage), avec l'URL configurée.
  - « En attente » : nombre d'éléments `pending` dans `sync_queue`.
  - « Échecs » : nombre d'éléments `failed`.
  - « Dernière sync » : horodatage ou « Jamais ».
- **Carte « Configuration »** : champ URL de l'API centrale (éditable), rappel de la commande pour démarrer l'API locale (`npm run server:dev`), identifiant du poste (`deviceId`, UUID généré une fois), bouton « Enregistrer l'URL ».
- **Bouton d'action principal** : « Synchroniser maintenant » (icône rafraîchissement, état de chargement pendant l'exécution).
- **Tableau « File d'attente »** (`sync_queue`) : date de création, type d'entité, action, statut (`synced`/`failed`/`pending`, badge coloré), message d'erreur le cas échéant.

## 4. Workflows standards

**Configurer l'URL de l'API** : saisie → bouton « Enregistrer l'URL » → `ipcClient.sync.updateConfig({ apiBaseUrl })` → `sync:config:update` (`electron/ipc/sync.ipc.ts`) → `updateSyncConfig()` : valide l'URL (`validateSyncApiUrl`, tolérance différente en développement) puis persiste dans `sync_config` (table à une seule ligne, `id = 1`).

**Lancer une synchronisation** : bouton « Synchroniser maintenant » → `ipcClient.sync.run()` → `sync:run` → `runSync()` (`electron/services/sync.service.ts`) :
1. Ping `GET {apiBaseUrl}/api/health` (timeout 5 s). Si hors ligne, journalise un `sync_log` d'erreur et retourne un message « API centrale injoignable ».
2. Sinon, récupère jusqu'à 100 éléments `sync_queue` en statut `pending`/`failed` avec moins de 5 tentatives (`attempts < 5`), et les pousse en un seul appel `POST {apiBaseUrl}/api/sync/push` (en-tête `X-HMP-API-Key`, clé définie par la variable d'environnement `HMP_SYNC_API_KEY` ou une valeur de développement par défaut).
3. Marque chaque élément accepté comme `synced` ; en cas d'échec HTTP, marque **tous** les éléments en attente comme `failed` avec le message d'erreur et incrémente `attempts`.
4. Tente ensuite un `GET {apiBaseUrl}/api/sync/pull?deviceId=...` — le nombre d'éléments reçus est compté (`pulled`) mais **aucune application des changements distants à la base locale n'a été trouvée dans le code actuel** (commentaire `/* pull optionnel phase 8 */`).
5. Met à jour `sync_config.last_sync_at`, journalise un `sync_log`, écrit une entrée d'audit `SYNC` / module `sync`.

**Suivre la file d'attente** : chaque création/modification/suppression d'une entité synchronisable enfile un enregistrement via `enqueueSync()` (appelé depuis d'autres services métier, hors périmètre de ce module).

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module — c'est un mécanisme purement technique d'échange de données entre postes/serveur central.

## 6. Interconnexions

- **Sauvegarde, base de données, santé système** ([`sauvegarde-restauration.md`](sauvegarde-restauration.md)) : `SystemHealthPage` (`/settings/system-health`) affiche un contrôle « Conflits synchronisation » basé sur `sync_conflict_log`, et `SyncPage` propose un raccourci implicite via la sidebar « Système ».
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : chaque synchronisation réussie ou modification de configuration écrit une entrée `audit_log` (module `sync`).
- **Administration & utilisateurs** ([`administration-utilisateurs.md`](administration-utilisateurs.md)) : les rôles avec la permission `sync.full` sont gérés depuis `/admin/roles`.
- Toute la donnée métier (utilisateurs, hôtels, recettes, etc.) est potentiellement source d'entrées dans `sync_queue` ; ce module n'affiche que la file, il ne modifie pas directement les autres modules.
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `synchronisation-multi-postes`) référence aussi les Unités hôtelières comme connectées (le `deviceId` identifie le poste, indépendamment d'un hôtel précis).

## 7. Dépannage

- **« API centrale injoignable »** : vérifier que le serveur API est démarré (`npm run server:dev`) et que l'URL configurée est correcte et joignable depuis ce poste (pare-feu, réseau local).
- **Éléments bloqués en `failed` dans la file** : au-delà de 5 tentatives (`attempts >= 5`), un élément n'est plus repris automatiquement par `runSync()` — un incident manuel (vérification des données, purge) est nécessaire côté base.
- **« Dernière sync : Jamais »** malgré des tentatives : la mise à jour de `last_sync_at` n'a lieu qu'en fin de `runSync()`, donc seulement après un ping réussi — un poste hors ligne ne verra jamais cette date évoluer.
- **Les données créées sur un autre poste n'apparaissent pas localement** : cohérent avec l'état actuel du code — le mécanisme de « pull » compte les changements distants sans les appliquer à la base locale ; la synchronisation descendante n'est pas encore opérationnelle.
- **Route `/system/sync` inaccessible (redirection vers `/dashboard`)** : le rôle connecté n'a pas la permission `sync.full` — la faire attribuer par un SUPERADMIN/ADMIN_DEC via `/admin/roles`, ou se reconnecter avec un compte administrateur.
