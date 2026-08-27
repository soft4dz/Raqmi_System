# Production & fiches techniques

## 1. Présentation

Ce module gère la nomenclature cuisine (fiches techniques = recettes avec ingrédients et coût matière) et les ordres de production. Une fois validée, une fiche technique devient un article vendable dans le module Points de vente (`/pos`) — le module Production ne gère plus lui-même la vente (voir Dépannage). Objectif métier : maîtriser le coût matière, la marge par plat, et automatiser la consommation de stock à la production comme à la vente.

Composant : `src/pages/cuisine/CuisinePage.tsx`. Route : `/cuisine`. Backend : `electron/services/cuisine-production.service.ts` (logique active), `electron/services/cuisine-pos.service.ts` (module désactivé/historique).

Utilisé principalement par les chefs de département restauration/cuisine — voir [`08-chef-departement.md`](../guides-utilisateurs/08-chef-departement.md).

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/cuisine` dans `src/routes/AppRoutes.tsx`.
- **Particularité** : contrairement à la plupart des modules Opérations, `production-fiches-techniques` **n'apparaît pas** dans `CONFIGURED_MODULE_IDS` (`src/shared/constants/configuredModules.ts`). Il ne peut donc pas être désactivé depuis **Administration → Modules activés** — `RequireModuleEnabled` le laisse toujours passer.
- Nécessite un référentiel produits déjà alimenté dans [`stocks-consommations.md`](stocks-consommations.md) (les ingrédients d'une fiche sont choisis parmi `stocks.listProduits()`).

## 3. Écrans & champs

Écran à deux onglets (`CuisinePage.tsx`), avec sélecteur d'hôtel.

**Onglet « Fiches techniques »** :
- Liste des recettes (`CuisineRecette`, `src/shared/types/cuisine.ts`) : nom, statut (`brouillon` / `valide`), code, nombre d'ingrédients, coût de revient et marge % une fois calculés.
- Panneau détail d'une fiche sélectionnée : portions, statut, tableau des lignes (ingrédient, quantité + unité + taux de perte, coût ligne), bouton « Valider (production) » actif quand la fiche est en brouillon et compte au moins un ingrédient.
- Formulaire d'ajout de ligne (uniquement si fiche en brouillon) : sélection produit, quantité, taux de perte.
- Modale « Nouvelle fiche technique » : code (ex. `PLT-001`), nom du plat, portions, prix de vente (DA).

**Onglet « Ordres de production »** :
- Formulaire de planification : recette validée (seules les recettes `statut = 'valide'` sont proposées), date de production, portions prévues.
- Tableau des ordres (`CuisineOrdreProduction`) : date, plat, portions, coût théorique, statut, bouton « Exécuter → stock » si le statut n'est pas `termine`.

## 4. Workflows standards

**Créer et valider une fiche technique** :
1. « Nouvelle fiche technique » → `ipcClient.cuisine.createRecette` → `cuisine:recettes:create` → `createRecette()` insère dans `cuisine_recettes` (statut `brouillon`).
2. Ajouter des lignes d'ingrédients → `ipcClient.cuisine.upsertRecetteLigne` → `upsertRecetteLigne()` recalcule `cout_revient` à chaque ajout/retrait de ligne (`calculerCoutRevient()` = somme de `quantité × (1 + taux_perte/100) × prix_unitaire produit`).
3. « Valider (production) » → `ipcClient.cuisine.validerRecette` → `validerRecette()` : exige au moins un ingrédient, fige `cout_revient`, calcule `marge_pct = (prixVente - coutRevient) / prixVente × 100` si un prix de vente est renseigné, passe le statut à `valide`. **Une fiche validée ne peut plus être modifiée** (`updateRecette`, `upsertRecetteLigne`, `removeRecetteLigne` lèvent une erreur si `statut === 'valide'`).
4. Émet l'événement `RECIPE_VALIDATED` (`event-bus.service.ts`).

**Planifier et exécuter un ordre de production** :
1. « Planifier » (recette validée obligatoire) → `ipcClient.cuisine.createOrdre` → `createOrdreProduction()` : calcule un coût théorique (`coutRevient de la recette × portionsPrevues`), insère dans `cuisine_ordres_production` (statut `planifie`/valeur par défaut).
2. « Exécuter → stock » → `ipcClient.cuisine.executerOrdre` → `executerOrdreProduction()` : consomme le stock des ingrédients pour la quantité de portions prévues via `consommerStockRecette()` (mouvements `sortie` dans [`stocks-consommations.md`](stocks-consommations.md), source `cuisine_ordre`), passe le statut à `termine`, enregistre `portionsRealisees` et émet `PRODUCTION_EXECUTED`.

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module (pas de TVA ni d'obligation fiscale propre à la nomenclature ou à l'ordre de production — les écritures comptables/TVA sont générées côté [`pos-restauration.md`](pos-restauration.md) au moment de la vente, pas côté production).

## 6. Interconnexions

- **Stocks & consommations** ([`stocks-consommations.md`](stocks-consommations.md)) : consommation automatique des ingrédients à l'exécution d'un ordre de production, et à chaque vente validée en caisse (voir ci-dessous).
- **Points de vente** ([`pos-restauration.md`](pos-restauration.md)) : les fiches techniques `valide` alimentent directement la carte du POS (`recettesValidees` dans `PosPage.tsx`) ; la validation d'un ticket POS appelle la même fonction `consommerStockRecette()` que l'exécution d'un ordre de production.
- **Achats & approvisionnements** ([`achats-approvisionnements.md`](achats-approvisionnements.md)) : fournit indirectement les produits/ingrédients via les réceptions de bons de commande qui alimentent le stock.
- **Budget & prévisions** (`/objectifs`) : référencé dans `src/modules/moduleCatalog.ts` (`connectedTo`) mais aucun flux de code direct identifié entre production et budget.

## 7. Dépannage

- **« Les ventes se font désormais via Points de vente (/pos) »** : erreur levée volontairement par `enregistrerVentePos()` (`cuisine-pos.service.ts`) si l'ancien endpoint IPC `cuisine:pos:vente` est encore invoqué. L'onglet Ventes POS cuisine est désactivé côté UI ; toute vente doit passer par `/pos` pour éviter les doublons de mouvements de stock et d'écritures comptables.
- **Impossible de modifier une fiche technique** : la fiche est déjà `valide`. Il n'existe pas de fonction de dévalidation dans le code actuel — en cas d'erreur sur une fiche validée, créer une nouvelle fiche.
- **« Recette validée requise »** lors de la planification d'un ordre ou de l'ajout d'une ligne de ticket POS : la recette sélectionnée est encore en `brouillon` — la valider d'abord dans l'onglet « Fiches techniques ».
- **Coût de revient à 0 ou incohérent** : vérifier le `prixUnitaire` du produit ingrédient dans `stock_produits` (référentiel [`stocks-consommations.md`](stocks-consommations.md)) — le coût de revient est un simple produit quantité × prix unitaire courant, sans historique de PMP par période.
