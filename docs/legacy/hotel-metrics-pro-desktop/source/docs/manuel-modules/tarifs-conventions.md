# Tarifs & conventions

## 1. Présentation

Le module **Tarifs & conventions** définit la politique tarifaire de chaque unité hôtelière : plans tarifaires, composants de prestation (petit-déjeuner, dîner…), formules (RO/BB/HB/FB/AI), grille de prix journalière par type de chambre, promotions et conventions clients (tarifs négociés B2B). Il alimente le calcul de prix utilisé par le module Hébergement lors de la création d'une réservation.

Route d'entrée : `/tarifs`. Composant racine : `src/pages/tarifs/TarifsPage.tsx`.

Ce module s'adresse principalement au **directeur d'unité** et au **contrôleur exploitation / DEC** (guides [`03-directeur-unite.md`](../guides-utilisateurs/03-directeur-unite.md), [`04-controleur-exploitation.md`](../guides-utilisateurs/04-controleur-exploitation.md)) pour la définition de la stratégie tarifaire, et sert en lecture au réceptionniste au moment de la réservation (via l'estimation automatique de prix dans [`hebergement-occupation.md`](hebergement-occupation.md)).

## 2. Prérequis & accès

- Route : `/tarifs`, déclarée dans `src/routes/AppRoutes.tsx` et affichée dans le menu latéral (section « Exploitation », `src/layouts/sidebarModules.ts`) sans restriction de rôle particulière (visible pour tout utilisateur connecté).
- Contrairement au module Hébergement, `TarifsPage.tsx` ne masque aucun onglet selon le rôle côté interface — le contrôle d'accès se fait côté serveur.
- Contrôle serveur : chaque mutation (création de plan/composant/formule/promotion/convention) vérifie `checkHotelAccess(actor, hotelId)` dans `electron/services/tarifs.service.ts`, qui lève `Accès hôtel refusé.` si l'utilisateur n'est pas admin global et que l'hôtel n'est pas dans son périmètre (`actor.hotelIds`).
- Les listes (`listPlans`, `listComposants`, `listFormules`, `listPromotions`, `listConventions`) sont filtrées par `applyActorHotelFilter`.
- Dépend du référentiel **Types de chambres**, géré dans le module Hébergement (onglet Paramétrage, voir [`hebergement-occupation.md`](hebergement-occupation.md)) et du référentiel **Clients** (`/clients`, [`clients.md`](clients.md)) pour les conventions.

## 3. Écrans & champs

5 onglets (`src/pages/tarifs/TarifsPage.tsx`), onglet par défaut : « Grille tarifaire ».

### 3.1 Plans & Formules (`PlansFormules.tsx`)
Un panneau dépliable par hôtel, avec 3 sous-sections :
- **Plans tarifaires** : Code, Libellé, Type (`BASIQUE`, `PROMOTIONNEL`, `PACKAGE`, `GROUPE` — enum `TypePlan`), Priorité, Conditions d'annulation, Conditions de paiement. Suppression impossible si le plan est utilisé dans des tarifs journaliers.
- **Composants** (prestations annexes : PDJ, dîner, minibar…) : Code, Libellé, Mode de calcul (`PAR_NUIT`, `PAR_PERSONNE_NUIT`, `PAR_SEJOUR`, `FIXE` — enum `ModeCalcul`), Prix par défaut (DA). Suppression impossible si utilisé dans une formule.
- **Formules** (RO/BB/HB/FB/AI) : Code, Libellé, Description, liste de composants inclus (avec prix effectif = `prixOverride` si défini, sinon `prixDefaut`). Des boutons de raccourci proposent les formules standard (Room Only, Bed & Breakfast, Half Board, Full Board, All Inclusive).

### 3.2 Grille tarifaire (`GrilleTarifaire.tsx`)
- Filtres : Unité, Plan tarifaire, Formule (« Hébergement seul » par défaut), période Du/Au (défaut : mois courant).
- Grille calendaire : lignes = types de chambres, colonnes = jours de la période. Chaque cellule est éditable en ligne (clic → saisie du prix, Entrée pour valider) via `tarifs:upsertTarif`.
- Cellule fermée à la vente : icône cadenas (`fermetureVente`), non éditable directement dans cette vue.
- **Mode masse** (« Mise à jour en masse ») : applique un même prix de base à plusieurs types de chambres sur toute la période sélectionnée (`tarifs:upsertBulk`).
- Champs du tarif journalier (`TarifJournalier`, non tous éditables depuis cette grille) : `prixBase`, `prixPersonneSupp`, `minSejour`, `maxSejour`, `fermetureVente`, `restrictionArrivee`, `restrictionDepart`.

### 3.3 Promotions (`PromotionsPage.tsx`)
- Liste des promotions (actives en premier, puis inactives grisées) avec bouton toggle actif/inactif et suppression.
- Formulaire de création : Nom*, Date début/fin, Min. séjour (nuits), Type de réduction (`POURCENTAGE` ou `MONTANT_FIXE`), Valeur, Jours applicables (sélection des 7 jours de semaine, vide = tous les jours).
- Champs additionnels du modèle non exposés dans le formulaire simple : `typeChambreIds`, `formulesIds` (ciblage optionnel).

### 3.4 Conventions client (`ConventionsPage.tsx`)
- Liste des conventions (actives en premier) avec expansion pour voir le détail des lignes tarifaires.
- Formulaire : Nom*, Client* (sélection dans le référentiel Clients actifs), Hôtel*, Date début/fin, Priorité, Description, puis un **tableau de lignes tarifaires** (Type de chambre, Type de réduction — `POURCENTAGE` ou `FIXE_PRIX` —, Valeur). Au moins une ligne est obligatoire.
- Note d'interface explicite : « les tarifs conventionnés remplacent le tarif public — aucune promo ne s'applique » (confirmé côté service : une convention active a priorité sur les promotions dans `simulerPrix`).

### 3.5 Simulateur (`SimulateurTarifPage.tsx`)
- Formulaire : Hôtel*, Type de chambre*, Plan tarifaire*, Formule (optionnel), Client (pour tester une convention), dates Arrivée/Départ, Adultes/Enfants.
- Résultat : prix total du séjour, nombre de nuits, convention/promotion appliquée le cas échéant, et détail nuit par nuit (prix base vs prix final).

## 4. Workflows standards

### 4.1 Construire une grille tarifaire pour une saison
1. Onglet **Plans & Formules** : créer (ou vérifier) un plan tarifaire par hôtel (ex. « Standard »).
2. Créer les composants de prestation utilisés (PDJ, dîner…) puis les formules qui les regroupent (BB, HB…).
3. Onglet **Grille tarifaire** : sélectionner hôtel + plan + formule (ou « Hébergement seul »), définir la période, puis saisir cellule par cellule ou via le **mode masse** pour appliquer un tarif uniforme sur toute la période à plusieurs types de chambres d'un coup.

### 4.2 Créer une promotion
1. Onglet **Promotions** → « Nouvelle promotion ».
2. Définir la période, le type/valeur de réduction, les contraintes (min. séjour, jours de la semaine).
3. La promotion est appliquée automatiquement par le simulateur/l'estimation de prix **uniquement si aucune convention client n'est active** sur la même réservation (règle de priorité convention > promotion).

### 4.3 Négocier une convention B2B
1. Onglet **Conventions client** → « Nouvelle convention ».
2. Sélectionner le client (`clients_facturation`), l'hôtel, la période de validité, la priorité (en cas de conventions multiples applicables, la plus prioritaire l'emporte — `ORDER BY cv.priorite DESC` dans `simulerPrix`).
3. Ajouter une ou plusieurs lignes tarifaires par type de chambre (réduction en % ou prix fixe DA), avec formule optionnelle.
4. Une fois créée, la convention s'applique automatiquement à toute réservation de ce client sur cet hôtel via le simulateur.

### 4.4 Simuler un prix avant de réserver
1. Onglet **Simulateur** : renseigner hôtel, type de chambre, plan, dates, occupants et éventuellement un client.
2. Le calcul (`tarifs:simuler` → `simulerPrix`) additionne pour chaque nuit : prix de base de la grille + supplément personnes (au-delà de 2 adultes) + composants de la formule, puis applique la convention active du client si elle existe, sinon la meilleure promotion active, puis totalise sur le séjour.
3. Ce même calcul est utilisé en coulisse par le module Hébergement lors de la création d'une réservation (`estimateReservationPrice`).

## 5. Règles métier DZ

Aucune règle fiscale ou légale algérienne spécifique n'est gérée dans ce module — il s'agit de paramétrage commercial interne (prix, réductions). La TVA n'est appliquée qu'au moment de la facturation effective (voir [`facturation.md`](facturation.md)).

## 6. Interconnexions

- **Hébergement & occupation** (`/hebergement`, [`hebergement-occupation.md`](hebergement-occupation.md)) : consommateur principal — `estimateReservationPrice` appelle `simulerPrix` pour proposer un montant à la création d'une réservation ; le référentiel Types de chambres est géré dans ce module.
- **Clients** (`/clients`, [`clients.md`](clients.md)) : les conventions sont rattachées à un `clientId` du référentiel `clients_facturation`.
- **Facturation** (`/facturation`, [`facturation.md`](facturation.md)) : le prix issu de la grille/formule est celui répercuté sur la ligne de facture générée depuis une réservation (avec TVA 19 % appliquée à ce stade).
- **Administration → Hôtels** : chaque plan/composant/formule/tarif est rattaché à une unité hôtelière (`hotelId`) issue de `ipcClient.hotels.list()`.

## 7. Dépannage

- **« Accès hôtel refusé. »** : création de plan/composant/formule/promotion/convention sur un hôtel hors du périmètre de l'utilisateur — vérifier l'affectation hôtel du compte.
- **« Ce plan est utilisé dans des tarifs journaliers. »** / **« Cette formule est utilisée dans des tarifs journaliers. »** : suppression bloquée — retirer/réassigner les tarifs journaliers concernés avant suppression.
- **« Ce composant est utilisé dans une formule. »** : retirer le composant des formules qui le référencent avant de le supprimer.
- **Grille vide / « Sélectionnez un plan tarifaire pour afficher la grille »** : aucun plan n'a été créé ou sélectionné pour l'hôtel — passer par l'onglet Plans & Formules d'abord.
- **Prix estimé à 0 dans le simulateur ou à la réservation** : aucun tarif journalier saisi dans la grille pour la combinaison hôtel/type de chambre/plan/date demandée — compléter la grille tarifaire.
- **Une promotion créée n'apparaît jamais appliquée** : vérifier qu'aucune convention active n'existe pour ce client sur cet hôtel/cette période (les conventions ont toujours priorité sur les promotions), et que les critères (dates, min. séjour, jours de semaine, type de chambre ciblé) correspondent bien à la réservation testée.
- **Nom et code en majuscules automatiques** : les champs Code (types de chambres, composants, formules, plans) sont normalisés en majuscules côté formulaire/service — ce n'est pas un bug si l'affichage diffère de la saisie initiale en minuscules.
