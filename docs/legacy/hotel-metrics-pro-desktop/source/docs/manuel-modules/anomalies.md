# Journal des anomalies

## Présentation

Journal transversal de suivi des anomalies opérationnelles (technique, sécurité, hygiène, service, etc.), avec gravité, statut de traitement et rattachement optionnel à un hôtel. Certaines anomalies sont créées manuellement depuis cet écran, d'autres automatiquement par d'autres modules (checklists, rapprochement financier).

Page : `src/pages/anomalies/AnomaliesPage.tsx`. Service : `electron/services/anomalies.service.ts`. Types renderer : `src/shared/types/anomalies.ts`.

Public cible : contrôleur d'exploitation, chef de département — voir `docs/guides-utilisateurs/04-controleur-exploitation.md` et `docs/guides-utilisateurs/08-chef-departement.md`.

## Prérequis & accès

- Route : `/anomalies` (« Anomalies » du module « Qualité & relation client »), toujours visible dans `sidebarModules.ts` (aucune restriction `visible` par rôle).
- Filtrage automatique par périmètre : si aucun `hotelId` n'est passé en filtre et que l'utilisateur n'a pas d'accès tous-hôtels (`!ctx.allHotelsAccess`), la liste se restreint à son premier hôtel assigné (`ctx.hotelIds[0]`).
- Aucun contrôle de permission dédié à la création/résolution observé dans `anomalies.ipc.ts` au-delà de l'authentification standard.

## Écrans & champs

Écran unique :

1. **En-tête** avec bouton « Nouvelle anomalie ».
2. **Filtres** : hôtel (« Tous les hôtels »), statut (Tous / Ouverte / En cours / Résolue / Fermée).
3. **Liste** : icône de statut, `titre`, badge de gravité (mineure/modérée/grave/critique), `typeAnomalie` (technique/sécurité/hygiène/service/autre), `description` (2 lignes max), `dateAnomalie`, `hotelNom`, `localisation`. Bouton « Résoudre » si statut `ouverte` ou `en_cours`.
4. **Modale de création** : hôtel, Titre (obligatoire), Description, Type (`typeAnomalie`), Gravité (`gravite`), Localisation.

## Workflows standards

1. **Créer une anomalie** : bouton « Nouvelle anomalie » → formulaire → `ipcClient.anomalies.create(form)` (canal `anomalies:create`).
2. **Résoudre une anomalie** : bouton « Résoudre » → `ipcClient.anomalies.update(id, { statut: 'resolue' })` (canal `anomalies:update`).
3. **Filtrage** : changement d'hôtel ou de statut relance `ipcClient.anomalies.list(hotelId, statut)` (canal `anomalies:list`).
4. **Création automatique par d'autres modules** : voir Interconnexions — le module Checklists et le module Rapprochements créent des anomalies via `createAnomalie()` sans passer par cet écran.

## Règles métier DZ

Aucune règle DZ spécifique à ce module — c'est un journal de suivi qualité/exploitation interne, sans obligation légale algérienne propre.

## Interconnexions

- **Checklists contrôle interne** (`docs/manuel-modules/checklists.md`) : toute réponse « non conforme » soumise crée une anomalie (catégorie `qualite`).
- **Rapprochements** (`docs/manuel-modules/rapprochements.md`, `electron/services/finance-reconciliation.service.ts`) : un écart financier non justifié crée à la fois une anomalie et une alerte Cockpit DEC.
- **Cockpit DEC** (`docs/manuel-modules/dec-cockpit.md`) : le widget « Anomalies ouvertes » et le KPI PDG `ANOMALIES_OUVERTES` (`docs/manuel-modules/dashboard-pdg.md`) comptent les anomalies au statut `ouverte`/`en_cours`.

## Dépannage

- **Incohérence entre le formulaire et les données enregistrées (constat de code)** : le formulaire de création (`src/pages/anomalies/AnomaliesPage.tsx`) construit son état avec les champs `typeAnomalie`, `gravite`, `localisation`, `dateAnomalie` (alignés sur `src/shared/types/anomalies.ts`), alors que la fonction serveur `createAnomalie` (`electron/services/anomalies.service.ts`) attend `categorie`, `severite`, `dateSignalement` et ne gère pas de champ `localisation`. Le canal IPC ne fait pas de mapping entre ces deux jeux de noms : une anomalie créée depuis cet écran est donc susceptible d'être enregistrée avec les valeurs par défaut du service (`categorie='autre'`, `severite='mineure'`) plutôt qu'avec le type/la gravité réellement sélectionnés dans le formulaire, et le champ « Localisation » saisi n'est pas persisté. À vérifier/corriger côté équipe technique avant de considérer la gravité affichée comme fiable.
- **Anomalie qui ne disparaît pas après résolution** : la liste reste filtrée sur l'ancien statut sélectionné — repasser le filtre sur « Toutes » ou « Résolue ».
- **Anomalie visible pour un hôtel inattendu** : vérifier le filtre hôtel actif ; sans filtre explicite, un utilisateur non consolidé ne voit que son premier hôtel assigné.
