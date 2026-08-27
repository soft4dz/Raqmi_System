# Gestion documentaire (GED)

## Présentation

Module de gestion électronique des documents de l'établissement : import, classement par catégorie, recherche, consultation et archivage logique des fichiers (contrats, factures, documents RH, techniques, qualité, juridiques). Les fichiers sont copiés dans un dossier applicatif local et référencés en base ; ils ne sont pas stockés en base elle-même.

Composant : `src/pages/ged/GedPage.tsx`, route `/ged`.

Public : large — utilisé par plusieurs profils pour archiver leurs documents (RH, comptabilité, direction). Voir `docs/guides-utilisateurs/00-manuel-general.md`.

## Prérequis & accès

- Route `/ged` déclarée dans `src/routes/AppRoutes.tsx` **sans garde de permission** particulière (accessible à tout utilisateur authentifié).
- Menu : `src/layouts/sidebarModules.ts`, section `commercial-ged` (« Commercial & documents ») → « Gestion documentaire » (`/ged`), sans condition `visible`.
- Le service backend (`electron/services/ged.service.ts`) ne fait **aucun contrôle de permission** : toute action (liste, upload, archivage, ouverture) n'exige que d'être un utilisateur authentifié (`uid` transmis par `wrapIpcAsync`/`wrapIpc`).
- Exception : la **suppression** d'un document (`ged:delete`) appelle `assertDocumentLegallyProtected(id)`, qui bloque l'opération si le document est sous archivage légal actif (voir `ged-archivage-legal.md`).
- Dépend du référentiel hôtels (filtrage optionnel par `hotelId`, non exposé comme filtre dans l'IHM actuelle) et du référentiel utilisateurs (auteur de l'upload).

## Écrans & champs

Écran unique (`GedPage.tsx`) :

- **Barre de recherche** (titre/description, `search`) et **filtre par catégorie** (`categorieId`, liste `ged:listCategories`).
- **Liste des documents** (`ged:listDocuments`), une carte par document avec :
  - Titre, badge catégorie, badge « Confidentiel » (si `confidentiel`), numéro de version (`v{version}`)
  - Nom de fichier, taille formatée (o/Ko/Mo), auteur de l'upload, date de création
  - Actions : **Ouvrir** (icône lien externe) et **Archiver** (icône archive)
- **Modale « Importer un document »** : titre* (obligatoire), description, catégorie (optionnelle), case « Document confidentiel ». La sélection du fichier physique se fait via une boîte de dialogue système déclenchée **après** validation du formulaire (pas de champ fichier dans le formulaire lui-même).

Catégories prédéfinies (seed, migration `045_gestion_documentaire.sql`) : `contrats` (Contrats & conventions), `factures` (Factures & comptabilité), `rh` (RH & personnel), `technique` (Technique & maintenance), `qualite` (Qualité & normes), `legal` (Juridique & légal), `divers`.

Statuts de document (colonne `statut`) : `actif` (par défaut), `archive`, `supprime` (suppression logique — les documents supprimés sont exclus de la liste par défaut).

## Workflows standards

1. **Importer un document** — bouton « Importer un document » → modale (titre, description, catégorie, confidentiel) → bouton « Sélectionner et importer » → `ipcClient.ged.upload()` → IPC `ged:upload` → `uploadDocument()` (`electron/services/ged.service.ts`) : ouvre une boîte de dialogue native (`Electron.dialog.showOpenDialog`), copie le fichier sélectionné vers le dossier `userData/ged/{uuid}.{ext}` (nom physique anonymisé), puis insère la ligne en base (`ged_documents`) avec taille, tags (JSON), version par défaut `1.0`.
2. **Rechercher / filtrer** — la recherche (`search`) filtre sur `titre LIKE` ou `description LIKE` ; le filtre catégorie est un `AND` supplémentaire. Les requêtes sont automatiquement relancées à chaque changement (React Query, clé `['ged-documents', categorieId, search]`).
3. **Ouvrir un document** — bouton icône lien externe → IPC `ged:ouvrir` → `ouvrirDocument()` → `Electron.shell.openPath(chemin)` : ouvre le fichier avec l'application par défaut du système d'exploitation.
4. **Archiver un document** — bouton icône archive → IPC `ged:archiver` → passe le statut à `archive` (statut simple, à ne pas confondre avec l'« archivage légal » du module `ged-archivage-legal.md`, qui est une opération distincte avec empreinte d'intégrité et durée de rétention).
5. **Supprimer un document** — IPC `ged:delete(id, motif?)` existe côté backend (suppression logique + motif journalisé) mais **n'est câblé par aucun bouton** dans `GedPage.tsx` actuellement. La tentative est de toute façon bloquée si le document est protégé par une archive légale active (`assertDocumentLegallyProtected`).

## Règles métier DZ

Aucune règle métier DZ spécifique à ce module de base — la GED « courante » est un outil de classement documentaire générique. Les obligations réglementaires de conservation s'appliquent au niveau de l'**archivage légal GED** (voir `ged-archivage-legal.md`), qui s'appuie sur les mêmes documents mais ajoute empreinte cryptographique et durée de rétention légale.

## Interconnexions

- **Archivage légal GED** (`ged-archivage-legal.md`) — un document GED peut être placé sous archivage légal (`ged:legal:archive`), ce qui le protège contre la suppression via `assertDocumentLegallyProtected` tant que l'archive reste active.
- **Données personnelles / Loi 18-07** (`conformite-donnees-personnelles.md`) — les politiques de conservation RGPD (`rgpd_politique_conservation`) référencent les politiques de rétention GED (`ged_retention_policy_id`), reliant la classification GED (catégorie `legal`, `rh`, `factures`, `contrats`) aux durées légales de conservation des données personnelles.
- **RH & productivité** (`rh-productivite.md`, `rh-paie-declarations.md`) — le dossier employé RH possède ses propres endpoints GED spécialisés (`rh:ged:modeles`, `rh:ged:dossier`, `rh:ged:scanFolder`, `rh:ged:soumettre` dans `electron/preload.ts`), distincts de ce module générique mais partageant la même logique de protection légale des documents.

## Dépannage

- **Le bouton « Sélectionner et importer » ne fait rien** : le titre est obligatoire (bouton désactivé tant que `form.titre` est vide) ; si la boîte de dialogue système ne s'ouvre pas ou est annulée, l'upload échoue silencieusement côté service (`Aucun fichier sélectionné`) et une notification d'erreur doit apparaître.
- **Un document « archivé » (bouton Archiver) reste supprimable** : le statut `archive` simple n'offre **aucune protection** contre la suppression — seul l'archivage légal (`ged-archivage-legal.md`) bloque la suppression via `assertDocumentLegallyProtected`. Ne pas confondre les deux mécanismes lors d'un contrôle.
- **« Document introuvable » à l'ouverture** : le fichier physique a pu être déplacé ou supprimé du dossier `userData/ged/` en dehors de l'application — la base référence un chemin qui n'existe plus.
- **Point de contrôle audit interne** : l'upload n'est pas explicitement tracé par `writeAuditLog` dans `ged.service.ts` (seules la suppression logique et les opérations d'archivage légal le sont) — pour un audit complet des imports, s'appuyer sur la date de création (`createdAt`) et l'auteur (`uploadedBy`) de chaque document plutôt que sur le journal d'audit seul.
