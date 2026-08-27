# Plage & piscine

## 1. Présentation

Module de gestion des accès payants à la plage et à la piscine de l'hôtel : configuration des capacités et tarifs (adulte, enfant, résident), enregistrement des entrées avec calcul automatique du montant selon la composition du groupe (adultes/enfants/résidents).

Composant : `src/pages/plage/PlagePage.tsx`. Route : `/plage`. Backend : `electron/services/plage.service.ts`.

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/plage` dans `AppRoutes.tsx`.
- Module désactivable via **Administration → Modules activés** (`plage-piscine` figure dans `CONFIGURED_MODULE_IDS`).
- Aucun contrôle d'accès par hôtel appliqué côté service (`plage.service.ts` ne vérifie pas `actorCanAccessHotel`).
- La tarification (onglet « Paramétrage ») doit être renseignée avant toute saisie d'entrée, sinon le montant calculé sera 0.

## 3. Écrans & champs

Deux onglets (`PlagePage.tsx`), avec sélecteur d'hôtel.

**Onglet « Paramétrage »** : capacité plage, capacité piscine, tarif adulte, tarif enfant, tarif résident (`PlageConfig`).

**Onglet « Exploitation »** :
- KPIs (`PlageStats`) : visiteurs du jour (+ nombre d'entrées), CA du jour, visiteurs du mois (+ CA du mois).
- Sélecteur de date pour consulter les entrées d'un jour donné.
- Liste des entrées du jour (`PlageEntree`) : type de visiteur (texte libre stocké dans `observation`), formule (`plage`/`piscine`), détail adultes/enfants/résidents, montant payé.
- Modale « Enregistrer une entrée » : type de visiteur (`touriste`, `resident`, `membre`, `exterieur`, `vip`), formule (`plage` ou `piscine`), nombre d'adultes, nombre d'enfants.

## 4. Workflows standards

**Paramétrer les tarifs** : onglet Paramétrage → « Enregistrer » → `ipcClient.plage.saveConfig` → upsert dans `plage_config`.

**Enregistrer une entrée** : « Enregistrer entrée » → formulaire :
- Si le type de visiteur sélectionné est `resident`, la page reclasse automatiquement les personnes saisies en « résidents » côté appel IPC (`nbResidents = adultes + enfants`, `nbAdultes = nbEnfants = 0`) plutôt qu'en adultes/enfants classiques — logique portée par `PlagePage.tsx`, pas par le service.
- → `ipcClient.plage.createEntree` → `createEntree()` calcule le montant : `nbAdultes × tarifAdulte + nbEnfants × tarifEnfant + nbResidents × tarifResident`, insère dans `plage_entrees` avec la zone (`plage`/`piscine`) et la date d'entrée (par défaut la date sélectionnée à l'écran).

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module (pas de TVA appliquée dans le service, pas d'écriture comptable ou fiscale automatique constatée dans le code).

## 6. Interconnexions

- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `plage-piscine`) référence CA journalier, Encaissements & trésorerie, Stocks & consommations et Maintenance & interventions comme modules connectés.
- **Constat de code** : comme pour [`parking.md`](parking.md), aucune synchronisation automatique n'a été trouvée entre les entrées plage/piscine et `recettes_journalieres`, `encaissements` ou la comptabilité SCF, ni avec les Stocks (pas de consommation de produits liée à une entrée). Le CA plage/piscine reste local à ce module.
- Pour intégrer ces recettes au CA hôtel, il faut actuellement les ressaisir manuellement dans [`recettes-journalieres.md`](recettes-journalieres.md) ou [`encaissements-tresorerie.md`](encaissements-tresorerie.md).

## 7. Dépannage

- **Montant calculé à 0 DA** : les tarifs (`PlageConfig`) ne sont pas renseignés pour l'hôtel — compléter l'onglet Paramétrage.
- **Entrée « résident » sans montant attendu** : vérifier que le tarif résident est bien renseigné ; les résidents ne sont jamais comptés dans les colonnes adultes/enfants (`nombreAdultes`/`nombreEnfants` affichés à 0 pour ces entrées, la personne apparaît uniquement dans le total `nombrePersonnes`).
- **Entrée non visible dans la liste du jour** : la liste filtre strictement sur la date sélectionnée dans le sélecteur (`plage.listEntrees(hotelId, date)`) — changer la date pour retrouver une entrée saisie sur un autre jour.
- **Le CA plage/piscine n'apparaît pas dans la clôture journalière hôtel** : comportement attendu à ce jour — voir Interconnexions.
