# Rapprochements

## Présentation

Contrôle quotidien de cohérence entre le chiffre d'affaires déclaré (recettes journalières) et les moyens de paiement effectivement collectés (espèces, TPE, virement, chèque, créance en attente). Le module calcule un écart par journée/hôtel, exige une justification en cas de désaccord, et sert de verrou qualité avant la clôture comptable.

Composant : `src/pages/finance/RapprochementsPage.tsx`. Service backend : `electron/services/finance-reconciliation.service.ts`.

Public cible : Directeur d'unité / Contrôleur exploitation — voir `docs/guides-utilisateurs/06-comptabilite-tresorerie.md` et `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route : `/finance/rapprochements`, sans wrapper de permission au niveau du routeur (`src/routes/AppRoutes.tsx:257`).
- Entrée de menu « Rapprochements » visible uniquement si `canValidateRecettes(role)` (`src/layouts/sidebarModules.ts:90`) — en pratique `DIRECTEUR_UNITE` ou les rôles admin globaux (`SUPERADMIN`, `ADMIN_DEC`).
- Contrôle serveur différencié par action (`finance-reconciliation.service.ts`) :
  - Créer / préremplir : `actorCanAccessHotel` (l'utilisateur doit avoir accès à l'hôtel concerné) — pas de restriction de rôle particulière.
  - Justifier un écart : aucune vérification de rôle explicite au-delà de l'authentification, mais l'action est tracée (`writeAuditLog`).
  - **Valider** : réservé aux rôles admin globaux (`isGlobalAdminRole` → `SUPERADMIN`/`ADMIN_DEC`), sinon erreur « Validation DEC/admin requise. ».

## Écrans & champs

Écran unique (`RapprochementsPage.tsx`) :

1. **Barre d'action** : sélecteur Hôtel, champ Date, bouton « Créer rapprochement ».
2. **Panneau du rapprochement sélectionné** : boutons « Préremplir » et « Valider », zone de texte « Justification de l'écart » + bouton « Justifier l'écart » (actif seulement si le champ n'est pas vide).
3. **Bloc de synthèse** (4 cartes) : CA déclaré, Total rapproché, Écart, Statut.
4. **Liste des rapprochements** : date, hôtel, statut, écart (coloré en orange si ≠ 0), sélection par clic.

Statuts possibles (`ReconciliationStatut`) : `a_controler`, `equilibre`, `ecart_justifie`, `ecart_non_justifie`, `valide`.

## Workflows standards

1. **Création** (`reconciliation:create`) : un rapprochement est créé (ou récupéré s'il existe déjà) pour un couple hôtel/date ; un workflow (`createWorkflow`, module `rapprochement`) lui est associé.
2. **Préremplissage** (`reconciliation:prefill`) : calcule automatiquement
   - `caDeclare` = somme des `recettes_journalieres` non supprimées de la date,
   - `montantEspeces` / `montantTpe` / `montantVirement` / `montantCheque` = encaissements **confirmés** du jour groupés par mode (`especes`, `carte`→TPE, `virement`, `cheque`),
   - `montantCreance` = somme des `global_creances` ouvertes/partielles dont la `date_piece` correspond au jour rapproché,
   - `ecart` = CA déclaré − total rapproché ; statut `equilibre` si `|écart| < 0.01`, sinon `a_controler`.
3. **Justification** (`reconciliation:justify`) : texte obligatoire ; statut basculé à `ecart_justifie` (ou `equilibre` si l'écart est en fait nul).
4. **Validation** (`reconciliation:validate`, admin uniquement) :
   - si le rapprochement est toujours `a_controler` avec un écart ≥ 0,01 DA (c'est-à-dire non justifié), la validation échoue : le statut passe à `ecart_non_justifie`, une **anomalie** (module `finance`, sévérité `elevee`) et une **alerte DEC critique** sont créées automatiquement, puis l'erreur « Écart non justifié — anomalie créée. » est renvoyée ;
   - sinon, le statut passe à `valide`, le workflow associé est approuvé (`approveWorkflow`), et le rapprochement est lié à la clôture journalière correspondante (`daily_closures.reconciliation_id`) si celle-ci existe et n'est pas déjà liée.

## Règles métier DZ

Aucune règle fiscale DZ spécifique à ce module — c'est un contrôle interne de fiabilité du CA déclaré, préalable aux traitements comptables/fiscaux réglementaires réalisés en aval (`comptabilite-scf.md`, `fiscalite-dgi.md`).

## Interconnexions

- **CA journalier (ERP)** (`recettes-journalieres.md`) : source du CA déclaré.
- **Encaissements & trésorerie** (`encaissements-tresorerie.md`) : source des montants par mode de paiement (encaissements au statut `confirme`).
- **Créances & recouvrement** (`creances-recouvrement.md`) : source du montant de créances en attente pour la journée.
- **Clôture journalière** (`recettes-journalieres.md`, table `daily_closures`) : liée automatiquement lors de la validation.
- **Journal des anomalies** (`anomalies.md`) et **Cockpit DEC** (`dec-cockpit.md`) : réception d'une anomalie et d'une alerte critique en cas d'écart non justifié à la validation.
- **Workflows** (`workflows.md`) : chaque rapprochement crée une instance de workflow suivie jusqu'à approbation.

## Dépannage

- **« Écart non justifié — anomalie créée. »** au clic sur Valider : remplir d'abord la zone « Justification de l'écart » et cliquer sur « Justifier l'écart » avant de valider, sinon la validation échoue et génère automatiquement une anomalie + alerte critique.
- **« Accès refusé. »** à la création/préremplissage : l'hôtel sélectionné n'est pas dans le périmètre (`hotelIds`) de l'utilisateur connecté.
- **« Validation DEC/admin requise. »** : seuls les rôles `SUPERADMIN` et `ADMIN_DEC` peuvent valider un rapprochement.
- **Écart qui persiste après préremplissage** : vérifier que tous les encaissements du jour sont bien au statut `confirme` (les encaissements `en_attente` ne sont pas comptés) et que les créances liées portent la bonne `date_piece`.
