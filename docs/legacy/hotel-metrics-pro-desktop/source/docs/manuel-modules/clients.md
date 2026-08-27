# Clients

## 1. Présentation

Le module **Clients** gère le référentiel unique des clients de facturation (entreprises et particuliers) du groupe hôtelier : coordonnées, identifiants fiscaux algériens (NIF, NRC, NIS, AI), informations bancaires, personnes de contact, et statistiques de facturation par client. C'est le référentiel partagé consommé par les réservations (Hébergement), les conventions tarifaires (Tarifs) et la facturation.

Route d'entrée : `/clients` (liste `/clients`, détail `/clients/:id`, création `/clients/nouveau`). Composants : `src/pages/clients/ClientsListPage.tsx`, `ClientDetailPage.tsx`, `NouveauClientPage.tsx`.

Ce module est principalement utilisé par la **comptabilité/trésorerie** (guide [`06-comptabilite-tresorerie.md`](../guides-utilisateurs/06-comptabilite-tresorerie.md)) et les fonctions administratives, dans la mesure où sa gestion (création, modification, suppression) est réservée aux rôles admin globaux (voir §2).

## 2. Prérequis & accès

- Route `/clients` affichée dans le menu latéral (section « Exploitation », `src/layouts/sidebarModules.ts`) **sans condition de rôle** — le lien est visible pour tout utilisateur connecté.
- **Contrôle réel côté serveur, plus strict que le menu** : toutes les fonctions de `electron/services/clients.service.ts` (liste, détail, dashboard, création, modification, activation/désactivation, suppression, gestion des contacts) passent par `assertCanManageClients`, qui exige `isGlobalAdminRole(actor.roleCode)` — c'est-à-dire **uniquement les rôles `SUPERADMIN` ou `ADMIN_DEC`** (`GLOBAL_ROLES` dans `electron/services/actorContext.ts`). Tout autre rôle reçoit l'erreur « Accès refusé — droits insuffisants », même si l'écran est accessible dans l'interface.
- Ce référentiel n'est pas filtré par hôtel : il est global au groupe (pas d'`applyActorHotelFilter` dans `clients.service.ts`).
- Dépendances : le module alimente les sélecteurs client de **Hébergement** (réservations, [`hebergement-occupation.md`](hebergement-occupation.md)), **Tarifs & conventions** (conventions client, [`tarifs-conventions.md`](tarifs-conventions.md)) et **Facturation** (`/facturation`, [`facturation.md`](facturation.md)).

## 3. Écrans & champs

### 3.1 Liste des clients (`ClientsListPage.tsx`, `/clients`)
- KPI en tête : Total clients, Clients actifs, Entreprises, Particuliers, puis Total facturé TTC/HT et Restant dû (agrégés sur toutes les factures non annulées, tous clients confondus — `getClientsDashboard`).
- Filtres : recherche libre (nom, email, téléphone, NIF), Type (Entreprises/Particuliers), Statut (Tous/Actifs/Inactifs).
- Tableau : Client (nom + badge type/forme juridique), Contact (téléphone, email, NIF), Localisation (ville, wilaya), Factures (nombre), Total TTC, Restant, Statut (Actif/Inactif), actions **Voir** / **Activer-Désactiver** / **Supprimer** (avec confirmation inline).

### 3.2 Fiche client (`ClientDetailPage.tsx`, `/clients/:id`)
En-tête : nom affiché (raison sociale pour une entreprise, civilité+nom+prénom pour un particulier), badges type et statut actif/inactif, résumé (nb factures, total, restant dû, nb contacts), boutons Activer/Désactiver et Modifier.

7 onglets :
- **Aperçu** : Identification (type, civilité/nom/prénom ou raison sociale/forme juridique), Statistiques (factures, total TTC, payé, restant dû, date dernière facture), Contact principal (téléphone, mobile, email, fax, adresse), Identifiants clés (NIF, NRC, NIS, régime d'imposition, assujetti TVA + n° TVA).
- **Contact & Adresse** : téléphone, mobile, email, fax, site web ; adresse ligne 1/2, code postal, ville/commune (sélecteur wilaya/commune `WilayaCommuneFields`), wilaya, pays (Algérie par défaut).
- **Personnes de contact** : liste de contacts secondaires avec Fonction (`TypeContact` : gérant, DG, DAF, commercial, comptable, autre), Nom complet, Titre/Poste, Téléphone, Email, indicateur « Contact principal » (un seul contact principal par client, la sélection en désigne un nouveau et retire le flag des autres). Ajout/modification/suppression en ligne.
- **Fiscal & Légal** : NIF + date d'expiration, NRC (registre du commerce) + date d'expiration, NIS (n° identification statistique), AI (article d'imposition), date de création entreprise, n° agrément/décision, régime d'imposition (`RegimeImposition` : `reel`, `forfait_unique`, `ifu`, `auto_entrepreneur`), case « Assujetti à la TVA » + n° TVA si cochée.
- **Banque** : banque domiciliataire, agence bancaire, RIB/numéro de compte.
- **Factures** : liste des factures du client (numéro, date, statut, TTC, restant, lien vers le détail) et raccourci « Nouvelle facture ».
- **Notes** : notes internes libres (non communiquées au client).

### 3.3 Nouveau client (`NouveauClientPage.tsx`, `/clients/nouveau`)
Formulaire à onglets (Identification, Contact, Adresse, Fiscal, Banque, Notes) reprenant les mêmes champs que la fiche détail. Champ obligatoire : Nom (ou Raison sociale pour une entreprise). Le type de client (Entreprise/Particulier) détermine les champs affichés dans l'onglet Identification. Case « Client actif » cochée par défaut.

## 4. Workflows standards

### 4.1 Créer un client
1. `/clients` → « Nouveau client ».
2. Choisir le type (Entreprise/Particulier), renseigner le nom/raison sociale obligatoire, puis compléter les onglets pertinents (coordonnées, adresse, identifiants fiscaux, banque, notes).
3. Valider → `clients:create`. Redirection automatique vers la fiche du client créé.

### 4.2 Modifier / activer-désactiver un client
1. Depuis la fiche client → « Modifier », éditer les champs par onglet, « Enregistrer » (`clients:update`).
2. Activer/Désactiver (`clients:toggleActif`) : bascule le champ `actif` sans suppression — un client inactif reste visible dans l'historique de facturation mais peut être exclu des listes filtrées.

### 4.3 Gérer les personnes de contact
1. Onglet « Personnes de contact » de la fiche client → « Ajouter un contact » → renseigner fonction, nom (obligatoire), titre, téléphone, email, et cocher « Contact principal » si pertinent.
2. `clients:contacts:create` / `update` / `delete` — un seul contact principal actif à la fois par client (le service retire automatiquement le flag des autres contacts lors de la désignation d'un nouveau principal).

### 4.4 Supprimer un client
1. Liste des clients → « Supprimer » → confirmer.
2. `clients:delete` effectue une suppression logique (`deleted_at`), **bloquée si le client a des factures actives** (statut différent de `annulee`) : `Ce client a N facture(s) active(s) — suppression impossible.` Dans ce cas, il faut d'abord annuler ou clôturer les factures concernées, ou simplement désactiver le client plutôt que le supprimer.

### 4.5 Utiliser le référentiel Clients depuis un autre module
- Dans **Hébergement**, la modale « Nouvelle réservation » propose une sélection de client via `ipcClient.facturation.listClients()` pour préremplir automatiquement nom/prénom/email/téléphone.
- Dans **Tarifs & conventions**, la création d'une convention nécessite de choisir un client actif (`useClients({ actif: true })`).
- Dans **Facturation**, chaque facture peut être rattachée à un `clientId` de ce référentiel (voir [`facturation.md`](facturation.md)).

## 5. Règles métier DZ

- Le module capture les identifiants légaux algériens standards pour une facturation conforme : **NIF** (numéro d'identification fiscale, avec date d'expiration), **NRC** (numéro du registre du commerce, avec date d'expiration), **NIS** (numéro d'identification statistique), **AI** (article d'imposition), et le **régime d'imposition** (réel, forfait unique, IFU, auto-entrepreneur).
- Case « Assujetti à la TVA » avec numéro de TVA associé — sert de référence pour la facturation (voir règles TVA détaillées dans [`facturation.md`](facturation.md)).
- Champs bancaires (banque domiciliataire, agence, RIB) disponibles pour les besoins de paiement/virement, sans validation de format particulière observée dans le code.
- Aucune validation automatique de format (NIF/NRC/RIB) n'est appliquée côté service — la saisie reste libre.

## 6. Interconnexions

- **Hébergement & occupation** (`/hebergement`, [`hebergement-occupation.md`](hebergement-occupation.md)) : sélection d'un client existant à la création d'une réservation.
- **Tarifs & conventions** (`/tarifs`, [`tarifs-conventions.md`](tarifs-conventions.md)) : les conventions tarifaires négociées sont rattachées à un client de ce référentiel.
- **Facturation** (`/facturation`, [`facturation.md`](facturation.md)) : chaque facture peut référencer un `clientId` ; les statistiques de la fiche client (nombre de factures, total TTC, payé, restant dû) sont calculées par agrégation sur la table `factures`.
- **Créances & recouvrement** (`/creances`, [`creances-recouvrement.md`](creances-recouvrement.md)) : le solde restant dû affiché sur la fiche client reflète les factures impayées, en lien avec le suivi des créances.

## 7. Dépannage

- **« Accès refusé — droits insuffisants »** : l'utilisateur connecté n'a pas le rôle `SUPERADMIN` ou `ADMIN_DEC` — c'est la cause la plus fréquente d'un blocage sur ce module malgré un lien de menu visible. Contacter un administrateur pour effectuer l'action ou obtenir les droits nécessaires.
- **« Ce client a N facture(s) active(s) — suppression impossible. »** : annuler ou finaliser les factures concernées avant suppression, ou préférer la désactivation (`Désactiver`) qui n'a pas cette contrainte.
- **« Le nom / raison sociale est obligatoire. »** : champ nom vide à la création — l'onglet Identification est automatiquement rouvert par le formulaire.
- **« Client introuvable »** : identifiant de client invalide ou client supprimé logiquement (`deleted_at` renseigné) — vérifier l'URL `/clients/:id`.
- **« Contact introuvable »** : tentative de modification/suppression d'un contact déjà supprimé — rafraîchir la fiche client.
- KPI « Total facturé » / « Restant dû » incohérents : ces totaux excluent les factures au statut `annulee` — vérifier le statut des factures concernées dans [`facturation.md`](facturation.md) si un montant semble manquant.
