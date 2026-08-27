# Points de vente (POS)

## 1. Présentation

Le module POS gère la caisse restauration/bar de l'hôtel : points de vente (restaurant, bar, room service…), factions (services de la journée), sessions de caisse, tickets, encaissement, clôture de faction (rapport Z) et clôture journalière du point de vente. C'est le seul point d'entrée autorisé pour enregistrer une vente de plat/boisson issue des fiches techniques validées — il alimente ensuite automatiquement le CA journalier hôtel et la comptabilité SCF.

Composant : `src/pages/pos/PosPage.tsx`. Route : `/pos`. Backend : `electron/services/pos.service.ts` (sessions/tickets), `electron/services/pos-cloture.service.ts` (clôtures faction et journée), `electron/services/pos-recettes-sync.service.ts` (synchronisation CA).

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/pos` dans `AppRoutes.tsx`, mais l'accès hôtel est vérifié côté service (`assertHotel` / `actorCanAccessHotel` dans `pos.service.ts` et `pos-cloture.service.ts`).
- Module désactivable via **Administration → Modules activés** (`pos-restauration` figure dans `CONFIGURED_MODULE_IDS`, `src/shared/constants/configuredModules.ts`).
- Nécessite qu'au moins une fiche technique soit validée dans [`production-fiches-techniques.md`](production-fiches-techniques.md) pour pouvoir composer un ticket.
- Un point de vente doit exister (créé dans l'onglet Paramétrage) avant toute ouverture de session.

## 3. Écrans & champs

Trois onglets (`PosPage.tsx`), avec sélecteur d'hôtel et de point de vente.

**Onglet « Paramétrage »** :
- Création d'un point de vente (`PosPointVente`) : code, nom, type (`restaurant`, `bar`, `room_service`, `autre`). À la création, 4 factions par défaut sont générées automatiquement (voir Workflows).
- Liste des factions du point de vente sélectionné (code, nom, heure début/fin).

**Onglet « Caisse »** :
- Bloc « Session faction » : si aucune session ouverte, formulaire (faction, fond de caisse DA) + bouton « Ouvrir session ». Si une session est ouverte : badge « Session ouverte », totaux ventes/espèces/carte, bouton « Nouveau ticket ».
- Bloc « Ticket en cours » : numéro et statut du ticket, lignes (désignation × quantité, montant), total TTC. Si le ticket est en `brouillon` : ajout de ligne (plat parmi les recettes validées + quantité), sélection du mode de paiement (`especes`, `carte`, `cheque`, `virement`), bouton « Encaisser ».

**Onglet « Clôtures »** :
- Clôture faction (rapport Z) : fond théorique espèces affiché (`fondCaisse + totalEspeces`), saisie du fond compté et observations, bouton « Clôturer faction ».
- Clôture journalière : observations, bouton « Clôturer la journée POS », historique des clôtures (`PosClotureJournaliere`) avec date, total ventes, statut.

Un bandeau rappelle la séquence : ouvrir session faction → encaisser tickets → clôturer chaque faction (Z) → clôturer journée POS → clôture journalière hôtel (`/recettes/cloture`).

## 4. Workflows standards

**Ouvrir une session et encaisser** :
1. Sélectionner une faction et un fond de caisse → `pos.openSession` → `pos:sessions:open` → `openSession()` : refuse si la journée hôtel ou POS est déjà clôturée (`isDateJournalLocked`, `isPosJourneeLocked`), refuse s'il existe déjà une session ouverte pour cette faction.
2. « Nouveau ticket » → `pos.createTicket` → crée un ticket `brouillon` numéroté `T{pointVenteId}-{AAAAMMJJ}-{séquence}`.
3. Ajouter des lignes (recette validée uniquement) → `pos.addTicketLigne` → recalcule le total TTC/HT/TVA du ticket (TVA fixe 19 %, `calcTvaFromTtc`).
4. « Encaisser » → `pos.validerTicket` → `validerTicket()` : pour chaque ligne, consomme le stock des ingrédients (`consommerStockRecette`, voir [`production-fiches-techniques.md`](production-fiches-techniques.md)), génère l'écriture comptable de vente restauration (`genererEcritureVenteRestauration`), crée un encaissement (`statut = 'confirme'`), met à jour les totaux de la session par mode de paiement.

**Clôturer une faction (rapport Z)** : `pos.cloturerSession` → `cloturerSessionFaction()` : bloque s'il reste des tickets `brouillon` non traités ; calcule l'écart de caisse (`fondCloture saisi − fondThéorique = fondCaisse + totalEspeces`).

**Clôturer la journée POS** : `pos.cloturerJournee` → `cloturerJourneePos()` : bloque s'il reste des sessions `ouverte` pour la date ; agrège les totaux de toutes les factions du point de vente ; appelle ensuite `syncPosCaToRecettesJournalieres()` pour pousser le CA restauration consolidé vers `recettes_journalieres` (rubrique `RESTAURATION`), observation préfixée `[ERP auto]`.

## 5. Règles métier DZ

- TVA fixe à 19 % appliquée sur chaque ligne de ticket (`TVA_RATE = 19` dans `pos.service.ts`), calculée depuis un prix TTC (`calcTvaFromTtc`).
- Chaque ticket encaissé génère une écriture comptable SCF automatique (`genererEcritureVenteRestauration`, `electron/services/comptabilite.service.ts`) : journal `CA` (espèces) ou `BQ` (autres modes) au débit du compte trésorerie, crédit du compte de CA restauration et, si TVA > 0, crédit du compte TVA collectée.
- La clôture hôtel (Night Audit / `/recettes/cloture`) exige que tous les points de vente actifs de l'hôtel soient clôturés pour la date (`assertAllPosClosedForHotel`, exposée côté page via « Clôture journalière hôtel » et vérifiable via `pos.getHotelClosureStatus`).

## 6. Interconnexions

- **Production & fiches techniques** ([`production-fiches-techniques.md`](production-fiches-techniques.md)) : seules les fiches techniques `valide` apparaissent comme articles vendables ; l'encaissement consomme le stock ingrédient par ingrédient.
- **Stocks & consommations** ([`stocks-consommations.md`](stocks-consommations.md)) : décrémentation automatique à chaque ticket validé.
- **Comptabilité SCF** ([`comptabilite-scf.md`](comptabilite-scf.md)) : écriture de vente générée à la validation du ticket.
- **CA journalier (ERP)** ([`recettes-journalieres.md`](recettes-journalieres.md)) : synchronisation automatique de la rubrique `RESTAURATION` à la clôture journalière du point de vente ; la clôture hôtel (`/recettes/cloture`) exige que tous les PDV soient clôturés au préalable.
- **Encaissements & trésorerie** (`/encaissements`) : chaque ticket encaissé crée une ligne dans `encaissements` (statut `confirme`).

## 7. Dépannage

- **« Journée hôtel clôturée — ouverture session impossible » / « Journée POS clôturée »** : la date de service est déjà verrouillée par une clôture hôtel ou une clôture POS existante — vérifier `/recettes/cloture` ou l'historique des clôtures dans l'onglet Clôtures.
- **« Session déjà ouverte pour cette faction »** : une seule session active par couple (point de vente, faction, date) — retrouver la session existante plutôt que d'en recréer une.
- **« Tickets brouillon en cours — validez ou annulez avant clôture faction »** : purger les tickets en brouillon (encaisser ou, si prévu côté service, annuler via `pos.annulerTicket`) avant de clôturer la faction.
- **« Recette validée requise »** en ajoutant une ligne au ticket : la fiche technique sélectionnée n'est pas encore validée dans [`production-fiches-techniques.md`](production-fiches-techniques.md).
- **CA restauration absent de la clôture journalière hôtel** : vérifier que la clôture journalière POS (onglet Clôtures) a bien été effectuée pour la date — c'est cet événement qui déclenche `syncPosCaToRecettesJournalieres`.
