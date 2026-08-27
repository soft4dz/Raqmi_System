# Hébergement & occupation

## 1. Présentation

Le module **Hébergement & occupation** est le cœur de l'exploitation hôtelière : il gère le référentiel des chambres (types et unités physiques), les réservations, les KPI d'occupation (TO, ADR, RevPAR) et les folios clients (ardoise de séjour). C'est le point d'entrée pour tout ce qui concerne le cycle client de la réservation au départ (check-in/check-out).

Route d'entrée : `/hebergement`. Composant racine : `src/pages/hebergement/HebergementPage.tsx`.

Ce module s'adresse en priorité au **réceptionniste** (guide [`09-receptionniste.md`](../guides-utilisateurs/09-receptionniste.md)) pour la gestion quotidienne des chambres et réservations, ainsi qu'au **directeur d'unité** (guide [`03-directeur-unite.md`](../guides-utilisateurs/03-directeur-unite.md)) pour le suivi des KPI d'occupation et le paramétrage des chambres de son unité.

## 2. Prérequis & accès

- Route : `/hebergement` — déclarée dans `src/routes/AppRoutes.tsx` et le menu latéral (`src/layouts/sidebarModules.ts`, section « Exploitation »), visible pour tout utilisateur connecté (aucune fonction `can...` conditionne son affichage dans le menu).
- L'onglet **Paramétrage** (types de chambres, chambres) n'est visible que si `canManageHotels(role)` (permission `hotels.manage`, réservée aux rôles admin) **ou** `role === 'DIRECTEUR_UNITE'` — voir `HebergementPage.tsx` (`canAdmin`).
- Filtrage multi-hôtel : chaque action serveur passe par `getActorContext(actorUserId)` et `applyActorHotelFilter(...)` (`electron/services/actorContext.ts`), qui restreint les données à la liste d'hôtels affectés à l'utilisateur, sauf pour les rôles admin globaux (`isGlobalAdminRole`).
- Dépendances : le module s'appuie sur **Tarifs & conventions** (`/tarifs`, voir [`tarifs-conventions.md`](tarifs-conventions.md)) pour l'estimation de prix, et déclenche des écritures dans **Facturation** (`/facturation`) et **Conformité hôtelière** (`/hotel-legal`, fiche de police) lors des changements de statut de réservation.

## 3. Écrans & champs

Le module est organisé en 4 onglets (`src/pages/hebergement/HebergementPage.tsx`) :

### 3.1 Tableau de bord (`OccupationDashboard.tsx`)
- Filtres période : « Du » / « Au » (par défaut : 1er du mois → aujourd'hui).
- KPI globaux (calculés par `getOccupationKpis` côté service) : **Taux d'occupation** (TO %), **ADR moyen** (tarif moyen par nuitée vendue), **RevPAR** (revenu par chambre disponible), **Revenu total**.
- Si plusieurs hôtels accessibles : tableau détail par unité (Hôtel, Chambres, Nuitées, TO %, ADR, RevPAR, Arrivées, Départs).
- Si un seul hôtel accessible : cartes détail (Total chambres, Hors service, Nuitées vendues, Arrivées, Départs, Montant payé).

### 3.2 Plan des chambres (`PlanChambres.tsx`)
- Diagramme d'état des chambres (`ChartEtatChambres.tsx`).
- Légende/filtre cliquable par statut avec compteurs : **Libre**, **Occupée**, **Ménage**, **Hors service**.
- Plan groupé par hôtel puis par étage ; chaque carte chambre affiche numéro, étage, type, statut, et des boutons d'action rapide « → [autre statut] » pour changer le statut en un clic (`updateStatut`).

### 3.3 Réservations (`ReservationsPage.tsx`)
- Barre d'outils : recherche (client/chambre), filtre période (Du/Au, défaut aujourd'hui → J+30), bouton « Nouvelle réservation ».
- Tableau : Client, Chambre (n° + type), Période (dates + nombre de nuits/adultes), Statut (badge coloré), Montant (+ n° de facture si générée), menu d'actions.
- Statuts de réservation (`StatutReservation`) : `provisoire`, `confirmee`, `arrivee`, `depart`, `annulee`, `no_show`.
- Actions rapides par ligne : **Check-in** (→ arrivee), **Check-out** (→ depart), **Confirmer**, **Annuler**, **No show**, et bouton dédié « Générer facture » (visible si pas encore facturée et statut ≠ annulée/no_show).
- Modale **Nouvelle réservation** : Unité hôtelière*, Client facturation (sélection dans le référentiel Clients ou saisie manuelle Nom/Prénom/Téléphone), Chambre, Plan tarifaire, Formule (optionnel), Date arrivée*/départ*, Adultes/Enfants, Montant total (auto-estimé via `hebergement:estimatePrice`, éditable), Source (`direct`, `booking`, `expedia`, `airbnb`, `agence`, `autre`), Notes. Le tarif estimé s'affiche en temps réel (debounce 300 ms) en s'appuyant sur la grille tarifaire + conventions/promotions.

### 3.4 Paramétrage (`ParametrageHebergement.tsx`, admin/DIRECTEUR_UNITE uniquement)
- Un panneau dépliable par unité hôtelière.
- **Types de chambres** : Code (majuscule), Libellé, Capacité (pers.), Tarif de base (DA), Description. Suppression impossible si des chambres actives utilisent le type (`deleteTypeChambre` lève une erreur).
- **Chambres** : Numéro, Étage, Type (optionnel), Statut initial (`libre` ou `hors_service`), Description. Suppression (désactivation logique) impossible si la chambre a une réservation active (statut hors `annulee`/`depart`/`no_show`).

## 4. Workflows standards

### 4.1 Créer une réservation
1. Onglet **Réservations** → « Nouvelle réservation ».
2. Choisir l'hôtel, éventuellement un client du référentiel Clients (`ipcClient.facturation.listClients`) ou saisir un nom manuellement.
3. Choisir chambre/plan/formule/dates → le prix est estimé automatiquement (`hebergement:estimatePrice`, qui appelle `estimateReservationPrice` → `simulerPrix` du module Tarifs si un plan est défini).
4. Valider → `hebergement:createReservation`. Le service vérifie la disponibilité de la chambre sur la période (`SELECT COUNT(*) ... date_arrivee < ? AND date_depart > ?`) et lève `Chambre déjà occupée sur cette période.` en cas de conflit.
5. Si le statut initial est `arrivee`, la chambre passe directement à `occupee`.

### 4.2 Cycle de vie réservation → check-in → check-out
1. **Check-in** (statut → `arrivee`) : la chambre passe à `occupee` ; création automatique (best-effort, erreurs ignorées) d'une **fiche de police** (`createFichePoliceFromReservation`, module Conformité hôtelière) et d'un **folio client** (`createFolioFromReservation`).
2. **Check-out** (statut → `depart`) : la chambre passe à `menage` ; calcul de la **taxe de séjour** du mois (`calculerTaxeSejour`), clôture du folio (`closeFolio`), et synchronisation du CA hébergement vers le module CA journalier (`syncHebergementCaFromErp`, voir [`recettes-journalieres.md`](recettes-journalieres.md)).
3. **Annulation** : la chambre repasse à `libre`.

### 4.3 Générer une facture depuis une réservation
- Bouton « Générer facture » sur une ligne (ou clôture de folio en facture) → `hebergement:createFactureFromReservation` / `closeFolioToFacture`.
- Le service crée une facture (`createFacture`, module Facturation) avec une ligne unique « Séjour ch. X (arrivée → départ) », quantité = nombre de nuits, prix unitaire = montant total / nb nuits, **TVA 19 %**. La réservation est ensuite liée à la facture (`facture_id`).
- Une facture ne peut être générée qu'une seule fois par réservation (`res.factureId` déjà défini → erreur).

### 4.4 Folio client (ardoise de séjour)
- Créé automatiquement au check-in avec une ligne « Nuitées ch. X ».
- Des lignes supplémentaires (extras) peuvent être ajoutées via `addFolioLine` (désignation, quantité, prix unitaire, taux TVA, catégorie) tant que le folio est `ouvert`.
- Le folio est clos au check-out (`closeFolio`), ou directement facturé (`closeFolioToFacture`), qui crée la facture correspondante et passe le folio au statut `facture`.

### 4.5 Gérer le plan des chambres
- Changement de statut en un clic depuis le plan (`libre` / `occupee` / `menage` / `hors_service`) via `hebergement:updateStatutChambre` — utile pour la remise en service après ménage ou blocage pour maintenance.

## 5. Règles métier DZ

- Aucune règle fiscale spécifique n'est appliquée dans ce module lui-même : la TVA (19 %) est appliquée au moment de la facturation (`createFactureFromReservation`), voir [`facturation.md`](facturation.md) pour le détail des règles DZ de facturation.
- La **taxe de séjour** est calculée au check-out via `calculerTaxeSejour` (module `hotel-legal.service.ts`, détails dans la fiche [`conformite-hoteliere.md`](conformite-hoteliere.md)) — non détaillée ici car hors périmètre de cette fiche.
- La **fiche de police** (déclaration client obligatoire en Algérie) est générée automatiquement au check-in (`createFichePoliceFromReservation`) — voir [`conformite-hoteliere.md`](conformite-hoteliere.md).

## 6. Interconnexions

- **Tarifs & conventions** (`/tarifs`, [`tarifs-conventions.md`](tarifs-conventions.md)) : l'estimation de prix d'une réservation (`estimateReservationPrice`) utilise la grille tarifaire, les conventions client actives et les promotions du module Tarifs via `simulerPrix`.
- **Clients** (`/clients`, [`clients.md`](clients.md)) : le champ « Client facturation » de la modale de réservation lit `clients_facturation` via `ipcClient.facturation.listClients()`.
- **Facturation** (`/facturation`, [`facturation.md`](facturation.md)) : génération de facture directe depuis une réservation ou depuis la clôture d'un folio (`createFacture`, ligne TVA 19 %, `reservation_id` lié).
- **Conformité hôtelière** (`/hotel-legal`) : fiche de police au check-in, taxe de séjour au check-out.
- **CA journalier (ERP)** (`/recettes/journalieres`, [`recettes-journalieres.md`](recettes-journalieres.md)) : au check-out, `syncHebergementCaFromErp` pousse le chiffre d'affaires hébergement de la réservation vers la recette journalière de l'hôtel.
- **Administration → Hôtels** : le paramétrage des chambres dépend de la liste des unités hôtelières (`ipcClient.hotels.list()`) ; sans hôtel configuré, l'onglet Paramétrage affiche un état vide invitant à passer par Administration → Hôtels.

## 7. Dépannage

- **« Accès hôtel refusé. »** : l'utilisateur tente de créer un type de chambre / une chambre / une réservation sur un hôtel hors de son périmètre (`actor.hotelIds`). Vérifier l'affectation hôtel de l'utilisateur dans Administration → Utilisateurs.
- **« Chambre déjà occupée sur cette période. »** : conflit de réservation détecté sur les dates choisies pour cette chambre — choisir une autre chambre ou ajuster les dates.
- **« Ce type est utilisé par des chambres actives. »** : impossible de supprimer un type de chambre tant que des chambres actives y sont rattachées — réaffecter ou désactiver les chambres d'abord.
- **« Chambre occupée par une réservation active. »** : impossible de désactiver une chambre ayant une réservation en cours (autre que annulée/départ/no-show).
- **« Une facture existe déjà pour cette réservation. »** : la génération de facture est bloquée si `factureId` est déjà renseigné sur la réservation — consulter la facture existante depuis `/facturation`.
- **« Montant de réservation invalide. »** : le montant total de la réservation doit être positif avant de pouvoir générer une facture — vérifier la saisie ou l'estimation de prix (plan/grille tarifaire absents ?).
- Fiche de police ou folio non créés au check-in : ces opérations sont **best-effort** (erreurs silencieusement ignorées dans `updateReservationStatut`) — si l'une échoue (ex. données client incomplètes), vérifier manuellement dans Conformité hôtelière / le folio de la réservation.
- KPI d'occupation à 0 ou incohérents : vérifier que des chambres actives existent pour l'hôtel et que les réservations ne sont pas toutes en statut `annulee`/`no_show`/`provisoire` (exclues du calcul des nuitées).
