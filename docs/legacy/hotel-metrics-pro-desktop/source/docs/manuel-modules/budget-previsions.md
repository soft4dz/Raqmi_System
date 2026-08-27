# Objectifs & saisie mensuelle

## Présentation

Deux écrans complémentaires de pilotage budgétaire par hôtel :
- **Objectifs** : fixation d'un budget mensuel de chiffre d'affaires (hébergement, restauration, boissons, autres) et d'indicateurs d'exploitation (occupation, couverts, prix moyen), avec suivi du taux de réalisation par rapport au CA réellement validé.
- **Saisie mensuelle** : consolidation officielle du CA du mois à partir du cumul des recettes journalières validées, avec justification obligatoire de tout écart et verrouillage définitif du mois une fois validé.

Composants : `src/pages/objectifs/ObjectifsPage.tsx`, `src/pages/objectifs/ObjectifFormPage.tsx`, `src/pages/recettes/SaisieMensuellePage.tsx`. Services backend : `electron/services/objectifs.service.ts`, `electron/services/recettes.service.ts` (fonctions `getRecetteMensuelle` / `saveRecetteMensuelle`).

Public cible : Directeur d'unité / Contrôleur exploitation — voir `docs/guides-utilisateurs/03-directeur-unite.md` et `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route `/objectifs` : protégée par `RequireObjectifsView` (`canViewObjectifs` = `canViewRecettes` → rôles admin globaux, `CONTROLEUR_UNITE` (permission `recettes.saisie`), `DIRECTEUR_UNITE` (permission `recettes.validate`), ou `PDG`).
- Route `/objectifs/edit` : même garde `RequireObjectifsView` côté affichage ; l'écriture réelle est en plus vérifiée côté serveur par `assertObjectifsEdit` (admin global, ou permission `recettes.saisie`/`recettes.validate`). Le bouton « Saisir objectifs » n'apparaît que si `canManageObjectifs(role)`.
- Route `/recettes/mensuelles` : protégée par `RequirePermission permission={PERMISSIONS.RECETTES_SAISIE}` — en pratique `CONTROLEUR_UNITE` ou rôles admin globaux ; côté serveur, `saveRecetteMensuelle` revérifie `assertRecettesSaisie`.
- Entrées de menu : « Objectifs » visible si `canViewObjectifs(role)`, « Saisie mensuelle » visible si `canSaisieRecettes(role)` (`src/layouts/sidebarModules.ts:92-93`).

## Écrans & champs

**Objectifs** (`ObjectifsPage.tsx`) : filtres Année (3 dernières années), Mois, Hôtel ; tableau colonnes Hôtel, Période (MM/AAAA), Objectif (total DZD), Réalisé (total DZD), Taux (vert si ≥ 100 %, orange si < 80 %), action « Modifier ».

**Formulaire objectifs** (`ObjectifFormPage.tsx`) :
- 4 cartes en lecture seule « Réalisé » (Hébergement / Restauration / Boissons / Autres), calculées à partir des recettes journalières validées du mois.
- Bloc « Période et hôtel » : Hôtel, Année, Mois (hôtel/année verrouillés si préremplis par l'URL).
- Bloc « Objectifs chiffre d'affaires (DZD) » : Hébergement, Restauration, Boissons, Autres.
- Bloc « Indicateurs complémentaires » : Capacité chambres, Chambres vendues, Taux occupation (%), Capacité restaurant, Couverts vendus, Prix moyen chambre.
- Bouton « Enregistrer » désactivé si `dto.canEdit === false`.

**Saisie mensuelle** (`SaisieMensuellePage.tsx`) :
- Sélecteurs Hôtel / Mois / Année + bouton « Charger ».
- 3 cartes KPI : Cumul journalier validé, Total mensuel saisi, Écart (orange si `|écart| > 0.01`, vert sinon).
- Tableau par rubrique : Rubrique, Cumul journalier (lecture seule), Montant mensuel (éditable, désactivé si le mois est verrouillé), Écart (calculé).
- Zone « Justification écart global », affichée seulement si `|écart| > 0.01`.
- Boutons « Enregistrer » et « Valider et verrouiller le mois » (masqués une fois `verrouille = true` ; message « Ce mois est verrouillé — plus de modification possible. » à la place).

## Workflows standards

1. **Saisie des objectifs** (`objectifs:save`) : upsert par triplet (hôtel, année, mois) ; validations serveur : mois entre 1 et 12, année entre 2000 et 2100.
2. **Calcul du taux de réalisation** (`objectifs:list`) : recalculé à chaque affichage — somme des `recettes_journalieres` au statut `valide`/`validated` du mois, divisée par la somme des 4 objectifs, arrondie à une décimale (en %).
3. **Chargement de la saisie mensuelle** (`recettes:getMensuelle`) : pour chaque rubrique active, le montant mensuel est préinitialisé au cumul journalier validé si aucune valeur n'a encore été saisie manuellement.
4. **Enregistrement** (`recettes:saveMensuelle`) : côté serveur, toute ligne dont le montant mensuel diffère du cumul journalier de plus de 0,01 DA nécessite une justification par ligne (sinon erreur « Justification obligatoire pour l'écart sur {rubrique}. ») ; un écart global non nul nécessite en plus une justification globale (« Justification globale obligatoire pour l'écart mensuel. »).
5. **Verrouillage** (« Valider et verrouiller le mois ») : statut passe à `valide`, `verrouille = 1`. Aucune fonction de déverrouillage n'a été identifiée dans le code lu — le verrou est présenté comme définitif côté IHM.

## Règles métier DZ

Aucune règle fiscale DZ spécifique à ce module — c'est un outil de pilotage budgétaire interne. Le verrouillage mensuel sert néanmoins de point de contrôle avant les usages réglementaires en aval (Comptabilité SCF, Fiscalité DGI) qui reposent sur les mêmes recettes validées.

## Interconnexions

- **CA journalier (ERP)** (`recettes-journalieres.md`) : source unique du « réalisé » (Objectifs) et du « cumul journalier » (Saisie mensuelle) — uniquement les lignes au statut `valide`/`validated` (les brouillons ne sont jamais comptés).
- **Dashboard PDG / Dashboard global** : indicateur `OBJECTIF_REALISE` (`critical` si taux < 70 %, `warning` si < 90 %), voir `dashboard-pdg.md`.
- **Rapprochements**, **Comptabilité SCF**, **Fiscalité DGI** : s'appuient sur les mêmes recettes validées une fois le mois consolidé via la Saisie mensuelle.

## Dépannage

- **« Vous n'avez pas les droits de modification. »** sur le formulaire Objectifs : l'utilisateur n'a ni permission `recettes.saisie`, ni `recettes.validate`, ni rôle admin global.
- **« Justification obligatoire pour l'écart sur … »** en Saisie mensuelle : chaque ligne modifiée par rapport au cumul journalier doit porter un texte de justification si l'écart dépasse 0,01 DA.
- **« Ce mois est verrouillé — plus de modification possible. »** : aucune action de déverrouillage n'est exposée dans le code lu — une correction nécessite une intervention directe en base par un administrateur.
- **Taux de réalisation à 0 % alors que des recettes existent** : vérifier que les recettes journalières concernées sont bien au statut « valide »/« validated » — les brouillons et recettes soumises non validées ne sont pas comptés dans le réalisé.
