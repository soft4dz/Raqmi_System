# Archivage légal GED

## Présentation

Extension de la GED (`ged.md`) qui ajoute une couche de **preuve d'intégrité** et de **durée de rétention légale** aux documents archivés : empreinte SHA-256 du fichier au moment de l'archivage, horodatage, date de fin de rétention calculée selon une politique, et vérification d'intégrité à la demande (recalcul et comparaison du hash). Un document sous archive légale active ne peut plus être supprimé.

Composant : `src/pages/ged/GedArchivageLegalPage.tsx`, route `/ged/archivage-legal`.

Public : direction, comptabilité, audit interne — module de preuve pour les contrôles réglementaires et fiscaux. Voir `docs/guides-utilisateurs/10-audit-interne.md`, `06-comptabilite-tresorerie.md`.

## Prérequis & accès

- Route `/ged/archivage-legal` déclarée dans `src/routes/AppRoutes.tsx` **sans garde de permission** particulière (accessible à tout utilisateur authentifié) — contrairement aux modules `conformite/*` qui sont protégés par `RequireSystemAdmin`.
- Menu : `src/layouts/sidebarModules.ts`, section `commercial-ged` → « Archivage légal GED » (`/ged/archivage-legal`), sans condition `visible`.
- Le service backend (`electron/services/ged-archivage.service.ts`) ne fait **aucun contrôle de permission** explicite sur la lecture des politiques/archives ni sur la vérification d'intégrité.
- Dépend de la GED de base (`ged.md`) : un document doit déjà exister dans `ged_documents` avant de pouvoir être archivé légalement.

## Écrans & champs

Écran unique (`GedArchivageLegalPage.tsx`), deux sections :

### Politiques de rétention

Liste en lecture seule (`gedArchivage.listRetentionPolicies` → IPC `ged:retention:list`) : libellé, code, durée en années, code de catégorie associée. Politiques seedées (migration `058_phase2_controle_hotellerie.sql`) :
- `LEGAL_COMPTA` — Documents comptables et fiscaux (catégorie `factures`, 10 ans)
- `LEGAL_RH` — Bulletins et registres RH (catégorie `rh`, 10 ans)
- `LEGAL_HOTEL` — Fiches police et registres hôteliers (catégorie `legal`, 10 ans)
- `LEGAL_CONTRATS` — Contrats et conventions (catégorie `contrats`, 10 ans)

### Archives légales

Liste des documents déjà archivés légalement (`gedArchivage.listLegalArchives` → IPC `ged:legal:list`, limité aux 200 plus récentes) : titre du document, code de politique appliquée, date d'archivage (`horodatage`), date de fin de rétention (`retentionUntil`), empreinte SHA-256 tronquée affichée en `font-mono`. Bouton **« Vérifier intégrité »** par archive → notification de succès (« conforme ») ou d'échec (« hash non conforme »).

**Il n'existe aucun bouton « Archiver légalement » dans cet écran ni dans `GedPage.tsx`** : l'IPC `ged:legal:archive(documentId, politiqueCode?)` (mappé `gedArchivage.archiveLegally` dans `src/lib/ipcClient.ts` et `src/shared/types/ipc.ts`) existe et est pleinement fonctionnel côté backend, mais n'est déclenché par aucun composant React du dossier `src/pages/`. L'archivage légal d'un document doit donc être initié autrement (script, futur écran, ou appel direct) — ce point est à signaler si le processus métier attendu est de pouvoir archiver légalement un document depuis l'IHM.

## Workflows standards

1. **Archiver légalement un document** (logique backend, non câblée dans l'IHM actuelle) — `archiverLegalement(actorUserId, documentId, politiqueCode = 'LEGAL_COMPTA')` (`electron/services/ged-archivage.service.ts`) :
   - retrouve le document GED et sa catégorie ;
   - résout la politique de rétention par code explicite, ou par défaut celle associée à la catégorie du document (repli sur `divers` si aucune catégorie) ;
   - calcule l'empreinte SHA-256 du fichier physique (repli sur un hash calculé à partir de l'id/titre/horodatage si le fichier est illisible) ;
   - calcule `retentionUntil = horodatage + duree_annees` de la politique ;
   - si une archive `actif` existe déjà pour ce document, la renvoie sans dupliquer ;
   - sinon, crée l'archive (statut `actif`) et bascule le document GED au statut `archive`.
2. **Consulter les archives et politiques** — chargement automatique à l'ouverture de la page (`GedArchivageLegalPage.tsx`).
3. **Vérifier l'intégrité d'une archive** — bouton « Vérifier intégrité » → IPC `ged:legal:verify(archiveId)` → `verifierIntegriteArchive()` : relit le fichier physique, recalcule son SHA-256, le compare au hash enregistré à l'archivage. Renvoie `{ ok: boolean, hashAttendu, hashCalcule? }`. Si le fichier a été altéré, déplacé ou supprimé, la vérification échoue (`ok: false`).
4. **Expiration automatique des archives** — fonction `marquerArchivesExpirees()` existe côté service (bascule au statut `expire` les archives dont `retention_until` est dépassée) ; **aucun appel** à cette fonction n'a été localisé dans le code exploré (ni tâche planifiée, ni IPC dédié) — à vérifier si un job d'arrière-plan l'invoque ailleurs dans l'application avant de considérer l'expiration comme automatique en production.

## Règles métier DZ

- **Durée de rétention par défaut : 10 ans**, conforme aux obligations de conservation des pièces comptables/fiscales et des registres RH/hôteliers en droit algérien (contrainte `duree_annees INTEGER NOT NULL DEFAULT 10 CHECK(duree_annees > 0)`).
- **Preuve d'intégrité** : chaque archive porte une empreinte SHA-256 calculée à l'instant `T` de l'archivage, servant de preuve que le document n'a pas été modifié depuis (valeur probante en cas de contrôle).
- **Protection contre la suppression** : un document sous archive légale active (`statut='actif'`) ne peut pas être supprimé via `ged:delete` — `assertDocumentLegallyProtected()` lève une erreur explicite avec la date de rétention.
- **Cycle de vie de l'archive** : statuts `actif → expire → detruit` (contrainte `CHECK` en base) ; seul le passage à `expire` a une fonction dédiée dans le code (`marquerArchivesExpirees`), le passage à `detruit` n'est pas implémenté dans les services explorés.

## Interconnexions

- **Gestion documentaire (GED)** (`ged.md`) — l'archivage légal s'applique à un document GED existant et modifie son statut (`archive`) ; la suppression logique GED (`ged:delete`) est bloquée pour tout document sous archive active.
- **Données personnelles / Loi 18-07** (`conformite-donnees-personnelles.md`) — les politiques de conservation RGPD (`rgpd_politique_conservation`) référencent explicitement les politiques de rétention GED (`ged_retention_policy_id`) : `CONS_RH`→`LEGAL_RH`, `CONS_COMPTA`→`LEGAL_COMPTA`, `CONS_HOTEL`→`LEGAL_HOTEL`, `CONS_CONTRATS`→`LEGAL_CONTRATS`.
- **Comptabilité SCF** (`comptabilite-scf.md`) et **Conformité hôtelière** (`conformite-hoteliere.md`) — sources documentaires typiques destinées à l'archivage légal (pièces comptables, fiches police), bien qu'aucun appel automatique de `ged:legal:archive` depuis ces modules n'ait été localisé dans le code exploré.

## Dépannage

- **Impossible d'archiver légalement un document depuis l'IHM** : normal en l'état actuel du code — aucun bouton ne déclenche `ged:legal:archive` dans les écrans existants (voir Écrans & champs). Ce n'est pas un bug applicatif mais une fonctionnalité backend non exposée côté interface.
- **La vérification d'intégrité échoue (« hash non conforme »)** : le fichier physique référencé (`chemin` en base) a été modifié, déplacé ou supprimé depuis l'archivage — traiter comme un incident de conformité (fichier potentiellement compromis) et consulter le journal d'audit (`writeAuditLog`, module `ged`) pour retracer l'historique du document.
- **Tentative de suppression d'un document refusée avec un message de protection légale** : comportement attendu — le document doit d'abord sortir de l'archivage légal (fonctionnalité de « déclassement » non trouvée dans le code exploré) avant suppression, ou la suppression doit être définitivement écartée pour ce document.
- **Une archive reste au statut `actif` après la date de rétention** : l'expiration automatique (`marquerArchivesExpirees`) n'a pas de déclencheur identifié dans le code exploré — vérifier si une tâche planifiée l'invoque ailleurs, sinon considérer un contrôle manuel périodique.
- **Point de contrôle audit interne** : l'archivage légal et l'export/registre associé sont tracés via `writeAuditLog` (module `ged`) — utile pour justifier la chaîne de conservation d'une pièce lors d'un contrôle fiscal ou d'un audit ANPDP.
