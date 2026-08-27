# Journal d'audit & traçabilité

## 1. Présentation

Écran de consultation du journal d'audit de l'application : historique en lecture seule des actions sensibles (création/modification/désactivation d'utilisateurs, d'hôtels, de rôles, sauvegardes, restaurations, synchronisations, etc.), avec recherche et filtrage par module. Sert de point de contrôle interne et de preuve de traçabilité des opérations administratives.

Composant : `src/pages/audit/AuditLogPage.tsx`. Route : `/audit/logs`. Guides associés : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md) et [`10-audit-interne.md`](../guides-utilisateurs/10-audit-interne.md).

## 2. Prérequis & accès

- Authentification requise. Route protégée par `RequirePermission permission={PERMISSIONS.AUDIT_READ}` (`src/routes/AppRoutes.tsx`), soit la permission `audit.read` (`canReadAudit()` dans `src/shared/permissions.ts`).
- `audit.read` est accordée automatiquement aux rôles SUPERADMIN/ADMIN_DEC ainsi qu'aux rôles `AUDIT_INTERNE` et `PDG` (voir `ROLE_PERMISSIONS` dans `src/shared/permissions.ts`) ; tout autre rôle doit se la voir attribuer explicitement via `/admin/roles`.
- Côté service, `listAuditLogs()` (`electron/services/audit.service.ts`) revérifie systématiquement `assertPermission(actorUserId, 'audit.read')`.
- Le module `journalisation-tracabilite` fait partie des « modules socle » (`PROTECTED_MODULE_IDS`) : il ne peut pas être désactivé via **Activation des modules** ([`modules-activation.md`](modules-activation.md)).

## 3. Écrans & champs

Écran unique avec :
- Barre d'outils : champ de recherche libre (description, e-mail utilisateur, action), filtre par module (liste statique dans le composant : `auth`, `administration` — voir Dépannage pour la limite de cette liste), bouton Actualiser.
- Tableau (`AuditLogItem`, `src/shared/types/admin.ts`) : date (formatée), action (badge coloré selon le type — `LOGIN` vert, `CREATE` violet/accent, `UPDATE` orange, `DELETE` rouge, `LOGOUT` gris), module, utilisateur (e-mail + code rôle), description en texte libre.
- Chargement limité à 300 entrées par requête (`limit: 300` côté page ; plafond serveur 500, `Math.min(filters.limit ?? 200, 500)`).

## 4. Workflows standards

**Consulter le journal** : chargement automatique à l'ouverture de la page → `ipcClient.audit.list({ search, module, limit: 300 })` → `audit:list` (`electron/ipc/audit.ipc.ts`) → `listAuditLogs()` (`electron/services/audit.service.ts`) : construit une requête SQL avec conditions optionnelles (`module = ?`, `action = ?`, `description/user_email/action LIKE ?`), triée par `created_at DESC`.

**Rechercher/filtrer** : la saisie dans le champ recherche ou le changement de filtre module redéclenche automatiquement le chargement (effet React sur `search`/`moduleFilter`).

**Écriture d'une entrée d'audit** (déclenchée depuis les autres modules, pas depuis cet écran) : tout service métier peut appeler `writeAuditLog()` avec `userId`, `action` (ex. `CREATE`, `UPDATE`, `DELETE`, `LOGIN`, `BACKUP`, `RESTORE`, `SYNC`, `READ`), `module`, `page`, `description`, et en option les valeurs avant/après (`oldValue`/`newValue`, sérialisées en JSON) — insertion dans la table `audit_log` avec un identifiant `machine_id` (nom de la machine) et `sync_status = 'pending_create'` (l'entrée elle-même est éligible à la synchronisation multi-postes, voir [`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)).

## 5. Règles métier DZ

Aucune obligation légale algérienne de conservation ou de format n'a été identifiée dans le code pour ce journal (pas de délai de rétention configuré, pas de purge automatique — voir Dépannage). Il s'agit d'un journal technique de contrôle interne, sans contrainte réglementaire DZ explicite dans l'implémentation actuelle.

## 6. Interconnexions

- **Toute la plateforme** : la fonction `writeAuditLog()` (`electron/services/audit.service.ts`) est appelée par la quasi-totalité des services métier (administration, sauvegarde, synchronisation, RH, comptabilité, etc.) — ce module en est le point de consultation centralisé.
- **Administration & utilisateurs** ([`administration-utilisateurs.md`](administration-utilisateurs.md)) : source la plus visible d'entrées `module = 'administration'` (création/modification d'utilisateurs, hôtels, rôles, rubriques).
- **Sauvegarde, base de données, santé système** ([`sauvegarde-restauration.md`](sauvegarde-restauration.md)) : actions `BACKUP`/`RESTORE`/`DELETE` (module `system`) journalisées à chaque opération sur `/settings/backup`.
- **Synchronisation multi-postes** ([`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)) : action `SYNC` (module `sync`) à chaque exécution.
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `journalisation-tracabilite`) référence aussi Audit & contrôle interne (`/audit/logs` — c'est la même route/écran) comme module connecté.

## 7. Dépannage

- **Liste de filtre « module » incomplète** : le `<select>` de `AuditLogPage.tsx` ne propose en dur que `auth` et `administration`, alors que les entrées réelles couvrent aussi `system`, `sync`, `notifications`, `system_health`, `modules`, et les modules métier (RH, comptabilité, etc.). Pour retrouver une entrée d'un autre module, utiliser le champ recherche libre plutôt que le filtre déroulant.
- **Aucune entrée n'apparaît malgré des actions récentes** : vérifier que l'action effectuée appelle bien `writeAuditLog()` — certaines lectures (`READ`) ne sont journalisées que pour des écrans spécifiques (ex. consultation de la santé système) ; les simples affichages de liste ne sont généralement pas audités.
- **Volume important / recherche lente** : le plafond serveur est de 500 lignes par requête ; affiner la recherche par mot-clé ou par module pour réduire le volume retourné.
- **Pas de mécanisme de purge/archivage visible** : aucune fonction de suppression des entrées `audit_log` n'a été trouvée dans le code — le journal croît indéfiniment en base locale ; prévoir une politique de rétention manuelle si le volume devient un problème de taille de base (voir [`sauvegarde-restauration.md`](sauvegarde-restauration.md) pour le contrôle de taille du fichier SQLite).
- **Case « Audit activé » dans les Paramètres généraux sans effet visible** : ce paramètre (`auditEnabled`) est stocké mais aucun code n'a été trouvé qui le lit pour conditionner l'écriture des journaux — `writeAuditLog()` s'exécute indépendamment de ce réglage.
