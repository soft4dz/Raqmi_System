# Cockpit DEC

## Présentation

Le Cockpit DEC (Directeur d'Exploitation / Contrôleur) est l'écran de pilotage quotidien du contrôle interne : un jeu de widgets synthétiques (CA du jour, encaissements, retards de clôture, anomalies, réclamations, créances critiques) couplé à un flux d'**alertes de contrôle** centralisées, alimentées automatiquement par plusieurs modules métier (clôture journalière, rapprochement financier).

Composant : `src/pages/dec/DecCockpitPage.tsx`. Service : `electron/services/dec-cockpit.service.ts`.

Public cible : contrôleur d'exploitation / DEC — voir `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route : `/dec/cockpit` (« Cockpit DEC » du module « Pilotage »).
- Contrôle serveur (`getDecCockpit`) : accès autorisé si l'utilisateur a un rôle admin global (`isGlobalAdminRole`) **ou** s'il a au moins un hôtel assigné (`actor.hotelIds.length > 0`) ; sinon erreur « Accès cockpit DEC refusé. ».
- Filtre hôtel disponible en haut de page (liste chargée via `useHotelsList`), par défaut positionné sur le premier hôtel de l'utilisateur (`defaultHotelId`).

## Écrans & champs

Écran unique :

1. **Sélecteur d'hôtel** : « Tous les hôtels » ou un hôtel précis.
2. **Widgets** (`DecWidgetData[]`), 6 indicateurs avec code, domaine, valeur et niveau (`normal`/`warning`/`critical`) :
   - `ca_jour` — CA du jour.
   - `encaissements` — encaissements confirmés du jour.
   - `retard_saisie_0930` — nombre de clôtures journalières non finalisées (`statut` hors `valide_dec`/`cloture`) après 09h30 ; niveau `warning` seulement après 09h30 et si > 0.
   - `anomalies` — anomalies ouvertes/en cours ; `critical` si > 5, `warning` si > 0.
   - `reclamations` — réclamations non clôturées/résolues ; `warning` si > 3.
   - `creances` — créances à niveau de risque élevé/critique et statut ouvert/partiel ; `critical` si > 100 000 DA.
3. **Liste « Alertes ouvertes »** (`DecAlert[]`) : icône selon sévérité (`info`/`warning`/`critical`), titre, description, hôtel et module source (`sourceModule`), bouton « Clôturer ».

## Workflows standards

1. **Consultation** : `ipcClient.decCockpit.get(hotelId)` (canal `dec:cockpit:get`) recalcule les 6 widgets et, au passage, appelle `checkClosureDeadlineAlerts` (`electron/services/daily-closure.service.ts`) qui **crée automatiquement** une alerte DEC (`sourceModule: 'cloture_journaliere'`) pour chaque hôtel sans clôture validée après l'heure limite du jour.
2. **Liste des alertes** : `ipcClient.decCockpit.listAlerts('ouverte')` (canal `dec:alerts:list`) — triée par sévérité (critique en premier) puis date.
3. **Clôture manuelle d'une alerte** : bouton « Clôturer » → `ipcClient.decCockpit.closeAlert(alertId)` (canal `dec:alerts:close`) → `statut='cloturee'`, `closed_by`, `closed_at` renseignés, entrée dans le journal d'audit (module `dec_cockpit`). Invalide les caches `dec-alerts` et `dec-cockpit`.
4. **Création programmatique d'alertes** : d'autres modules appellent `createDecAlertIfMissing()` pour pousser une alerte dans le cockpit sans doublon (déduplication sur `source_module` + `titre` + `hotel_id` + `statut='ouverte'`). Sources actuellement identifiées dans le code : `cloture_journaliere` (clôture journalière en retard, `electron/services/daily-closure.service.ts`) et `rapprochement` (écart financier non justifié, `electron/services/finance-reconciliation.service.ts`).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — le seuil « après 09h30 » et les seuils numériques (5 anomalies, 3 réclamations, 100 000 DA de créances critiques) sont des règles de gestion interne, pas des obligations légales algériennes.

## Interconnexions

- **Clôture journalière** (via `docs/manuel-modules/recettes-journalieres.md`) : déclenche l'alerte de retard de clôture après 09h30.
- **Rapprochements** (`docs/manuel-modules/rapprochements.md`) : déclenche l'alerte d'écart financier non justifié.
- **Journal des anomalies** (`docs/manuel-modules/anomalies.md`) et **Réclamations clients** (`docs/manuel-modules/reclamations.md`) : alimentent directement les widgets `anomalies` et `reclamations`.
- **Créances & recouvrement** (`docs/manuel-modules/creances-recouvrement.md`) : alimente le widget `creances` (niveau de risque élevé/critique).
- Distinct du **Dashboard global** et du **Dashboard PDG** : le Cockpit DEC est le seul écran présentant le flux d'alertes actionnables (`dec_cockpit_alerts`) avec clôture manuelle.
- Voir aussi **Checklists contrôle interne** (`docs/manuel-modules/checklists.md`), autre outil de contrôle DEC (modèle `DEC_CA_JOUR`).

## Dépannage

- **« Accès cockpit DEC refusé. »** : l'utilisateur n'a ni rôle admin global ni hôtel assigné — vérifier son affectation dans `/admin/users`.
- **Alerte de retard de clôture qui ne disparaît pas** : elle ne se referme pas automatiquement — soit clôturer la journée dans le module Recettes/Clôture, soit clôturer manuellement l'alerte une fois le retard traité.
- **Widget « Retards clôture » toujours à 0 avant 09h30** : comportement voulu (`after0930` doit être vrai pour que le comptage s'applique).
- **Doublon d'alerte absent alors qu'un nouvel écart apparaît** : normal, la déduplication se fait sur le couple module/titre/hôtel à l'état « ouverte » — un nouveau titre ou une clôture préalable de l'ancienne alerte permet la recréation.
