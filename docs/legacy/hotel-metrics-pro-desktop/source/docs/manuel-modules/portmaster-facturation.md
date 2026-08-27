# PortMaster — contrats, clients, facturation, recouvrement

## 1. Présentation

Ce sous-ensemble de **PortMaster** couvre le cycle commercial et financier du port de plaisance : dossiers clients (armateurs, personnes physiques ou morales), contrats d'amarrage et leurs encaissements, grilles tarifaires par longueur de bateau, facturation (contrat ou hors contrat), file de validation N+1, et recouvrement des créances portuaires (relances).

Le volet référentiel/opérationnel (hub, dashboard, référentiel port, bateaux, emplacements, mouvements) est documenté séparément dans [`portmaster.md`](portmaster.md), à lire en complément.

Route d'entrée principale : `/portmaster/contrats`. Ce module s'adresse au **responsable PortMaster** — voir le guide [`05-responsable-portmaster.md`](../guides-utilisateurs/05-responsable-portmaster.md) pour la vue « tâches quotidiennes » ; cette fiche détaille les écrans, champs et règles de calcul réels.

**Attention à ne pas confondre** : il existe une route distincte `/contrats` (`ContratsHotelPage.tsx`) qui gère les contrats hôteliers B2B (conventions entreprises/agences), totalement indépendante de `/portmaster/contrats`. Cette fiche ne couvre que le périmètre `/portmaster/*`.

## 2. Prérequis & accès

- Toutes les routes sont protégées par `RequirePortmaster` (`src/routes/RequirePortmaster.tsx`) : nécessite `canAccessPortmaster(role)`, c'est-à-dire la permission `portmaster.full` (rôle `RESPONSABLE_PORT`, ou rôles admin globaux `SUPERADMIN`/`ADMIN_DEC`).
- Contrôle serveur systématique dans chaque service (`portmaster.service.ts`, `portmaster-clients.service.ts`, `portmaster-tarifs.service.ts`, `portmaster-factures.service.ts`, `portmaster-validations.service.ts`, `portmaster-recouvrement.service.ts`) via une fonction locale `assertPortmaster(actorUserId)`.
- Dépend du référentiel **Bateaux** et **Emplacements** ([`portmaster.md`](portmaster.md)) : un contrat lie obligatoirement un bateau et un emplacement.
- Dépend du taux de TVA portuaire configurable : clé `port_taux_tva_default` dans `app_settings` (lu/écrit par `electron/services/settings.service.ts`, exposé sous `tauxTvaPort`), valeur par défaut **19 %**, utilisé par `createFacture` si aucun taux n'est explicitement fourni.

## 3. Écrans & champs

### 3.1 Contrats d'amarrage (`/portmaster/contrats`, `ContratsPage.tsx`)
- Filtre par statut : Tous / Actif / Résilié / Expiré.
- Table (`ContratListItem`) : N° contrat, Bateau, Poste (emplacement), Début, Fin, Montant total, Reste dû (surligné si > 0), Statut (badge).
- Action « Nouveau contrat » → `/portmaster/contrats/new` ; icône crayon → `/portmaster/contrats/:id`.

### 3.2 Fiche contrat (`/portmaster/contrats/new`, `/portmaster/contrats/:id`, `ContratFormPage.tsx`)
- En mode édition, 3 tuiles résumé : Montant total, Encaissé, Reste à recouvrer.
- Formulaire : N° contrat\*, Statut (`brouillon` / `soumis` / `valide` / `actif` / `resilie` / `expire`), Bateau\* (options actives), Emplacement\* (emplacements libres + l'emplacement courant en édition), Date début\*, Date fin, Montant mensuel (DZD), Montant total (DZD), Observation.
- Boutons contextuels :
  - **Enregistrer le contrat** → `portmaster:contrats:save`.
  - **Soumettre à validation** (visible seulement si `statut === 'brouillon'`) → `portmaster:contrats:submit`.
  - **Générer une facture** (visible si `statut === 'actif'` ou `'valide'`) → `portmaster:factures:fromContrat`, redirige vers la facture créée.
- Bloc « Enregistrer un encaissement » (visible en édition uniquement) : Date, Montant, Mode (Virement/Chèque/Espèces/Carte), Référence → `portmaster:encaissements:add`, met à jour en direct les compteurs Encaissé/Reste.

### 3.3 Clients portuaires (`/portmaster/clients`, `ClientsPage.tsx`)
- Table (`ClientListItem`) : Client (nom affiché), Type (Personne physique / morale), Téléphone, E-mail, Dossier (badge `complet`=succès, `incomplet`/`a_regulariser`=avertissement, `bloque`=danger), Nombre de bateaux, Créances (montant), action modifier.
- Recherche texte avec debounce 300 ms sur raison sociale/nom/prénom/e-mail/téléphone.
- Bouton « Nouveau client » → `/portmaster/clients/new`.

### 3.4 Fiche client (`/portmaster/clients/new`, `/portmaster/clients/:id`, `ClientFormPage.tsx`)
- Type de client : Personne physique / Personne morale (bascule l'affichage des champs).
- Personne morale : Raison sociale\*, Représentant légal.
- Personne physique : Prénom, Nom\*.
- Champs communs : Téléphone, E-mail, Adresse, Ville, Statut dossier (`incomplet` / `complet` / `a_regulariser` / `bloque`), NIN, NIF, RC, Notes.
- Enregistrement → `portmaster:clients:save` (`SaveClientInput`).
- Le solde de créances (`soldeCreances`) et le type/label d'affichage sont calculés/dérivés côté service, non saisissables directement dans ce formulaire.

### 3.5 Facturation port (`/portmaster/factures`, `FacturesPage.tsx`)
- Filtre statut : Tous / Brouillon / Soumise / Validée / Payée.
- Table (`FactureListItem`) : N°, Client, Date, TTC, Reste, Statut (badge), lien « Détail ».
- Bouton « Hors contrat » → `/portmaster/factures/new` (création manuelle, sans contrat).
- Note : il n'existe pas de bouton de création « depuis un contrat » sur cet écran — la génération depuis contrat se fait exclusivement depuis la fiche contrat (§3.2).

### 3.6 Détail / création facture (`/portmaster/factures/new`, `/portmaster/factures/:id`, `FactureDetailPage.tsx`)
- **Mode création (`new`)** : Client\* (options `clientsOptions`), Montant HT → `portmaster:factures:create` avec `typeFacture: 'hors_contrat'`.
- **Mode détail** : 3 tuiles (TTC, Encaissé, Reste), ligne « Client — Statut » avec lien vers le contrat d'origine si `contratNumero` renseigné.
- Actions contextuelles :
  - **Soumettre à validation** (si `statut === 'brouillon'`) → `portmaster:factures:submit`.
  - **Export PDF** (si `canPrint`, c.-à-d. statut `validee` ou `payee`) → `ipcClient.export.facturePdf(id)`.
  - **Enregistrer paiement** (si statut `validee` ou `payee_partiel`) : champ Montant → `portmaster:factures:addPaiement` (mode de paiement fixé à « Virement » côté formulaire).

### 3.7 Tarification portuaire (`/portmaster/tarifs`, `TarifsPage.tsx`)
- Colonne gauche : liste des tarifs actifs (code + libellé), cliquable pour charger le détail.
- Colonne droite : tranches du tarif sélectionné (longueur min–max en m, montant par période), plus un **simulateur** : saisie d'une longueur (m) → bouton « Simuler » → `portmaster:tarifs:simuler` retourne le montant et la tranche appliquée.
- Pas d'écran de création/édition de tarif visible dans `TarifsPage.tsx` lui-même (le service `saveTarif` existe côté IPC — `portmaster:tarifs:save` — mais aucun formulaire de saisie n'a été trouvé dans ce composant : à considérer comme fonctionnalité back-end disponible sans interface dédiée identifiée, ou gérée ailleurs hors périmètre lu).
- Champs du modèle (`TarifDto`) : code, libellé, type de prestation, montant journalier (fallback si aucune tranche ne correspond), date d'effet, date de fin, actif/inactif, tranches (longueur min/max, montant période).

### 3.8 Validations PortMaster (`/portmaster/validations`, `ValidationsPortPage.tsx`)
- Liste des éléments en attente (`ValidationQueueItem`) : libellé, type d'entité (`facture` ou `contrat`).
- Deux entrées alimentent la file (`listValidationsEnAttente`) :
  - factures au statut `soumise` ;
  - contrats au statut `soumis` ;
  - plus les entrées explicites de la table `port_validations` (statut `en_attente`).
- Actions par ligne : **Valider** (`portmaster:validations:valider`) ou **Rejeter** (`portmaster:validations:rejeter`, motif obligatoire saisi via `window.prompt`).

### 3.9 Recouvrement (`/portmaster/recouvrement`, `RecouvrementPage.tsx`)
- 4 tuiles résumé (`RecouvrementSummary`) : Total créances, tranche 0–30 j, 31–60 j, 60+ j (montants).
- Formulaire « Planifier une relance » : ID facture (optionnel), ID contrat (optionnel), Date, Niveau (1–3), Commentaire → `portmaster:recouvrement:relanceCreate`.
- Table « Créances ouvertes » (`CreanceItem`) : Référence, Type (Facture/Contrat), Client, Reste, Retard (jours), Tranche (badge `danger` si `60+`), bouton « Relancer » (raccourci qui pré-remplit la relance avec la créance sélectionnée).
- Table « Historique relances » (`RelanceListItem`) : Date, Client, Référence, Niveau, Statut, action « Marquer envoyée » (si statut `planifiee`) → `portmaster:recouvrement:relanceEnvoyee`.

## 4. Workflows standards

### 4.1 Créer un contrat d'amarrage et l'activer
1. `Contrats` → « Nouveau contrat ». Saisir numéro, bateau, emplacement, dates, montants, statut.
2. Enregistrer → `portmaster:contrats:save`. Contrôles serveur (`saveContrat`) :
   - numéro de contrat unique (hors doublons supprimés) ;
   - si `statut === 'actif'`, l'emplacement ne doit pas déjà porter un autre contrat actif (erreur « Emplacement déjà occupé par un contrat actif. ») ;
   - le statut de l'emplacement est resynchronisé automatiquement (`syncEmplacementStatut`).
3. Si créé en `brouillon` : bouton « Soumettre à validation » → passe en `soumis`.
4. Un `RESPONSABLE_PORT` (ou admin) valide ensuite depuis `/portmaster/validations` → le contrat passe en `actif`.

### 4.2 Encaisser un paiement sur un contrat
1. Ouvrir la fiche contrat → bloc « Enregistrer un encaissement ».
2. Saisir date, montant (> 0), mode, référence → `portmaster:encaissements:add`.
3. Le montant encaissé s'ajoute à `port_encaissements` (liée au contrat) ; le reste à recouvrer est recalculé côté serveur à chaque lecture (`montant_total − somme des encaissements`), affiché immédiatement dans les tuiles.

### 4.3 Générer une facture depuis un contrat
1. Fiche contrat (statut `actif` ou `valide`) → « Générer une facture ».
2. `portmaster:factures:fromContrat(contratId, tarifId?)` :
   - vérifie que le contrat est actif ou validé ;
   - si un `tarifId` est fourni, recalcule le montant HT via `simulerTarif` sur la longueur du bateau (sinon reprend `montantTotal` du contrat) ;
   - retrouve le client via `port_contrats.client_id` ou, à défaut, `port_bateaux.client_id` (erreur « Aucun client rattaché au contrat. » si aucun des deux n'est renseigné) ;
   - crée la facture en statut `brouillon`, avec `typeFacture: 'contrat'`.
3. Redirection automatique vers le détail de la facture créée.

### 4.4 Créer une facture hors contrat
1. `Factures` → « Hors contrat ».
2. Sélectionner le client, saisir le montant HT → `portmaster:factures:create` (`typeFacture: 'hors_contrat'`).
3. La TVA est calculée automatiquement : `montant_tva = round(montantHt × taux) / 100`, taux = `tauxTva` fourni sinon `port_taux_tva_default` (19 % par défaut). `montant_ttc = montantHt + montant_tva`. Numérotation auto au format `FAC-PORT-{année}-{séquence sur 5 chiffres}`.

### 4.5 Soumettre, valider et encaisser une facture
1. Détail facture (statut `brouillon`) → « Soumettre à validation » → `portmaster:factures:submit` : statut passe à `soumise`, une entrée `port_validations` (`entity_type='facture'`) est créée.
2. Depuis `/portmaster/validations` : **Valider** (`validateFacture` — exige que la facture soit au statut `soumise`, sinon erreur « Facture non soumise. ») → passe à `validee`, ou **Rejeter** (motif obligatoire) → repasse en `brouillon`.
3. Une fois validée (ou partiellement payée), un paiement peut être enregistré (`portmaster:factures:addPaiement`, refusé si la facture n'est ni `validee` ni `payee_partiel` — erreur « Facture non validée — paiement refusé. »). Le statut évolue automatiquement : `payee` si `reste ≤ 0`, `payee_partiel` si un paiement partiel a été fait, sinon inchangé.
4. Une fois `validee`/`payee`, le bouton « Export PDF » devient disponible (`canPrint`).

### 4.6 Piloter le recouvrement
1. `Recouvrement` : consulter les créances ouvertes, triées par ancienneté décroissante — regroupent les **factures** `validee`/`soumise` non soldées et les **contrats** `actif` dont le reste à recouvrer est positif.
2. Bouton « Relancer » sur une créance → planifie automatiquement une relance de niveau 1 rattachée à la facture ou au contrat.
3. Ou formulaire manuel (facture/contrat/date/niveau/commentaire) → `portmaster:recouvrement:relanceCreate`. Le client est déduit automatiquement de la facture ou du contrat si non fourni.
4. Marquer une relance « envoyée » une fois le courrier/appel effectué → `portmaster:recouvrement:relanceEnvoyee` (échoue si la relance n'est plus `planifiee`).

### 4.7 Simuler un tarif d'amarrage
1. `Tarifs port` → sélectionner un tarif dans la liste.
2. Saisir une longueur de bateau (m) → « Simuler » → `portmaster:tarifs:simuler` parcourt les tranches (`longueurMinM`–`longueurMaxM`) et retourne le montant de la première tranche correspondante ; à défaut de tranche applicable, retombe sur `montantJournalier × 30` si défini, sinon montant 0 avec le libellé « Aucune tranche applicable ».

## 5. Règles métier DZ

- **TVA sur factures portuaires** : taux configurable via le paramètre `port_taux_tva_default` (`app_settings`), **19 % par défaut**, appliqué automatiquement à toute facture créée (`createFacture` dans `portmaster-factures.service.ts`) sauf taux explicite fourni en entrée. C'est la seule règle fiscale identifiée dans le code de ce sous-module.
- **Identification client** : le formulaire client distingue personne physique / personne morale et prévoit des champs NIN, NIF et RC — cohérents avec les obligations d'identification fiscale algériennes usuelles — mais aucune validation de format ni contrôle de conformité DGI n'est appliqué côté service (champs texte libres, non obligatoires).
- Aucune autre règle légale/réglementaire portuaire spécifique (redevance domaniale, taxe de plaisance, etc.) n'a été identifiée dans le code — le calcul tarifaire est un barème commercial interne par tranche de longueur, pas une grille réglementaire officielle.

## 6. Interconnexions

- **PortMaster — accueil, référentiel, bateaux, emplacements** ([`portmaster.md`](portmaster.md)) : un contrat lie un bateau du référentiel Bateaux à un emplacement du référentiel Emplacements ; l'activation/résiliation d'un contrat pilote le statut « occupé/libre » de l'emplacement (`syncEmplacementStatut`), répercuté sur le dashboard et le référentiel port.
- **Facturation** ([`facturation.md`](facturation.md)) : module de facturation hôtelière généraliste, distinct des factures PortMaster (tables et numérotation séparées `FAC-PORT-...`). Pas d'écriture croisée automatique identifiée.
- **Créances & recouvrement** ([`creances-recouvrement.md`](creances-recouvrement.md)) : l'écran Recouvrement de ce module gère les créances et relances **spécifiques au port** (`port_relances`, `port_encaissements`) ; c'est un circuit propre à PortMaster, séparé du module Créances généraliste de l'hôtel, bien que conceptuellement équivalent (tranches d'ancienneté 0-30/31-60/60+).
- **Encaissements & trésorerie** ([`encaissements-tresorerie.md`](encaissements-tresorerie.md)) : les encaissements de contrats et paiements de factures portuaires sont stockés dans `port_encaissements`, table dédiée non reliée automatiquement à la trésorerie hôtelière (`tresorerie.*` IPC).
- **Rapports & exports** ([`rapports-exports.md`](rapports-exports.md)) : export PDF d'une facture validée (`ipcClient.export.facturePdf`).
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : chaque création/modification de contrat, client, tarif, facture, encaissement, validation/rejet et relance génère une entrée `writeAuditLog` (module `portmaster`).
- **Synchronisation multi-postes** ([`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)) : la création d'une relance de recouvrement est mise en file via `enqueueSync('port_relance', ...)`.

## 7. Dépannage

- **« Emplacement déjà occupé par un contrat actif. »** : un autre contrat actif utilise déjà cet emplacement — choisir un autre emplacement libre ou résilier l'ancien contrat avant réaffectation.
- **« Ce numéro de contrat existe déjà. »** : le numéro saisi est déjà utilisé par un contrat non supprimé — vérifier la numérotation.
- **« Seuls les contrats en brouillon peuvent être soumis à validation. »** : le bouton « Soumettre » n'est de toute façon affiché que pour les contrats `brouillon` ; cette erreur signale une incohérence d'état (contrat déjà soumis entre-temps par un autre poste, par exemple).
- **« Le contrat doit être actif ou validé pour facturer. »** : tenter de générer une facture depuis un contrat encore en `brouillon`/`soumis` — faire valider le contrat d'abord.
- **« Aucun client rattaché au contrat. »** lors de la génération de facture : ni le contrat ni le bateau associé n'ont de `client_id` renseigné — compléter la fiche bateau/contrat en base, ou créer la facture manuellement en mode « hors contrat » en sélectionnant le client.
- **« Facture non soumise. »** à la validation : la facture n'est pas (ou plus) au statut `soumise` — état à vérifier avant nouvelle tentative.
- **« Facture non validée — paiement refusé. »** : tentative d'encaissement sur une facture encore `brouillon` ou `soumise` — la faire valider d'abord.
- **« Motif de rejet obligatoire. »** : le rejet d'une facture ou d'un contrat exige un motif non vide (saisi via une invite navigateur sur l'écran Validations).
- **« Rattachez la relance à un client, une facture ou un contrat. »** : le formulaire de relance manuelle nécessite au moins l'un des trois identifiants.
- **« Relance introuvable ou déjà traitée. »** : tentative de marquer « envoyée » une relance qui n'est plus au statut `planifiee`.
- **Simulateur de tarif renvoie « Aucune tranche applicable »** : aucune tranche du tarif sélectionné ne couvre la longueur saisie et aucun `montantJournalier` de secours n'est défini — compléter les tranches ou le montant journalier du tarif.
- **Écran Tarifs sans bouton de création visible** : conforme au code lu à date — la mutation `portmaster:tarifs:save` existe côté IPC/service mais aucune interface de saisie n'a été localisée dans `TarifsPage.tsx` ; à vérifier auprès de l'équipe produit si un écran de gestion des tarifs est prévu ailleurs ou à venir.
