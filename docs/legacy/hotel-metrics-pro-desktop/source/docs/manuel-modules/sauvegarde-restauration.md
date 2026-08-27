# Sauvegarde, base de données, santé système

## 1. Présentation

Trois écrans techniques d'administration système : gestion des copies de sécurité de la base SQLite locale (créer, restaurer, supprimer), consultation/maintenance de la base de données (statistiques, contrôle d'intégrité, VACUUM, import legacy MySQL), et tableau de bord de santé globale de l'installation (base de données, licence, sauvegardes, synchronisation, workflows, archives GED).

Composants : `src/pages/system/BackupPage.tsx` (`/settings/backup`), `DatabasePage.tsx` (`/settings/database`), `SystemHealthPage.tsx` (`/settings/system-health`). Guide associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md).

## 2. Prérequis & accès

- Authentification requise. Les trois routes sont protégées par `RequireSystemAdmin` (`src/routes/RequireSystemAdmin.tsx`), qui redirige vers `/settings` si `canManageUsers(role)` est faux — c'est-à-dire la permission `users.manage` (accordée de fait aux rôles SUPERADMIN/ADMIN_DEC).
- Côté service, chaque opération revérifie l'accès : `assertBackupAdmin()` (`backup.service.ts`) et `assertHealthAccess()` (`system-health.service.ts`) exigent tous deux `users.manage` ; `database.service.ts` suit le même principe (`getDatabaseInfo`, `runIntegrityCheck`, `runVacuum`, `importLegacyFromFile`).
- Ces écrans ne figurent pas dans la liste des modules désactivables individuellement dans `src/shared/constants/configuredModules.ts` sous cet intitulé précis ; seul le module global « Sauvegarde & restauration » (`sauvegarde-restauration` dans `moduleCatalog.ts`) est référencé — voir [`modules-activation.md`](modules-activation.md).

## 3. Écrans & champs

### Sauvegarde (`BackupPage.tsx` / `/settings/backup`)
- Carte « Dossier de sauvegarde » : chemin local du dossier (`data/backups/`), rappel technique sur l'API SQLite `backup()`, raccourci vers `/settings/database`.
- Tableau « Sauvegardes disponibles » : nom de fichier, date, taille formatée (`formatBytes`), actions « Restaurer » et supprimer (icône corbeille).
- Bouton « Nouvelle sauvegarde » (avec état de chargement).

### Base de données (`DatabasePage.tsx` / `/settings/database`)
- Carte « Fichier SQLite » : chemin, taille (+ taille du WAL si présent), mode journal, version SQLite, version application, dossiers données/logos, raccourci vers `/settings/backup`.
- Carte « Statistiques » : nombre d'enregistrements par table clé (`tableStats`).
- Carte « Migrations appliquées » : liste défilante des migrations SQL exécutées (nom de fichier, date d'application).
- Carte « Maintenance » : boutons « Contrôle intégrité » (`PRAGMA integrity_check`) et « VACUUM » (compactage, avec confirmation car potentiellement long) ; affichage du résultat détaillé après contrôle.
- Carte « Import legacy (MySQL) » : sélection d'un fichier `.sql` (dump phpMyAdmin), bouton « Importer » (action destructive avec double confirmation — remplace les données métier existantes : hôtels, utilisateurs, recettes, rubriques, objectifs).

### Santé système (`SystemHealthPage.tsx` / `/settings/system-health`)
- Bandeau d'état global (`ok`/`warning`/`critical`), version applicative, date de génération du rapport.
- Liste de contrôles (`SystemHealthCheck[]`), chacun avec code, libellé, statut, message, détail optionnel :
  - `db_file` : présence et taille du fichier SQLite.
  - `db_wal` : taille du journal WAL (avertissement si > 50 Mo).
  - `migrations` : dernière migration appliquée (avertissement si le préfixe numérique semble antérieur à la série `06x`).
  - `license` : état de la licence Raqmi System.
  - `backup` : présence et ancienneté de la dernière sauvegarde (avertissement si aucune, ou si > 7 jours).
  - `sync` : nombre de conflits de synchronisation ouverts.
  - `workflows` : nombre de workflows en attente de validation (avertissement si > 20).
  - `ged_legal` : nombre d'archives légales GED actives.
  - `data_dir` : accessibilité du répertoire de données.
- Bouton « Vérifier intégrité GED » : lance un contrôle par lot (`runGedIntegrityBatch`, 20 archives par défaut) et affiche un résumé succès/échec.

## 4. Workflows standards

**Créer une sauvegarde** : bouton « Nouvelle sauvegarde » → `ipcClient.backup.create()` → `backup:create` (`electron/ipc/backup.ipc.ts`) → `createBackup()` (`electron/services/backup.service.ts`) : exécute `PRAGMA wal_checkpoint(FULL)` puis `db.backup(dest)` (copie SQLite cohérente à chaud), copie également le dossier des logos en compagnon (`_logos`), nomme le fichier `hotel_metrics_local_AAAA-MM-JJ_HHMMSS.db`, écrit une entrée d'audit `BACKUP`.

**Restaurer une sauvegarde** : confirmation utilisateur (« les données actuelles seront remplacées, l'application redémarrera ») → `ipcClient.backup.restore(filename)` → `restoreBackup()` :
1. Valide le nom de fichier (`sanitizeBackupFilename` — doit se terminer par `.db`, pas de `..`) et la taille minimale (`MIN_BACKUP_BYTES = 50 000` octets).
2. Crée automatiquement une sauvegarde de sécurité « pré-restauration » (`pre_restore_...`) avant d'écraser quoi que ce soit.
3. Ferme la connexion à la base, copie le fichier de sauvegarde par-dessus la base active, restaure le dossier de logos compagnon, ré-ouvre la base.
4. Écrit une entrée d'audit `RESTORE`, puis relance l'application (`Electron.app.relaunch()` + `exit(0)`).

**Supprimer une sauvegarde** : confirmation → `ipcClient.backup.delete(filename)` → suppression du fichier `.db` et de son dossier `_logos` compagnon, entrée d'audit `DELETE`.

**Contrôle d'intégrité** : bouton dédié → `ipcClient.database.integrityCheck()` → `database:integrityCheck` → exécute la vérification SQLite native et retourne le détail.

**VACUUM** : confirmation → `ipcClient.database.vacuum()` → compacte le fichier, affiche la taille avant/après.

**Import legacy** : sélection d'un fichier `.sql` (`ipcClient.database.pickImportFile()`) → confirmation explicite → `ipcClient.database.importLegacy(importPath)` → statistiques d'import par table.

**Consulter la santé système** : chargement automatique (`useQuery`) → `ipcClient.systemHealth.get()` → `systemHealth:get` → `getSystemHealth()` agrège les contrôles listés ci-dessus et calcule le pire statut global (`worst()`), écrit une entrée d'audit `READ` / module `system_health`.

## 5. Règles métier DZ

Aucune règle légale/fiscale DZ spécifique. La conservation des sauvegardes (nombre de fichiers à garder, `backupRetentionCount` dans les Paramètres généraux) est un réglage purement technique, sans exigence réglementaire identifiée dans le code — et, comme noté ci-dessous, ce réglage n'est pas encore appliqué automatiquement.

## 6. Interconnexions

- **Paramétrage global** ([`parametrage-global.md`](parametrage-global.md)) : les champs « Sauvegarde automatique activée », heure et rétention sont saisis sur `/settings` mais consommés uniquement comme préférence affichée — voir Dépannage.
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : chaque création/suppression/restauration de sauvegarde et chaque consultation de la santé système génère une entrée `audit_log`.
- **Synchronisation multi-postes** ([`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)) : le contrôle « Conflits synchronisation » de `SystemHealthPage` lit directement `sync_conflict_log`, alimenté par le module de synchronisation.
- **Gestion documentaire / archivage légal GED** (`ged-archivage-legal.md`) : le contrôle « Archives légales GED » et le bouton « Vérifier intégrité GED » appellent `verifierIntegriteArchive()` du service d'archivage GED.
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `sauvegarde-restauration`) référence aussi Synchronisation multi-postes et Administration & utilisateurs comme modules connectés.

## 7. Dépannage

- **« Sauvegarde invalide ou vide » lors d'une restauration** : le fichier fait moins de 50 000 octets (`MIN_BACKUP_BYTES`) — choisir une autre sauvegarde ou en recréer une valide.
- **Aucune sauvegarde automatique ne se déclenche à l'heure configurée dans les Paramètres** : confirmé dans le code — `autoBackupEnabled`/`autoBackupTime` sont des préférences stockées mais **aucun planificateur** ne les exploite dans `electron/main.ts` ni ailleurs. La création de sauvegarde reste une action manuelle sur `/settings/backup`. C'est cohérent avec le contrôle « Sauvegarde » de `SystemHealthPage`, qui avertit après 7 jours sans sauvegarde récente — à surveiller manuellement.
- **Application qui redémarre après une restauration** : comportement normal et volontaire (`Electron.app.relaunch()`), nécessaire pour recharger une base fraîchement remplacée.
- **Import legacy qui écrase des données existantes** : comportement volontaire du dump MySQL/phpMyAdmin — bien lire le message de confirmation avant de continuer, l'opération n'est pas réversible autrement que par restauration d'une sauvegarde antérieure.
- **Alerte « WAL volumineux » sur la santé système** : exécuter `PRAGMA wal_checkpoint(FULL)` (déclenché automatiquement lors d'une sauvegarde) ou lancer un VACUUM depuis `/settings/database`.
- **Alerte « Dernière migration » en warning** : le nom de la dernière migration appliquée ne correspond pas au motif attendu (`06x_...`) — vérifier que toutes les migrations SQL ont bien été appliquées après une mise à jour de l'application.
