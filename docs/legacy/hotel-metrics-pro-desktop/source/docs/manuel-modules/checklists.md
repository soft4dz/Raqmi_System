# Checklists contrôle interne

## Présentation

Module de contrôle interne à base de modèles de checklists (« templates ») exécutés périodiquement par hôtel : le contrôleur démarre une exécution, renseigne un statut par item (conforme / non conforme / N/A / partiel), et la soumission calcule un score et **crée automatiquement une anomalie** pour chaque item non conforme.

Page : `src/pages/controle/ChecklistsPage.tsx`. Service : `electron/services/checklist.service.ts`.

Public cible : contrôleur d'exploitation / DEC — voir `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route : `/controle/checklists` (« Checklists » du module « Contrôle interne »).
- Démarrage d'une exécution sur un hôtel précis : contrôlé par `actorCanAccessHotel(actor, hotelId)` — refus (« Accès hôtel refusé. ») si l'hôtel n'est pas dans le périmètre de l'utilisateur.
- Aucune autre vérification de permission dédiée observée dans `checklist.ipc.ts` (accès en lecture des modèles/exécutions non filtré par rôle au-delà de l'authentification standard).

## Écrans & champs

Écran unique :

1. **Filtre hôtel**.
2. **Modèles disponibles** (`ChecklistTemplate[]`) : `libelle`, `domaine`, `frequence`, nombre d'items (`itemsCount`), bouton « Démarrer ». Modèles seedés en base (`control_checklist_templates`) :
   - `DEC_CA_JOUR` — Contrôle DEC : CA journalier et encaissements (domaine `dec`, quotidienne) — items : CA journalier saisi et validé, Encaissements rapprochés, Écarts justifiés ou nuls.
   - `QUALITE_CHAMBRES` — Qualité : chambres et espaces clients (domaine `qualite`, hebdomadaire) — item : Propreté chambres contrôlée.
   - `HYGIENE_RESTAURATION` — Hygiène : restauration et cuisine (domaine `hygiene`, hebdomadaire) — item : Hygiène cuisine vérifiée.
   - `MAINT_PREVENTIVE` — Maintenance préventive : équipements critiques (domaine `maintenance`, mensuelle) — item : Équipements critiques inspectés.
   - `SECURITE_ACCES` — Sécurité : accès, rondes et surveillance (domaine `securite`, quotidienne) — item : Rondes sécurité effectuées.
3. **Exécution en cours** (affichée si une exécution est active et a des résultats) : pour chaque item, `libelle`, sélecteur de statut (`conforme`, `non_conforme`, `na`, `partiel`) et champ Commentaire libre. Bouton « Soumettre la checklist ».
4. **Historique** : liste des exécutions (`ChecklistRun[]`) avec `templateLibelle`, `dateControle`, `statut`, `score` (%) si disponible ; clic pour rouvrir une exécution.

## Workflows standards

1. **Démarrer un contrôle** : bouton « Démarrer » sur un modèle → `ipcClient.checklist.start(templateId, hotelId)` (canal `checklist:start`) crée une ligne dans `control_checklist_runs` (statut `en_cours`, date du jour) et une ligne de résultat par item actif du modèle, initialisée à `na`.
2. **Renseigner les résultats** : chaque changement de statut ou de commentaire dans l'écran appelle immédiatement `ipcClient.checklist.updateResult(runId, itemId, { statut, commentaire })` (canal `checklist:updateResult`) — sauvegarde au fil de l'eau, pas seulement à la soumission.
3. **Soumettre la checklist** : bouton « Soumettre » → rejoue une dernière fois `updateResult` pour tout changement local non encore synchronisé, puis `ipcClient.checklist.submit(runId)` (canal `checklist:submit`) :
   - Calcule le score = (items `conforme` / items évalués hors `na`) × 100, arrondi.
   - Passe le run au statut `soumis`.
   - **Crée automatiquement une anomalie** (`createAnomalie`) pour chaque item `non_conforme` : titre « Checklist NC : {libellé de l'item} », catégorie `qualite`, sévérité `critique` si l'item est de criticité `critique`, sinon `moyenne`.
   - Journalise l'action (`writeAuditLog`, module `checklist`).
4. **Consulter l'historique** : `ipcClient.checklist.list(hotelId)` / `ipcClient.checklist.stats(hotelId)` (taux de clôture = exécutions `valide`/`cloture` sur le total).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — les checklists (hygiène, sécurité, maintenance, qualité) relèvent de procédures de contrôle interne définies par le groupe hôtelier, pas d'une obligation légale algérienne codifiée dans ce module.

## Interconnexions

- **Journal des anomalies** (`docs/manuel-modules/anomalies.md`) : toute réponse « non conforme » soumise génère une anomalie automatiquement — c'est le principal point d'entrée automatisé du journal des anomalies avec le module Rapprochements.
- **Cockpit DEC** (`docs/manuel-modules/dec-cockpit.md`) : le modèle `DEC_CA_JOUR` correspond au même contrôle quotidien CA/encaissements que surveille le cockpit, mais via un mécanisme de check-list déclaratif plutôt qu'un calcul automatique.
- **Maintenance** (`docs/manuel-modules/maintenance.md`) : le modèle `MAINT_PREVENTIVE` recoupe le suivi des interventions préventives.

## Dépannage

- **« Accès hôtel refusé. » au démarrage** : l'hôtel choisi n'est pas dans le périmètre de l'utilisateur.
- **« Exécution checklist introuvable. »** : `runId` invalide ou exécution supprimée — recharger la page pour rafraîchir l'historique.
- **Score à 0 % alors que plusieurs items sont conformes** : vérifier que les items ne sont pas tous restés à `na` (les items `na` sont exclus du calcul du score ; si tous les items évalués sont `na`, le score est forcé à 0).
- **Anomalie inattendue dans le journal des anomalies** : vérifier si elle provient d'une soumission de checklist avec un item marqué `non_conforme` — le titre commence alors par « Checklist NC : ».
