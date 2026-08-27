# Maintenance & interventions

## 1. Présentation

Ce module gère le parc d'équipements techniques de l'hôtel et les interventions de maintenance (préventive/corrective), avec suivi de priorité, statut, coûts pièces/main d'œuvre et rapport d'intervention.

Composant : `src/pages/maintenance/MaintenancePage.tsx`. Route : `/maintenance`. Backend : `electron/services/maintenance.service.ts`.

Utilisé principalement par les chefs de département technique/maintenance — voir [`08-chef-departement.md`](../guides-utilisateurs/08-chef-departement.md).

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/maintenance` dans `AppRoutes.tsx`.
- Module désactivable via **Administration → Modules activés** (`maintenance-interventions` figure dans `CONFIGURED_MODULE_IDS`).
- Aucun contrôle d'accès par hôtel n'est appliqué côté service (`maintenance.service.ts` n'utilise pas `actorCanAccessHotel`), contrairement au POS ou aux Achats.

## 3. Écrans & champs

Deux onglets (`MaintenancePage.tsx`), avec sélecteur d'hôtel.

**KPIs** (calculés côté service via `getMaintenanceStats`) : total interventions, en cours, urgentes (hors terminées/annulées), coût du mois (somme `coutPieces + coutMainOeuvre` des interventions terminées ce mois-ci).

**Onglet « Interventions »** :
- Filtres rapides par statut : Toutes, Ouvertes, En cours, Planifiées, Terminées.
- Liste (`Intervention`) : titre, priorité (`normale`/`haute`/`urgente`, badge coloré), équipement lié le cas échéant, icône de statut.
- Actions : **Commencer** (statut `ouverte` → `en_cours`), **Terminer** (statut `en_cours` → `terminee`, avec date de fin, coûts pièces/main d'œuvre du formulaire courant et rapport).
- Modale « Nouvelle intervention » : titre (requis), équipement (optionnel), description.

**Onglet « Équipements »** :
- Liste (`Equipement`) : désignation, code, catégorie, localisation, date d'achat.
- Bouton « Créer intervention » par équipement (pré-remplit le formulaire d'intervention avec l'équipement et bascule sur l'onglet Interventions).
- Modale « Nouvel équipement » : code, désignation, catégorie, emplacement, date d'achat (le service accepte aussi marque, modèle, numéro de série, fin de garantie, non exposés dans ce formulaire).

## 4. Workflows standards

**Déclarer et traiter une intervention** :
1. « Nouvelle intervention » (ou depuis un équipement) → `ipcClient.maintenance.createIntervention` → `createIntervention()` insère dans `interventions`, statut initial `ouverte` (implicite en base), priorité par défaut `normale`, type `corrective` par défaut.
2. « Commencer » → `ipcClient.maintenance.updateIntervention({ statut: 'en_cours', dateDebut })`.
3. « Terminer » → `ipcClient.maintenance.updateIntervention({ statut: 'terminee', dateFin, coutPieces, coutMainOeuvre, rapport })` — les coûts saisis sont ceux actuellement dans l'état local du formulaire de création (`form`), pas un formulaire dédié à la clôture ; à vérifier avant de cliquer « Terminer » si des coûts doivent être renseignés.

**Créer un équipement** : « Nouvel équipement » → `ipcClient.maintenance.createEquipement` → insertion dans `equipements`, statut par défaut en base.

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module (pas de TVA, pas d'obligation fiscale ou sociale propre à la gestion des interventions techniques).

## 6. Interconnexions

- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `maintenance-interventions`) référence Stocks & consommations, Qualité & réclamations clients, Achats & approvisionnements et Journal des anomalies comme modules connectés.
- **Constat de code** : à ce jour, aucun flux automatique n'existe entre Maintenance et ces modules — `coutPieces` est un champ numérique libre, non relié à un mouvement de stock ([`stocks-consommations.md`](stocks-consommations.md)) ni à un bon de commande ([`achats-approvisionnements.md`](achats-approvisionnements.md)). Toute pièce consommée doit être décrémentée manuellement dans Stocks si nécessaire.
- Toute action (création équipement, création/mise à jour intervention) n'écrit pas explicitement dans le journal d'audit (`writeAuditLog`) dans le code actuel de `maintenance.service.ts` — contrairement à la plupart des autres services Opérations.

## 7. Dépannage

- **Bouton « Terminer » grisé ou coûts incorrects** : les coûts pièces/main d'œuvre envoyés proviennent de l'état du formulaire « Nouvelle intervention » (`form.coutPieces`, `form.coutMainOeuvre`), qui n'est pas remis à zéro automatiquement entre deux interventions ; vérifier ces valeurs avant de clôturer une intervention si elles n'ont pas été explicitement saisies pour celle-ci.
- **Équipement non filtré par hôtel dans l'intervention** : la liste des équipements proposée dans le formulaire d'intervention correspond à l'hôtel sélectionné en haut de page (`hotelId`) — changer d'hôtel réinitialise les options disponibles.
- **KPI « Coût du mois » à 0** malgré des interventions terminées : le calcul se base sur `date_fin` du mois calendaire courant et sur le statut `terminee` — une intervention terminée sans `dateFin` renseignée n'est pas comptabilisée.
- **Intervention introuvable après mise à jour** : `updateIntervention` lève une erreur explicite si l'`id` ne correspond à aucune ligne — vérifier que l'intervention n'a pas été supprimée en base (aucune suppression n'est exposée côté UI, donc cas rare).
