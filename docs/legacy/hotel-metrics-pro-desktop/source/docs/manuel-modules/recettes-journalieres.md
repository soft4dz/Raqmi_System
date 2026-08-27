# CA journalier (ERP)

## 1. Présentation

Le module **CA journalier (ERP)** consolide automatiquement le chiffre d'affaires quotidien de chaque unité hôtelière (hébergement, restauration/POS, autres prestations facturées) et pilote le cycle de validation puis de **clôture journalière** qui verrouille la journée. Depuis la refonte ERP, **il n'y a plus de saisie manuelle du CA** : les montants proviennent des modules Hébergement, POS et Facturation.

4 routes/écrans composent ce module :
- `/recettes/journalieres` — consultation du CA consolidé du jour (`src/pages/recettes/SaisieJournalierePage.tsx`)
- `/recettes/historique` — historique groupé par jour/hôtel avec correction ponctuelle (`src/pages/recettes/HistoriqueRecettesPage.tsx`)
- `/recettes/validation` — file d'attente de validation des journées « soumises » (`src/pages/recettes/ValidationRecettesPage.tsx`)
- `/recettes/cloture` — clôture journalière formelle multi-étapes (`src/pages/recettes/ClotureJournalierePage.tsx`)

Ce module s'adresse au **directeur d'unité** et au **contrôleur exploitation / DEC** (guides [`03-directeur-unite.md`](../guides-utilisateurs/03-directeur-unite.md), [`04-controleur-exploitation.md`](../guides-utilisateurs/04-controleur-exploitation.md)), ainsi qu'à la **comptabilité/trésorerie** pour le rapprochement (guide [`06-comptabilite-tresorerie.md`](../guides-utilisateurs/06-comptabilite-tresorerie.md)).

**Hors périmètre de cette fiche** : la saisie/verrouillage mensuel (`/recettes/mensuelles`, `SaisieMensuellePage.tsx`) est documentée dans [`budget-previsions.md`](budget-previsions.md).

## 2. Prérequis & accès

- Menu (`src/layouts/sidebarModules.ts`, section « Exploitation ») :
  - « CA journalier (ERP) » et « Historique recettes » → visibles si `canViewRecettes(role)` (admin, `RECETTES_SAISIE`, `RECETTES_VALIDATE`, ou rôle `PDG`).
  - « Validation recettes » et « Clôture journalière » → visibles si `canValidateRecettes(role)` (permission `recettes.validate`).
- Contrôle serveur (`electron/services/actorContext.ts`) :
  - `assertRecettesValidate` (lecture du CA consolidé, historique) : nécessite `recettes.validate` **ou** `recettes.saisie`, admin global, ou rôle `PDG`.
  - `assertRecettesValidation` (page Validation, valider/refuser une journée) : nécessite `recettes.validate` ou admin global.
  - `assertRecettesSaisie`/admin (modification ou suppression d'une ligne depuis l'historique) : `recettes.saisie` ou admin global.
  - Suppression d'une **journée entière** : réservée aux rôles admin globaux (`isGlobalAdminRole`).
- Chaque hôtel est filtré par `applyActorHotelFilter`/`resolveHotelId` selon le périmètre de l'utilisateur.
- Dépend de : **Hébergement & occupation** (départs de réservation), **Points de vente (POS)** (clôtures caisse), **Facturation** (factures hors réservation) pour la consolidation, et de **Encaissements & trésorerie**/**Créances** pour le calcul de l'écart de caisse en clôture.

## 3. Écrans & champs

### 3.1 CA journalier (`SaisieJournalierePage.tsx`, `/recettes/journalieres`)
- Sélecteurs : Établissement, Date (navigation jour précédent/suivant, raccourci « Aujourd'hui », date max = aujourd'hui).
- Bandeau d'information : « Le chiffre d'affaires est alimenté automatiquement par l'ERP. Aucune saisie manuelle n'est requise. »
- En-tête journée : nom hôtel, badge statut (`brouillon`, `soumis` = « En attente de validation », `valide` = « Journée validée », `refuse`), total CA, badge de complétion (nombre de rubriques renseignées / total).
- Indicateurs opérationnels (lecture seule) : Encaissement HT, Chambres occupées, Nuitées, Couverts restaurant.
- Tableau des rubriques (regroupées par rubrique parente si applicable, avec sous-totaux) : Rubrique, Montant (DZD), Observation — **tout est en lecture seule** (`canEdit={false}` en dur dans le composant).
- Chaque chargement de page déclenche une resynchronisation ERP si la journée n'est pas verrouillée (`getSaisieJournaliere` appelle `syncAllRecettesFromErp` avant de renvoyer les données).

### 3.2 Historique des recettes (`HistoriqueRecettesPage.tsx`, `/recettes/historique`)
- Filtres : Hôtel, Du/Au, Recherche (nom d'établissement).
- Tableau groupé **une ligne par établissement par jour** : Date, Unité, Hébergement, Denrées, Boissons, Autres prestations, Total CA, Encaissement HT, Chambres occ., Nuitées, Couverts, Statut, Actions. Ligne de totaux si plusieurs journées.
- La ventilation Hébergement/Denrées/Boissons/Autres est déterminée par un **filtrage textuel sur le libellé de la rubrique** (`LIKE '%heberg%'`, `%denree%`/`%restaur%`, `%boisson%`/`%bar%`, le reste en « Autres ») — c'est une classification approximative côté service, pas un champ dédié.
- Action « Visualiser »/« Modifier la journée » (icône œil ou crayon) : ouvre le détail des rubriques de la journée.
  - En mode **administrateur**, chaque ligne (y compris sur une journée déjà validée) peut être modifiée (crayon) ou supprimée (corbeille), avec **motif obligatoire** (tracé en audit et dans la table `validations`).
  - En mode **non-admin** avec `canSaisieRecettes`, l'édition n'est possible que si la journée n'est pas déjà `valide`.
- Action admin « Supprimer la journée » : suppression logique de toutes les lignes de la journée, motif obligatoire, irréversible.

### 3.3 Validation des recettes (`ValidationRecettesPage.tsx`, `/recettes/validation`)
- Filtre Hôtel, bouton Actualiser.
- Tableau des journées au statut `soumis` : Date, Hôtel, Total CA, Nombre de lignes, actions **Valider** / **Refuser** (motif obligatoire pour le refus, saisi via une invite navigateur `window.prompt`).
- **Point d'attention factuel** : dans le modèle actuel, les lignes créées par la synchronisation ERP automatique (`syncAllRecettesFromErp`) sont insérées directement au statut `valide` (voir `recettes-auto-sync.service.ts`) ; le passage explicite au statut `soumis` n'est déclenché par aucun endpoint actif identifié dans le code (l'ancienne saisie manuelle qui posait ce statut, `saveSaisieJournaliere`, est désormais désactivée et lève systématiquement une erreur). Cet écran reste fonctionnel mais peut rester vide en usage courant tant qu'aucun mécanisme ne repasse une journée en `soumis`.

### 3.4 Clôture journalière (`ClotureJournalierePage.tsx`, `/recettes/cloture`)
- Sélection Hôtel + Date, bouton « Créer clôture » (`cloture:create`, idempotent : renvoie la clôture existante si déjà créée pour ce couple hôtel/date).
- Bandeau **Statut POS** (si des points de vente existent pour l'hôtel) : liste des points de vente avec badge « Clôturé » / « Session ouverte » / « Non clôturé ». Avertissement si tous les POS ne sont pas clôturés : « La soumission / clôture hôtel est bloquée tant que tous les points de vente ne sont pas clôturés. »
- Barre d'actions séquentielle (visible une fois une clôture sélectionnée) : **Préremplir**, **Soumettre**, **Valider unité**, **Valider DEC**, **Clôturer**.
- Détail de la clôture sélectionnée : CA déclaré, Encaissé, Créances, Écart (cartes), puis un tableau des lignes détaillées (Rubrique, Montant, Source module, Observation) incluant les lignes POS par point de vente et « Statut POS global ».
- Historique des clôtures de l'hôtel : liste cliquable avec date, statut, CA déclaré.
- Statuts de clôture (`DailyClosureStatut`) : `brouillon` → `soumis` → `valide_unite` → `valide_dec` → `cloture` (ou `refuse`).

## 4. Workflows standards

### 4.1 Consultation quotidienne du CA (aucune saisie requise)
1. `/recettes/journalieres` → sélectionner hôtel et date.
2. Les montants affichés sont recalculés à la volée depuis l'ERP tant que la journée n'est pas verrouillée par une clôture (`isDateJournalLocked`). Rien à saisir.

### 4.2 Corriger une ligne dans l'historique
1. `/recettes/historique` → repérer la journée, cliquer sur l'icône crayon (admin) ou œil (autres profils avec droit de saisie).
2. Dans le détail journée, cliquer le crayon d'une ligne éligible (`canEditLine` : ligne existante en base, et — pour un non-admin — journée non `valide`).
3. Modifier montant/observation, **saisir un motif** obligatoire → `recettes:updateLigne`. Le service repasse la ligne au statut `brouillon` et journalise la modification (table `validations` + audit log).
4. Suppression d'une ligne : même principe via `recettes:deleteLigne` (motif obligatoire, suppression logique).

### 4.3 Cycle de validation (page Validation)
1. `/recettes/validation` liste les journées au statut `soumis` (voir remarque 3.3 sur la disponibilité de ce statut dans le flux actuel).
2. **Valider** (`recettes:validerJour`) : passe toutes les lignes `soumis` de la journée à `valide`, enregistre une validation en base, et déclenche `syncEncaissementRecetteJour` (rapprochement avec la trésorerie).
3. **Refuser** (`recettes:refuserJour`, motif obligatoire) : repasse les lignes en `refuse`.

### 4.4 Clôture journalière complète
1. `/recettes/cloture` → sélectionner hôtel/date → « Créer clôture » (statut initial `brouillon`, un workflow `cloture_journaliere` est instancié en parallèle — voir [`workflows.md`](workflows.md)).
2. **Préremplir** (`cloture:prefill`) : resynchronise le CA ERP si non verrouillé, calcule CA déclaré (somme des recettes du jour), Encaissements confirmés, Créances ouvertes/partielles du jour, et l'Écart (`CA − Encaissements − Créances`). Reconstruit la liste détaillée des lignes (y compris statut par point de vente POS).
3. **Soumettre** (`cloture:submit`) : passage à `soumis` — **bloqué si tous les points de vente ne sont pas clôturés** (`assertAllPosClosedForHotel`). Possible uniquement depuis `brouillon` ou `refuse`.
4. **Valider unité** (`cloture:validateUnit`) : passage à `valide_unite`, réservé au périmètre hôtel de l'utilisateur ; requiert le statut `soumis`.
5. **Valider DEC** (`cloture:validateDec`) : passage à `valide_dec`, **réservé aux rôles admin globaux** ; requiert le statut `valide_unite`.
6. **Clôturer** (`cloture:close`) : passage définitif à `cloture` — requiert `valide_dec` et, à nouveau, tous les POS clôturés. Une fois `cloture`, la journée est **verrouillée** (`isDateJournalLocked` retourne vrai) : plus aucune modification de recette n'est possible sur cette date pour cet hôtel (`Journée clôturée. Modification des recettes impossible.`).

## 5. Règles métier DZ

Aucune règle fiscale spécifique n'est appliquée dans ce module : il consolide des montants déjà calculés (TVA, taxe de séjour, etc.) par les modules sources (Hébergement, Facturation — voir [`facturation.md`](facturation.md) et [`hebergement-occupation.md`](hebergement-occupation.md)). La discipline de **clôture journalière séquencée** (soumission → validation unité → validation DEC → clôture, avec blocage tant que les POS ne sont pas clôturés) répond en revanche à une exigence de contrôle interne du groupe, tracée intégralement en audit (table `validations`, `audit_log`).

## 6. Interconnexions

- **Hébergement & occupation** (`/hebergement`, [`hebergement-occupation.md`](hebergement-occupation.md)) : `syncHebergementCaFromErp` alimente la rubrique HEBERGEMENT à partir des réservations en cours/parties et des extras de folio, déclenché au check-out et rejoué à chaque consultation/préremplissage tant que la journée n'est pas verrouillée.
- **Points de vente (POS)** (`/pos`, [`pos-restauration.md`](pos-restauration.md)) : `syncPosCaToRecettesJournalieres` (service `pos-recettes-sync.service.ts`) alimente les rubriques restauration/boissons ; le statut de clôture des POS conditionne la soumission et la clôture définitive de la journée hôtel.
- **Facturation** (`/facturation`, [`facturation.md`](facturation.md)) : `syncAutresCaFromErp` alimente la rubrique AUTRES à partir des factures validées/payées du jour hors réservation hébergement.
- **Encaissements & trésorerie** (`/encaissements`) et **Créances** (`/creances`) : utilisés pour calculer l'écart de caisse en clôture (`prefillDailyClosure`) et pour le rapprochement déclenché à la validation d'une journée (`syncEncaissementRecetteJour`).
- **Workflows** (`/workflows`, [`workflows.md`](workflows.md)) et **Cockpit DEC** (`/dec/cockpit`) : chaque clôture journalière crée/pilote un workflow `cloture_journaliere`, et une alerte DEC est levée si une unité n'a pas clôturé après 9h30 (`checkClosureDeadlineAlerts`).
- **Budget & prévisions — Saisie mensuelle** (`/recettes/mensuelles`, [`budget-previsions.md`](budget-previsions.md)) : la consolidation mensuelle (`getRecetteMensuelle`) part des lignes journalières `soumis`/`valide`/`validated` de ce module.

## 7. Dépannage

- **« Journée clôturée. Modification des recettes impossible. »** : la date est verrouillée par une clôture au statut `cloture` — aucune correction n'est possible sur cette journée pour cet hôtel, y compris pour un admin.
- **« Recette validée — modification/suppression interdite. Seul un administrateur peut... »** : une ligne au statut `valide` ne peut être touchée que par un rôle admin global.
- **« Aucune recette soumise pour ce jour. »** (à la validation) : aucune ligne au statut `soumis` n'existe pour l'hôtel/la date — voir la remarque du §3.3 sur la disponibilité effective du statut `soumis` dans le flux ERP actuel.
- **« La soumission / clôture hôtel est bloquée tant que tous les points de vente ne sont pas clôturés. »** : passer par le module POS pour clôturer les caisses ouvertes avant de soumettre/clôturer la journée.
- **« Soumission impossible. »** / **« Validation unité : statut soumis requis. »** / **« Validation DEC : statut validé unité requis. »** / **« Clôture finale : validation DEC requise. »** : la clôture journalière doit suivre l'ordre strict `brouillon → soumis → valide_unite → valide_dec → cloture` ; toute action hors séquence est rejetée.
- **« Validation DEC réservée aux administrateurs. »** : seul un rôle admin global (`SUPERADMIN`/`ADMIN_DEC`) peut valider au niveau DEC.
- **« Redémarrez l'application Electron pour activer le nouvel endpoint. »** (message affiché dans l'historique si `historiqueGrouped` échoue) : indique un IPC non enregistré côté processus principal — symptôme d'une build/preload désynchronisé, à signaler à l'administrateur système.
- **Écart de caisse en clôture** : `ecart_caisse = CA déclaré − Encaissements confirmés − Créances ouvertes du jour` ; un écart significatif doit être investigué dans les modules Encaissements/Créances avant validation.
- **Motif obligatoire refusé** : toute modification, suppression de ligne, suppression de journée ou refus de validation exige un motif non vide — ces actions échouent silencieusement côté formulaire tant que le champ motif est vide.
