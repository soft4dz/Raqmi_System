# Stocks & consommations

## 1. Présentation

Le module Stocks gère le référentiel des produits (matières premières, consommables) et le suivi des niveaux de stock par hôtel, avec traçabilité des mouvements (entrées, sorties, ajustements, pertes). Il sert de socle de consommation pour la Production/fiches techniques (`/cuisine`) et les Points de vente (`/pos`), et reçoit les entrées automatiques des réceptions d'Achats (`/achats`).

Composant : `src/pages/stocks/StocksPage.tsx`. Route : `/stocks`.

Ce module est technique/opérationnel ; il n'a pas de guide dédié dans `docs/guides-utilisateurs/`, mais est utilisé par les profils couverts par [`08-chef-departement.md`](../guides-utilisateurs/08-chef-departement.md) (chef de département cuisine/hébergement) et par la comptabilité pour la valorisation des consommations.

## 2. Prérequis & accès

- Authentification requise (`RequireAuth`), mot de passe déjà changé (`RequirePasswordChanged`).
- Aucune permission spécifique (`can...`) n'encadre la route `/stocks` dans `src/routes/AppRoutes.tsx` — tout utilisateur authentifié y accède si le module est activé.
- Le module `stocks-consommations` fait partie des modules désactivables via **Administration → Modules activés** (`/settings/modules`, réservé aux profils avec `canManageUsers`) : voir `src/shared/constants/configuredModules.ts` et `src/routes/RequireModuleEnabled.tsx`. S'il est désactivé, l'utilisateur est redirigé vers `/modules`.
- Le sélecteur d'hôtel en haut de page (`useHotelsList`) détermine le périmètre des niveaux de stock affichés — aucun contrôle d'accès par hôtel n'est visible côté page (le contrôle `actorCanAccessHotel` existe côté service pour d'autres modules mais **n'est pas appliqué** dans `stocks.service.ts`).

## 3. Écrans & champs

Écran unique (`StocksPage.tsx`), avec un sélecteur d'hôtel et un bouton « Nouveau produit ».

**KPIs (calculés côté client à partir de `getNiveaux`)** :
- Références : nombre de lignes de stock pour l'hôtel sélectionné.
- En alerte : nombre de produits où `quantite <= seuilAlerte`.
- Valeur totale : somme de `quantite × prixUnitaire` (PMP simple, pas de FIFO/LIFO).

**Liste des niveaux de stock** (type `StockNiveau`, `src/shared/types/stocks.ts`) : désignation, code produit, seuil d'alerte, unité, valeur, quantité. Une bannière rouge liste les produits en rupture si `alertes.length > 0`.

Par ligne, deux actions rapides : **Entrée** et **Sortie**, qui ouvrent la modale « Mouvement de stock » avec :
- Type de mouvement (`entree`, `sortie`, `ajustement`, `perte`) — les libellés affichés sont les valeurs brutes de la base, non traduites.
- Quantité (numérique, requise).
- Motif (texte libre).

**Modale « Nouveau produit »** : code (requis), désignation (requise), unité (texte libre, défaut « pièce »), seuil d'alerte (numérique).

## 4. Workflows standards

**Créer un produit** : bouton « Nouveau produit » → `ipcClient.stocks.createProduit` → `stocks:createProduit` (`electron/ipc/stocks.ipc.ts`) → `createProduit()` (`electron/services/stocks.service.ts`) insère dans `stock_produits`. **Un produit nouvellement créé n'apparaît pas encore dans la liste des niveaux** de l'hôtel tant qu'aucun mouvement n'a été saisi (voir Dépannage).

**Enregistrer un mouvement** : clic sur ▲ (entrée) ou ▼ (sortie) sur une ligne → modale pré-remplie avec le type → `ipcClient.stocks.createMouvement` → `stocks:createMouvement` → `createMouvement()` :
- Insère une ligne dans `stock_mouvements`.
- Met à jour `stock_niveaux` par upsert (`ON CONFLICT ... DO UPDATE SET quantite = quantite + excluded.quantite`). **Important** : le signe appliqué est négatif uniquement pour `sortie` et `perte` ; `entree` **et** `ajustement` incrémentent tous deux le niveau (`signe = ['sortie','perte'].includes(type) ? -1 : 1`).
- Déclenche automatiquement une écriture comptable SCF via `postComptaForMouvement()` (`stocks-compta.service.ts`), sauf si `skipCompta` est passé — voir Règles métier DZ.

## 5. Règles métier DZ

- Chaque mouvement de stock de type `entree`, `sortie` ou `perte` (pas `ajustement`) génère automatiquement une écriture comptable SCF via `genererEcritureVariationStock()` (`electron/services/comptabilite.service.ts`) :
  - Entrée provenant d'une réception de bon de commande (motif contenant « Réception BC ») → journal `AC`, débit compte Stocks, crédit compte Fournisseurs.
  - Entrée hors réception → débit Stocks, crédit Achats consommés.
  - Sortie/perte → débit Achats consommés, crédit Stocks.
  - L'échec de la génération comptable est absorbé silencieusement (`try/catch` dans `createMouvement`) pour ne pas bloquer la saisie stock.
- Aucune autre règle fiscale DZ spécifique (pas de TVA directe sur les mouvements internes).

## 6. Interconnexions

- **Achats & approvisionnements** (`/achats`, [`achats-approvisionnements.md`](achats-approvisionnements.md)) : la réception d'un bon de commande (`livrerBon()`) crée automatiquement des mouvements `entree` par ligne reçue.
- **Production & fiches techniques** (`/cuisine`, [`production-fiches-techniques.md`](production-fiches-techniques.md)) : la validation d'un ordre de production (`executerOrdreProduction`) consomme le stock des ingrédients via `consommerStockRecette()` → mouvements `sortie`.
- **Points de vente** (`/pos`, [`pos-restauration.md`](pos-restauration.md)) : l'encaissement d'un ticket (`validerTicket`) consomme également le stock des ingrédients de chaque recette vendue, via la même fonction `consommerStockRecette()`.
- **Comptabilité SCF** ([`comptabilite-scf.md`](comptabilite-scf.md)) : réception automatique des écritures de variation de stock (voir Règles métier DZ).
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `stocks-consommations`) référence aussi Maintenance & interventions et Budget & prévisions comme connectés, mais **aucun code actuel** ne crée de mouvement de stock automatique depuis le module Maintenance (le coût des pièces y est un champ manuel, non lié à `stock_mouvements`).

## 7. Dépannage

- **« Aucun stock enregistré pour cet hôtel »** alors qu'un produit vient d'être créé : normal — `getNiveaux()` lit la table `stock_niveaux` (alimentée uniquement par un mouvement), pas `stock_produits`. Il faut saisir un premier mouvement d'entrée pour faire apparaître le produit dans la liste de cet hôtel.
- **Écart entre stock théorique et physique** : vérifier les mouvements `ajustement` — ils incrémentent toujours le niveau (aucune option d'ajustement négatif dans l'UI actuelle) ; pour corriger un excédent constaté, utiliser `sortie` ou `perte`.
- **Écriture comptable manquante pour un mouvement** : les mouvements `ajustement` ne génèrent jamais d'écriture SCF (exclu volontairement de `postComptaForMouvement`). Pour les autres types, vérifier le journal `AC`/`OD` dans [`comptabilite-scf.md`](comptabilite-scf.md).
- **Produit non trouvé lors d'une ligne de fiche technique / bon de commande** : le référentiel produits (`stocks.listProduits`) est global (non filtré par hôtel) — un produit doit exister une seule fois, indépendamment de l'hôtel.
