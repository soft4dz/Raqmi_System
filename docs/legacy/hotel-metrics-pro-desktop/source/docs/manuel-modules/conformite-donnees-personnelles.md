# Données personnelles (loi 18-07)

## Présentation

Module de conformité à la **loi algérienne n° 18-07 du 10 juin 2018** relative à la protection des personnes physiques dans le traitement des données à caractère personnel (autorité de référence : ANPDP). Il couvre les quatre piliers imposés par cette loi : registre des traitements, gestion des consentements, exercice des droits des personnes concernées, et notification des violations de données. Une cinquième vue restitue les politiques de conservation liées à l'archivage légal GED.

Composants (`src/pages/conformite/`) :
- `ConformiteDonneesIndexPage.tsx` — coquille avec onglets et `<Outlet/>`
- `RgpdHubPage.tsx` — tableau de bord (index)
- `RgpdTraitementsPage.tsx` — registre des traitements
- `RgpdConsentementsPage.tsx` — consentements
- `RgpdDemandesPage.tsx` — demandes d'exercice des droits
- `RgpdIncidentsPage.tsx` — incidents / violations de données
- `RgpdConservationPage.tsx` — politiques de conservation

Routes : `/conformite/donnees-personnelles` (hub) et sous-routes `traitements`, `consentements`, `demandes`, `incidents`, `conservation`.

Module sensible réservé à l'administration système. Voir `docs/guides-utilisateurs/01-super-admin.md` et `10-audit-interne.md`.

## Prérequis & accès

- Route protégée par `RequireSystemAdmin` dans `src/routes/AppRoutes.tsx` (englobe l'ensemble des sous-routes `/conformite/donnees-personnelles/*`) : `<Route path="/conformite/donnees-personnelles" element={<RequireSystemAdmin><ConformiteDonneesIndexPage /></RequireSystemAdmin>}>`. `RequireSystemAdmin` (`src/routes/RequireSystemAdmin.tsx`) redirige vers `/settings` si `canManageUsers(role)` est faux.
- Menu : `src/layouts/sidebarModules.ts`, section `exploitation` → « Données personnelles (18-07) », visible uniquement si `canManageUsers(role)`.
- Toutes les fonctions du service backend (`electron/services/rgpd-anpdp.service.ts`) appellent `assertRgpdAdmin(actorUserId)` → `assertPermission(actorUserId, 'users.manage')` : double verrouillage front + back sur la même permission (« gestion des utilisateurs »).
- Dépend du référentiel utilisateurs (permission `users.manage`) et, pour la vue conservation, de la GED (`ged_retention_policies`).

## Écrans & champs

### Hub (`RgpdHubPage.tsx`)

6 cartes indicateurs, alimentées par `rgpd:dashboard` :
- Traitements actifs
- Consentements actifs
- Demandes en cours (alerte visuelle si des demandes sont en retard)
- Demandes en retard (critique si > 0)
- Incidents ouverts
- Incidents critiques (critique si > 0, gravité `grave` ou `critique` et statut `ouvert`/`en_cours`)

Rappel réglementaire affiché : délai de réponse de **30 jours calendaires** aux demandes d'exercice des droits, et évaluation de notification à l'ANPDP pour les incidents graves.

### Registre des traitements (`RgpdTraitementsPage.tsx`)

Liste des traitements actifs (`rgpd:traitements:list`) : libellé, code, finalité, base légale, durée de conservation, responsable de traitement. Bouton « Export registre CSV » (`rgpd:traitements:exportCsv`). **Pas de formulaire de création/édition dans cet écran** — l'IPC `rgpd:traitements:upsert` existe côté backend (avec validation stricte des champs : `code`, `libelle`, `finalite`, `baseLegale`, `categoriesDonnees`, `categoriesPersonnes`, `destinataires`, `dureeConservation`, `mesuresSecurite`, `responsableTraitement`, `sousTraitants`, `transfertHorsAlgerie`) mais n'est appelé par aucun composant React du dossier `src/pages/conformite/` — la création de traitements passe donc uniquement par les données seedées en migration (voir Règles métier DZ).

### Consentements (`RgpdConsentementsPage.tsx`)

Liste + formulaire de création : nom du sujet, finalité, type de sujet (`client`, `employe`, `heberge`, `autre`), date de consentement (par défaut aujourd'hui). Statut affiché (`actif`/autre). L'IPC `rgpd:consentements:revoke` (retrait de consentement) existe côté backend mais n'est pas câblé dans cet écran.

### Demandes de droits (`RgpdDemandesPage.tsx`)

Liste + formulaire de création : type de demande (`acces`, `rectification`, `suppression`, `opposition`, `portabilite`), nom du demandeur, description libre. Chaque carte affiche la date de réception, l'échéance calculée, le statut, et des actions contextuelles :
- Statut `recue` → bouton « Prendre en charge » (passe à `en_cours`)
- Statut `en_cours` → boutons « Traiter » (`traitee`) / « Refuser » (`refusee`)

### Incidents (`RgpdIncidentsPage.tsx`)

Liste + formulaire de déclaration : gravité (`faible`, `moderee`, `grave`, `critique` — couleur de fond associée), nature de l'incident, données concernées (texte libre), case à cocher « Notification ANPDP effectuée ou planifiée ». Chaque carte affiche date incident/détection et, le cas échéant, la mention « ANPDP notifiée ».

### Conservation (`RgpdConservationPage.tsx`)

Tableau en lecture seule des politiques de conservation (`rgpd:conservation:list`) : type de donnée, libellé, durée (mois), code de la politique GED liée, base légale. Rattachées à l'archivage légal GED (Phase 2).

## Workflows standards

1. **Déclarer un traitement** — aucun écran dédié dans l'IHM actuelle ; les traitements sont initialisés par seed SQL (migration `059_phase3_rgpd_loi1807.sql`) et consultables/exportables uniquement.
2. **Enregistrer un consentement** — `RgpdConsentementsPage` → formulaire → IPC `rgpd:consentements:create` → `createConsentement()` (`electron/services/rgpd-anpdp.service.ts`), statut initial `actif`.
3. **Traiter une demande d'exercice de droit** — création (IPC `rgpd:demandes:create`) : le service calcule automatiquement `dateEcheance = dateReception + 30 jours` (`addDays`, `electron/services/rgpd-anpdp.service.ts`) et **déclenche un workflow de contrôle interne** via `createWorkflow()` (module Workflows). Progression du statut via `rgpd:demandes:update(id, statut, reponse?)` : `recue → en_cours → traitee | refusee`. Le passage à `traitee`/`refusee` fixe `dateTraitement`.
4. **Déclarer un incident de données** — `RgpdIncidentsPage` → formulaire → IPC `rgpd:incidents:create`. Le tableau de bord recalcule aussitôt les compteurs « incidents ouverts »/« incidents critiques ». L'IPC `rgpd:incidents:update` permet de faire évoluer le statut (`ouvert → en_cours → clos`), les mesures correctives et la notification ANPDP, mais cette mise à jour n'est pas câblée dans `RgpdIncidentsPage.tsx` actuellement.
5. **Exporter le registre des traitements** — bouton dédié dans `RgpdTraitementsPage.tsx` → IPC `rgpd:traitements:exportCsv`.

## Règles métier DZ

- **Base légale du traitement** (art. 30 loi 18-07) : chaque traitement doit préciser une base légale parmi `consentement`, `contrat`, `obligation_legale`, `interet_legitime`, `mission_publique` (contrainte `CHECK` en base, migration `059_phase3_rgpd_loi1807.sql`).
- **Registre pré-rempli** : quatre traitements hôteliers types sont seedés à l'installation — `RH_PAIE` (gestion RH et paie, base `obligation_legale`, conservation 10 ans après fin de contrat), `CLIENT_FACT` (relation client/facturation, base `contrat`, conservation 10 ans), `HEBERG_FICHE_POLICE` (fiche police hébergement, base `obligation_legale`, conservation 5 ans registre hôtelier), `VIDEO_SURV` (vidéosurveillance, base `interet_legitime`, conservation 30 jours sauf incident).
- **Délai de réponse aux demandes d'exercice des droits : 30 jours calendaires**, calculé automatiquement à la création de la demande et affiché sur le hub (compteur « demandes en retard » si la date d'échéance est dépassée et le statut encore `recue`/`en_cours`).
- **Notification des violations de données (art. 41)** : chaque incident porte un indicateur `notificationAnpdp` (booléen) et une date de notification optionnelle ; le hub distingue les incidents « critiques » (gravité `grave` ou `critique`, statut ouvert/en cours) pour prioriser l'évaluation de notification à l'ANPDP.
- **Politiques de conservation** liées à l'archivage GED (art. 8, principe de minimisation/durée) : `CONS_RH` (120 mois, RH/paie), `CONS_COMPTA` (120 mois, comptable/fiscal), `CONS_HOTEL` (60 mois, registres hôteliers), `CONS_CONTRATS` (120 mois, contrats et conventions) — chacune référence la politique de rétention GED correspondante (`LEGAL_RH`, `LEGAL_COMPTA`, `LEGAL_HOTEL`, `LEGAL_CONTRATS`).

## Interconnexions

- **Administration & utilisateurs** (`administration-utilisateurs.md`) — l'accès à tout ce module dépend de la permission `users.manage`, gérée dans le module rôles/permissions.
- **Workflows** (`workflows.md`) — la création d'une demande de droit déclenche automatiquement un workflow de suivi (`createWorkflow`).
- **Conformité hôtelière** (`conformite-hoteliere.md`) — le traitement `HEBERG_FICHE_POLICE` documente le registre de police tenu par ce module.
- **GED / Archivage légal GED** (`ged-archivage-legal.md`) — les politiques de conservation RGPD renvoient vers les politiques de rétention GED (`ged_retention_policy_id`).
- **Journal d'audit** (`journalisation-tracabilite.md`) — toute création/mise à jour (traitements, consentements, demandes, incidents) est journalisée (`writeAuditLog`, module `rgpd`).

## Dépannage

- **La page redirige vers `/settings`** : l'utilisateur connecté n'a pas la permission `users.manage` — seuls les rôles avec droits de gestion des utilisateurs (super administrateur) accèdent à ce module (`RequireSystemAdmin`).
- **Impossible de créer/modifier un traitement depuis l'IHM** : aucun formulaire n'est câblé pour `rgpd:traitements:upsert` dans les écrans actuels ; le registre affiché correspond aux traitements seedés par migration. Toute évolution du registre nécessite soit un script de migration, soit le développement d'un formulaire dédié.
- **Une demande apparaît « en retard » alors qu'elle vient d'être traitée** : le compteur du hub est recalculé côté serveur à chaque requête (`demandesEnRetard`) — vérifier que le statut a bien été mis à `traitee`/`refusee` (la requête ne compte que `recue`/`en_cours` avec échéance dépassée) et que les caches React Query (`rgpd-dashboard`) ont été invalidés (ils le sont automatiquement après `updateStatut`).
- **Incident non réévalué après ajout de mesures correctives** : `RgpdIncidentsPage.tsx` n'affiche pas de formulaire de mise à jour ; l'IPC existe (`rgpd:incidents:update`) mais reste à câbler côté IHM.
- **Point de contrôle audit interne** : les six catégories d'objets (traitements, consentements, demandes, incidents) sont toutes tracées via `writeAuditLog` module `rgpd` — recoupement utile pour vérifier l'exhaustivité du registre lors d'un contrôle ANPDP.
