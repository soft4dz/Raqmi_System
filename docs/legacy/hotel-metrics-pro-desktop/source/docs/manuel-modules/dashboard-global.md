# Dashboard global

## Présentation

Le Dashboard global est l'écran de pilotage financier et opérationnel consolidé de l'ERP. Il agrège le chiffre d'affaires, les objectifs, les encaissements et des indicateurs hôteliers (occupation, RevPAR, ADR) sur une période choisie, avec un scope automatiquement restreint au périmètre hôtel de l'utilisateur si celui-ci n'a pas d'accès consolidé.

Composant principal : `src/pages/dashboard/DashboardGlobalPage.tsx`, servi par le hook `src/hooks/useDashboard.ts`.

Public cible : direction, contrôle de gestion, comptabilité, audit interne — voir `docs/guides-utilisateurs/02-pdg.md`, `docs/guides-utilisateurs/03-directeur-unite.md` et `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route : `/dashboard` (entrée « Dashboard global » du module « Pilotage » dans `src/layouts/sidebarModules.ts`).
- Contrôle d'affichage : `canViewDashboard(role)` dans `src/shared/permissions.ts` — vrai pour les rôles admin (`SUPERADMIN`, `ADMIN_DEC`), les rôles ayant `canViewRecettes` (saisie ou validation recettes), `PDG`, `COMPTABILITE` et `AUDIT_INTERNE`. Les autres profils voient le message « Accès refusé au tableau de bord. ».
- Le scope hôtel est déterminé côté serveur par `getActorContext` : un rôle « consolidé » (accès tous hôtels) voit tous les établissements ; sinon le filtre hôtel est forcé au périmètre de l'utilisateur (`electron/services/dashboard.service.ts`, fonction `buildScope`). Le DTO renvoie `scopeHotelOnly` pour piloter l'affichage du filtre hôtel côté UI.
- Export Excel/PDF conditionné par `data.canExport` = `userHasPermission('reports.export')` OU rôle consolidé.
- Audit récent (bloc « Dernières actions utilisateur ») visible seulement si `canViewAudit` = `userHasPermission('audit.read')` ou rôle admin global.

## Écrans & champs

Écran unique en page pleine largeur, composé de blocs (`SectionBlock`) :

1. **Barre de filtres** (`src/components/dashboard/DashboardFiltersBar.tsx`) : mois (ou « Tous les mois »), année (année courante et 2 précédentes), plage de dates libre (`dateDebut`/`dateFin`), hôtel (si `showHotelFilter`), rubrique (chargée via `ipcClient.recettes.rubriques()`). Boutons « Appliquer » et « Réinitialiser ».
2. **Hero** (`DashboardHero`) : CA de la période, variation % vs période précédente, actions d'export (Excel/PDF) si autorisées.
3. **KPI structurés** (`DashboardKpiSection`) répartis en 3 blocs :
   - *Performance financière* : CA du jour, CA du mois, CA annuel, objectif mensuel, taux de réalisation.
   - *Indicateurs métiers* : taux d'occupation, RevPAR, prix moyen chambre (ADR), prix moyen couvert.
   - *Opérations & trésorerie* : encaissements, taux d'encaissement, écart objectif/réalisé, saisies réalisées, saisies manquantes.
4. **Graphiques** (`DashboardChartsSection`, chargé en lazy) : évolution journalière/mensuelle du CA, comparaison N/N-1, réalisation vs objectif mensuel, répartition par rubrique, taux d'encaissement par hôtel.
5. **Performance par unité** (`HotelAnalyseGrid`) : grille par hôtel avec réalisé, objectif, taux de réalisation, encaissements, écart et statut (`bon` / `moyen` / `critique` selon seuils 90 %/70 %).
6. **Alertes intelligentes** (`DashboardAlertsPanel`) : liste générée côté serveur (voir Workflows).
7. **Répartition des revenus** (`RubriqueBreakdown`) : part du CA par activité (Hébergement, Restauration, Boissons, Location espaces, Port/Marina, Autres).
8. **Tables de synthèse** (`DashboardTablesSection`) : Synthèse CA par hôtel, Synthèse CA par rubrique, Saisies manquantes, Dernières déclarations, et — si `canViewAudit` — Dernières actions utilisateur (journal d'audit, 10 dernières entrées).

## Workflows standards

1. **Consultation** : à l'ouverture, filtres par défaut = mois courant / année courante (`createDefaultDashboardFilters`). Toute modification de filtre reste en brouillon (`draftFilters`) jusqu'au clic sur « Appliquer » (`applyFilters`), qui déclenche `ipcClient.dashboard.get(appliedFilters)` (canal `dashboard:get`, géré par `electron/ipc/dashboard.ipc.ts` → `dashboardService.getDashboard`).
2. **Réinitialisation** : « Réinitialiser » restaure les filtres par défaut et relance la requête.
3. **Export Excel** : bouton d'export → `ipcClient.export.dashboardExcel(appliedFilters)`. Refusé silencieusement (message « Export non autorisé pour votre profil. ») si `data.canExport` est faux.
4. **Export PDF** : identique via `ipcClient.export.dashboardPdf(appliedFilters)`.
5. **Calcul des alertes** : le service `buildAlertes()` génère automatiquement des alertes (baisse de CA ≥ 20 %, taux de réalisation < 80 %, hôtel sans saisie sur la période, écart CA/encaissement > 25 %, taux d'encaissement < 60 %, saisies manquantes) — aucune action manuelle requise, purement calculé à la volée à chaque chargement.

## Règles métier DZ

Aucune règle DZ spécifique à ce module — les règles fiscales/comptables sont appliquées en amont dans les modules Recettes, Comptabilité et Fiscalité qui alimentent les données consolidées ici.

## Interconnexions

- **CA journalier (ERP)** (`docs/manuel-modules/recettes-journalieres.md`) : source principale des montants (`recettes_journalieres`, statuts `valide`/`validated`).
- **Objectifs & saisie mensuelle** (`docs/manuel-modules/budget-previsions.md`) : source des objectifs (`objectifs`) comparés au réalisé.
- **Encaissements & trésorerie** (`docs/manuel-modules/encaissements-tresorerie.md`) : source du taux d'encaissement.
- **Journalisation & traçabilité** (`docs/manuel-modules/journalisation-tracabilite.md`) : alimente le bloc « Dernières actions utilisateur ».
- **Rapports & exports** (`docs/manuel-modules/rapports-exports.md`) : l'export rapide « Tableau de bord directionnel » de l'onglet Exports rapides réutilise le même DTO et les mêmes filtres.
- Les alertes calculées ici sont indépendantes des alertes du **Cockpit DEC** (`dec_cockpit_alerts`) — ce sont deux mécanismes distincts, à ne pas confondre.

## Dépannage

- **« Accès refusé au tableau de bord. »** : le rôle de l'utilisateur ne passe pas `canViewDashboard` — vérifier le rôle dans `/admin/users` ou `/admin/roles`.
- **Page vide / KPI à zéro** : vérifier qu'il existe des lignes validées dans `recettes_journalieres` pour la période et le périmètre hôtel sélectionnés (statut doit être `valide` ou `validated`, `deleted_at IS NULL`).
- **Filtre hôtel absent** : normal pour un utilisateur non consolidé (`scopeHotelOnly = true`) ; le dashboard est alors automatiquement restreint à ses hôtels.
- **Export grisé / message « Export non autorisé »** : le rôle ne possède ni `reports.export` ni un accès consolidé — voir `docs/manuel-modules/rapports-exports.md`.
- **Écart entre CA et encaissements** : déclenche l'alerte « Écart CA / encaissement » — vérifier le rapprochement dans `docs/manuel-modules/rapprochements.md`.
