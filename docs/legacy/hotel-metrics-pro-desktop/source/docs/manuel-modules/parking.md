# Parking

## 1. Présentation

Module de gestion du parking hôtelier : configuration de la capacité et des tarifs (horaire, journée, nuit), enregistrement des entrées/sorties de véhicules avec calcul automatique du montant dû à la sortie.

Composant : `src/pages/parking/ParkingPage.tsx`. Route : `/parking`. Backend : `electron/services/parking.service.ts`.

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/parking` dans `AppRoutes.tsx`.
- Module désactivable via **Administration → Modules activés** (`parking` figure dans `CONFIGURED_MODULE_IDS`).
- Aucun contrôle d'accès par hôtel appliqué côté service (`parking.service.ts` ne vérifie pas `actorCanAccessHotel`).
- La tarification doit être paramétrée (onglet « Paramétrage ») avant toute sortie de véhicule, sinon le montant calculé sera 0.

## 3. Écrans & champs

Deux onglets (`ParkingPage.tsx`), avec sélecteur d'hôtel.

**Onglet « Paramétrage »** : capacité maximale, tarif/heure (DA), tarif journée, tarif nuit (`ParkingConfig`).

**Onglet « Exploitation »** :
- KPIs (`ParkingStats`) : places totales, occupées, libres, entrées du jour, CA du jour.
- Barre de taux d'occupation (couleur verte < 60 %, orange 60–80 %, rouge > 80 %).
- Liste des véhicules présents (`statut = 'en_cours'`) : immatriculation, type de véhicule, heure d'entrée, bouton « Sortie ».
- Modale « Entrée véhicule » : immatriculation (mise en majuscules automatiquement), type de véhicule (`voiture`, `moto`, `camionnette`, `bus`, `autre`).

## 4. Workflows standards

**Enregistrer une entrée** : « Entrée véhicule » → `ipcClient.parking.entree` → `entreeVehicule()` insère dans `parking_tickets` (statut `en_cours` par défaut en base, pas de montant).

**Enregistrer une sortie** : bouton « Sortie » sur un ticket → `ipcClient.parking.sortie` → `sortieVehicule()` :
- Calcule la durée en minutes entre `entree_at` et l'instant présent.
- Applique le tarif configuré : `≤ 1h` → tarif/heure ; `≤ 8h` → tarif/heure × nombre d'heures arrondi au supérieur ; `> 8h` → tarif journée forfaitaire (aucun calcul multi-jours ni tarif nuit automatique dans cette logique — le tarif nuit configuré n'est pas utilisé par le calcul actuel).
- Passe le statut à `termine`.

**Paramétrer les tarifs** : onglet Paramétrage → « Enregistrer » → `ipcClient.parking.saveConfig` → upsert dans `parking_config`.

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module (pas de TVA appliquée dans le service, pas d'écriture comptable ou fiscale automatique constatée dans le code).

## 6. Interconnexions

- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `parking`) référence CA journalier, Encaissements & trésorerie, Facturation et Rapports automatiques comme modules connectés.
- **Constat de code** : aucune synchronisation automatique n'a été trouvée entre le parking et `recettes_journalieres`, `encaissements` ou la comptabilité SCF (contrairement au POS, voir [`pos-restauration.md`](pos-restauration.md)). Le CA du jour affiché dans les KPIs Parking (`chiffreAffaireJour`) reste local à ce module et n'alimente aujourd'hui aucune autre fiche du système.
- Pour intégrer les recettes parking au CA hôtel, il faut actuellement les ressaisir manuellement dans [`recettes-journalieres.md`](recettes-journalieres.md) ou [`encaissements-tresorerie.md`](encaissements-tresorerie.md).

## 7. Dépannage

- **Montant de sortie à 0 DA** : la configuration tarifaire (`ParkingConfig`) n'a pas été renseignée pour l'hôtel — aller dans l'onglet Paramétrage et enregistrer au moins un tarif horaire ou journée.
- **Taux d'occupation figé à 0 %** : la capacité (`capacite`) n'est pas renseignée dans le paramétrage — le calcul `enCours / capacite` retourne 0 si `capacite = 0`.
- **Le CA parking n'apparaît pas dans la clôture journalière hôtel** : comportement attendu à ce jour — voir Interconnexions ; aucune synchronisation automatique n'existe.
- **Immatriculation vide sur un ticket** : le champ n'est pas strictement obligatoire côté service (`immatriculation ?? null`), mais le formulaire UI bloque l'enregistrement tant qu'il est vide (`disabled={!form.immatriculation}`).
