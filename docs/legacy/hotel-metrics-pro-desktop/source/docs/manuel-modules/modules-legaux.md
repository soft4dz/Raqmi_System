# Modules légaux (immobilisations, CASNOS, inventaire)

## Présentation

Ce module regroupe trois obligations comptables et sociales distinctes qui n'avaient pas de home fonctionnel dédié : la gestion des **immobilisations et de leurs amortissements** (SCF, classe 2), les **cotisations CASNOS** des travailleurs non salariés (gérants, prestataires, artisans), et l'**inventaire légal annuel** (rapprochement stocks comptables / stocks physiques). Le code le désigne comme « Phase 3 » de la certification Algérie.

Composants (`src/pages/conformite/modules-legaux/`) :
- `ConformiteModulesLegauxIndexPage.tsx` — coquille à onglets
- `ModulesLegauxHubPage.tsx` — tableau de bord (index)
- `ImmobilisationsPage.tsx` — immobilisations & amortissements
- `CasnosPage.tsx` — cotisations CASNOS
- `InventaireLegalPage.tsx` — inventaire légal annuel

Routes : `/conformite/modules-legaux` (hub) et sous-routes `immobilisations`, `casnos`, `inventaire`.

Module réservé à l'administration système / comptabilité. Voir `docs/guides-utilisateurs/01-super-admin.md`, `06-comptabilite-tresorerie.md` et `10-audit-interne.md`.

## Prérequis & accès

- Route protégée par `RequireSystemAdmin` dans `src/routes/AppRoutes.tsx` : `<Route path="/conformite/modules-legaux" element={<RequireSystemAdmin>...}>` (englobe toutes les sous-routes). Redirige vers `/settings` si `canManageUsers(role)` est faux.
- Menu : `src/layouts/sidebarModules.ts`, section `exploitation` → « Modules légaux », visible uniquement si `canManageUsers(role)`.
- **Contrôle backend distinct** : chaque service (`immobilisations.service.ts`, `casnos.service.ts`, `inventaire-legal.service.ts`) utilise sa propre garde `assertCan()` qui vérifie `isGlobalAdminRole(getActorContext(actorUserId).roleCode)` — un rôle « administrateur global », distinct techniquement de la vérification `users.manage` utilisée par le module RGPD, mais avec le même effet pratique de réserver l'accès à l'administration système.
- Immobilisations : dépend du module Comptabilité SCF (génération d'écritures OD) et du référentiel hôtels.
- CASNOS : dépend du référentiel employés/affiliés propre au module (table `casnos_affilies`, indépendante de la table RH `employes`).
- Inventaire légal : dépend du module Stocks (`stock_niveaux`, `stock_produits`) pour l'initialisation d'une session.

## Écrans & champs

### Hub (`ModulesLegauxHubPage.tsx`)

6 cartes indicateurs (`modulesLegaux:dashboard`) :
- Immobilisations actives
- Amortissements à comptabiliser (alerte si > 0)
- Affiliés CASNOS actifs
- Déclarations CASNOS en attente (statut `brouillon` ou `calculee`)
- Inventaires en cours
- Inventaires avec écarts (critique si > 0, écart absolu > 0,01 DA et statut ≠ `valide`)

Rappel affiché : les immobilisations génèrent des écritures d'amortissement en journal OD ; les écarts d'inventaire légal déclenchent un workflow de contrôle interne.

### Immobilisations (`ImmobilisationsPage.tsx`)

Sélecteur de période (mois), bouton « Comptabiliser {période} », bouton Export CSV. Tableau des immobilisations : code, libellé, valeur d'acquisition, durée d'amortissement (mois), bouton « Plan » par ligne. En sélectionnant une immobilisation, affichage du plan d'amortissement généré (période, dotation, VNC — valeur nette comptable, statut `prevu`/`comptabilise`).

Champs du modèle `Immobilisation` : `code`, `libelle`, `categorie` (`corporelle`, `incorporelle`, `financiere`), `dateAcquisition`, `valeurAcquisition`, `valeurResiduelle`, `dureeAmortissementMois` (défaut 60), `methode` (`lineaire` par défaut, `degressif` prévu en base mais non implémenté dans le calcul actuel), `compteImmobilisation` (défaut `218000`), `compteAmortissement` (défaut `281000`), `compteDotation` (défaut `681000`), `hotelId`, `statut` (`actif`, `cede`, `sorti`). **Aucun formulaire de création/édition n'est câblé dans `ImmobilisationsPage.tsx`** — l'IPC `immo:upsert` existe côté backend mais la liste affichée provient des données seedées (une immobilisation d'exemple `IMMO-001` en migration).

### CASNOS (`CasnosPage.tsx`)

Compteur d'affiliés actifs, sélecteur de période, bouton « Calculer période », bouton Export CSV. Tableau des déclarations de la période : affilié, revenu déclaré, cotisation calculée, statut. **Aucun formulaire de gestion des affiliés n'est câblé dans cet écran** — l'IPC `casnos:affilies:upsert` existe côté backend mais n'a pas d'interface associée dans `CasnosPage.tsx`. De même, l'IPC `casnos:declarations:marquerDeclaree` (passage au statut `declaree` avec référence CASNOS) n'est pas exposé dans cet écran.

### Inventaire légal (`InventaireLegalPage.tsx`)

Sélection/saisie de l'exercice (année) et de l'ID unité (`hotelId`, saisi en numérique brut — pas de sélecteur nommé), bouton « Nouvelle session », sélecteur de session existante. Une fois une session sélectionnée : bouton « Clôturer » (actif seulement si statut `en_cours`), bouton Export, trois indicateurs (valeur comptable, valeur physique, écart total), et le tableau des lignes (désignation, quantité comptable, quantité physique, écart). **La saisie de la quantité physique par ligne n'est pas éditable dans cet écran** — l'IPC `inventaireLegal:lignes:update` existe côté backend mais aucun champ de saisie n'est câblé dans `InventaireLegalPage.tsx` (le tableau est en lecture seule).

## Workflows standards

1. **Créer une immobilisation et générer son plan d'amortissement** — création hors IHM actuelle (voir ci-dessus) ; une fois l'immobilisation existante, bouton « Plan » → IPC `immo:genererPlan(immobilisationId)` → `genererPlanAmortissement()` (`electron/services/immobilisations.service.ts`) : calcule une dotation mensuelle linéaire `(valeurAcquisition − valeurResiduelle) / dureeAmortissementMois`, génère une ligne par mois jusqu'à extinction de la valeur amortissable, statut `prevu`.
2. **Comptabiliser les amortissements du mois** — bouton « Comptabiliser {période} » → IPC `immo:comptabiliserMensuel(periode)` → pour chaque ligne `prevu` de la période, génère une écriture comptable via `creerEcriture()` (journal `OD`, débit compte de dotation `681000`, crédit compte d'amortissement `281000`, pièce `AMORT-{code}-{periode}`), puis marque la ligne `comptabilise` avec l'`ecritureId` associé.
3. **Calculer les déclarations CASNOS d'une période** — bouton « Calculer période » → IPC `casnos:declarations:calculerPeriode(periode)` → pour chaque affilié actif, `calculerDeclarationCasnos()` calcule `revenu = revenuDeclare fourni OU revenuAssiette annuel / 12`, puis `cotisation = round(revenu × tauxCotisation) / 100` (taux en %, défaut 15 %) — insertion/mise à jour (`UPSERT` unique par affilié+période), statut `calculee`.
4. **Créer une session d'inventaire légal** — bouton « Nouvelle session » (exercice, unité, date du jour) → IPC `inventaireLegal:sessions:create` → bloque la création si une session `en_cours` existe déjà pour cet exercice/hôtel → initialise une ligne par produit actif en stock (`stock_niveaux` × `stock_produits`), avec quantité physique = quantité comptable par défaut (écart nul à l'ouverture).
5. **Clôturer une session d'inventaire** — bouton « Clôturer » → IPC `inventaireLegal:sessions:cloturer` → passe le statut à `cloture` ; si l'écart total dépasse 0,01 DA en valeur absolue, **déclenche automatiquement un workflow de contrôle interne** (`createWorkflow`, module `inventaire_legal`, priorité `normale`).
6. **Exports CSV** : trois exports disponibles — immobilisations (`immo:exportCsv`), déclarations CASNOS d'une période (`casnos:exportCsv`), lignes d'une session d'inventaire (`inventaireLegal:exportCsv`) — tous câblés dans leurs écrans respectifs.

## Règles métier DZ

- **Immobilisations SCF** : comptes normalisés par défaut conformes au Système Comptable Financier algérien — `218000` (immobilisations corporelles diverses), `281000` (amortissements du matériel, seedé en migration `061_phase3_modules_legaux.sql`), `681000` (dotations aux amortissements). Les écritures d'amortissement sont passées au journal `OD` (opérations diverses).
- **CASNOS (Caisse Nationale de Sécurité Sociale des Non-Salariés)** : cotisations dues par les travailleurs non salariés — types d'affiliés `gerant`, `prestataire`, `artisan`, `autre`. Taux de cotisation par défaut **15 %** (`taux_cotisation REAL NOT NULL DEFAULT 15`), assiette = revenu déclaré ou revenu annuel/12. Cycle de statut des déclarations : `brouillon → calculee → declaree → payee` ; seul le passage jusqu'à `calculee` est actionnable depuis l'IHM actuelle, la déclaration officielle (référence CASNOS) et le paiement restent des étapes manuelles/hors-IHM.
- **Inventaire légal annuel** : obligation comptable de rapprochement entre stocks comptables et stocks physiques constatés en fin d'exercice. Toute session avec écart non nul en fin de clôture déclenche un workflow de contrôle interne obligatoire — traçabilité de l'écart exigée via le champ `motifEcart` par ligne (saisie non exposée dans l'IHM actuelle, voir Dépannage).

## Interconnexions

- **Comptabilité SCF** (`comptabilite-scf.md`) — la comptabilisation mensuelle des amortissements génère des écritures via `creerEcriture()` (service comptabilité), journal OD.
- **Stocks & consommations** (`stocks-consommations.md`) — une session d'inventaire légal est initialisée à partir des niveaux de stock courants (`stock_niveaux`) au moment de sa création.
- **Workflows** (`workflows.md`) — un écart d'inventaire légal à la clôture déclenche automatiquement un workflow de contrôle interne.
- **Données personnelles / Loi 18-07** (`conformite-donnees-personnelles.md`) — les affiliés CASNOS (NIN, NIF) constituent une catégorie de données personnelles sensibles à considérer dans le registre des traitements, bien qu'aucun traitement RGPD dédié « CASNOS » ne soit seedé dans le code actuel.

## Dépannage

- **La page redirige vers `/settings`** : rôle non administrateur global (`isGlobalAdminRole`) — accès réservé à l'administration système, indépendamment de la permission `users.manage` vérifiée côté route React (double contrôle front/back avec deux mécanismes distincts : `canManageUsers` côté route, `isGlobalAdminRole` côté service).
- **Impossible de créer une session d'inventaire** : message « Session inventaire en cours déjà ouverte pour cet exercice et cette unité » — une seule session `en_cours` est autorisée par couple (exercice, hôtel) ; il faut clôturer la session existante avant d'en ouvrir une nouvelle.
- **Le bouton Clôturer reste désactivé** : actif uniquement si la session sélectionnée a le statut `en_cours` (`activeSession?.statut !== 'en_cours'` désactive le bouton) — une session déjà `cloture` ne peut pas être rouverte depuis l'IHM.
- **Aucune quantité physique ne peut être saisie sur une ligne d'inventaire** : l'écran `InventaireLegalPage.tsx` est actuellement en lecture seule sur les lignes (pas de champ éditable), alors que l'IPC `inventaireLegal:lignes:update` le permettrait techniquement — signaler ce manque si la saisie terrain est requise en production.
- **Impossible de créer/éditer une immobilisation ou un affilié CASNOS** : aucun formulaire n'est câblé dans les écrans actuels (`ImmobilisationsPage.tsx`, `CasnosPage.tsx`) malgré la présence des IPC `immo:upsert` et `casnos:affilies:upsert` côté backend — la mise à jour du référentiel passe pour l'instant par script/migration.
- **Point de contrôle audit interne** : toutes les opérations sensibles (création, comptabilisation, clôture, export) sont tracées via `writeAuditLog` (modules `immobilisations`, `casnos`, `inventaire_legal`).
