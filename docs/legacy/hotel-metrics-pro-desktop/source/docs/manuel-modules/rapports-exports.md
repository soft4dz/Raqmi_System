# Rapports & exports

## Présentation

Centre de rapports transversal (« atelier Cognos ») permettant de composer des rapports à façon (lignes × colonnes × mesures, croisé dynamique, graphique) sur un catalogue sémantique de sources de données de tout l'ERP, de consulter des synthèses KPI prédéfinies, de gérer ses modèles enregistrés et de lancer des exports rapides prédéfinis.

Page : `src/pages/rapports/RapportsPage.tsx`. Sous-composants dans `src/pages/rapports/components/`. Services backend : `electron/services/reports.service.ts` et `electron/services/reports/*` (registre des sources, moteur de composition, KPI, accès).

Public cible : tout rôle disposant d'un droit d'export/création de rapport — voir `docs/guides-utilisateurs/04-controleur-exploitation.md` (contrôle/reporting), `docs/guides-utilisateurs/02-pdg.md` (lecture consolidée) et `docs/guides-utilisateurs/10-audit-interne.md`.

## Prérequis & accès

- Route : `/rapports` (« Rapports & exports » du module « Pilotage »), visible dans le menu seulement si `canExportReports(role)` (`src/shared/permissions.ts`) — vrai pour les rôles admin globaux, ceux ayant `reports.export`/`reports.create`, l'accès PortMaster, et les rôles `PDG` ou `AUDIT_INTERNE`.
- Contrôle serveur plus fin, `assertReportAccess()` (`electron/services/reports/reports-access.ts`) : accès accordé si `reports.create`, `reports.export`, rôle admin global, `PDG` ou `COMPTABILITE` ; sinon l'appel lève une erreur de permission.
- Accès par **source de données** individuelle contrôlé par `canAccessSource()` : admin global, `PDG`, `COMPTABILITE`, `AUDIT_INTERNE` voient tout ; les autres rôles doivent avoir la permission associée au module de la source (ex. `portmaster.full` pour les sources PortMaster, `rh.manage`/`rh.team` pour les sources RH, `users.manage` pour les sources Administration), ou une des permissions listées dans la définition de la source (`REPORT_SOURCES`, `electron/services/reports/reports-sources.registry.ts`).
- Le scope hôtel d'un rapport est résolu par `resolveHotelScope()` : un rôle non consolidé sans hôtel assigné ne peut lancer aucun rapport (« Aucun hôtel assigné. »).

## Écrans & champs

Quatre onglets (`Tabs` de `src/pages/rapports/RapportsPage.tsx`) :

1. **Report Studio** (`CognosReportStudio`, alias `ComposedReportBuilder`) : constructeur « glisser-déposer » façon Cognos.
   - `SemanticPackageTree` : arbre des dimensions/mesures disponibles (catalogue sémantique, `ipcClient.reports.semanticCatalog()`).
   - Trois zones de dépôt : **Lignes**, **Colonnes**, **Valeurs** (mesures uniquement en Valeurs, dimensions uniquement en Lignes/Colonnes — contrôlé à la volée avec message d'erreur).
   - Sélecteur de mise en forme (`layout`) : Liste, Croisé dynamique, Graphique (avec type de graphique `chartType`).
   - Prompts d'exécution (`prompts`) : filtres demandés à chaque lancement (ex. hôtel, dates).
   - Champs Nom / Description du modèle, case « Partagé » (`isShared`), filtre hôtel.
   - Actions : Aperçu (`previewComposed`, limité à 100 lignes affichées), Enregistrer comme modèle, Export.
   - `ReportPreviewPanel` affiche le résultat (colonnes, lignes, résumé).
2. **Synthèses KPI** (`KpiReportsTab`) : liste de KPI prédéfinis groupés par catégorie (`ipcClient.reports.catalog()`), sélection d'un KPI, filtres (hôtel, date début/fin…), boutons Aperçu et Export.
3. **Mes modèles** (`MyReportsTab`) : liste des modèles enregistrés (personnels ou partagés) avec recherche par nom/source, actions Exporter, Dupliquer, Supprimer, et historique des dernières exécutions (`ipcClient.reports.listRuns(20)`). Pour un modèle combiné avec prompts, un dialogue (`RunReportPromptDialog`) demande les valeurs des filtres avant lancement.
4. **Exports rapides** (`QuickExportsTab`) : cartes d'export Excel en un clic, indépendantes du moteur de composition — Recettes journalières, Factures port, Créances port, Contrats d'amarrage, Tableau de bord directionnel (Excel), plus un export PDF dédié du dashboard.

## Workflows standards

1. **Aperçu Report Studio** : glisser des champs sémantiques dans Lignes/Colonnes/Valeurs → `ipcClient.reports.previewComposed(composition, filters)` (canal `reports:previewComposed`). L'aperçu n'est actif que si au moins une dimension et une mesure sont posées (et lignes+colonnes si layout = croisé).
2. **Enregistrement d'un modèle combiné** : `ipcClient.reports.createTemplate({...})` (canal `reports:createTemplate`) avec `dataSource: COMPOSED_REPORT_SOURCE` et la composition sérialisée dans `filters.composition`.
3. **Export d'un rapport combiné** : `ipcClient.reports.exportComposed(composition, filters, name)` (canal `reports:exportComposed`), limité à 25 000 lignes (`EXPORT_LIMIT`), génère un classeur Excel via une boîte de dialogue d'enregistrement ; message tronqué si le total dépasse la limite.
4. **Aperçu/export d'un KPI** : `ipcClient.reports.previewKpi(kpiId, filters)` / `ipcClient.reports.exportKpi(kpiId, filters)`.
5. **Gestion des modèles** : dupliquer (`ipcClient.reports.duplicateTemplate`), supprimer (`ipcClient.reports.deleteTemplate`), relancer un modèle existant (`ipcClient.reports.exportTemplate(templateId)` pour une source classique, ou `exportComposed` avec les filtres du prompt pour un modèle combiné).
6. **Export rapide** : un clic sur une carte de l'onglet « Exports rapides » appelle `ipcClient.export.excel(kind)` (canaux gérés par `electron/ipc/export.ipc.ts`, hors périmètre `reports.ipc.ts`) ou `ipcClient.export.dashboardExcel/dashboardPdf` pour le tableau de bord.

## Règles métier DZ

Aucune règle DZ spécifique à ce module — le Centre de rapports est un outil transversal de restitution ; les règles fiscales/légales s'appliquent dans les modules sources (Fiscalité DGI, Comptabilité SCF, Paie DZ, etc.) que ce module se contente d'interroger en lecture.

## Interconnexions

- **Toutes les sources de données de l'ERP** peuvent apparaître dans le catalogue sémantique selon les droits (`REPORT_SOURCES`), organisées par catégorie : Finance, Exploitation, Ressources humaines, PortMaster, Contrôle, Commercial, Système.
- **Dashboard global** (`docs/manuel-modules/dashboard-global.md`) : l'export rapide « Tableau de bord directionnel » réutilise directement `ipcClient.export.dashboardExcel`/`dashboardPdf` avec les filtres du mois courant.
- **PortMaster facturation** (`docs/manuel-modules/portmaster-facturation.md`) : sources d'export rapide Factures/Créances/Contrats port.
- **CA journalier (ERP)** (`docs/manuel-modules/recettes-journalieres.md`) : export rapide « Recettes journalières ».
- Les modèles créés ici sont personnels par défaut ; la case « Partagé » les rend visibles aux autres utilisateurs ayant accès à la même source.

## Dépannage

- **Onglet Report Studio vide / pas de champs à glisser** : le catalogue sémantique (`ipcClient.reports.semanticCatalog()`) n'a renvoyé aucune dimension/mesure accessible — vérifier les droits de l'utilisateur sur les sources concernées.
- **Bouton Aperçu inactif** : conditions non réunies — au moins une dimension (ligne ou colonne) et une mesure sont requises ; en mode croisé, lignes **et** colonnes sont obligatoires.
- **Erreur « Accès refusé à cet hôtel. »** lors d'un export filtré : l'hôtel demandé dans les filtres n'appartient pas au périmètre de l'utilisateur.
- **Erreur « Aucun hôtel assigné. »** : l'utilisateur n'a aucun hôtel dans son périmètre et n'a pas d'accès consolidé — un rapport ne peut pas être exécuté sans scope.
- **Export tronqué avec message « Limité à 25000 lignes sur N. »** : comportement normal du plafond d'export (`EXPORT_LIMIT`) — affiner les filtres pour réduire le volume.
- **Modèle invisible dans « Mes modèles »** : soit il appartient à un autre utilisateur et n'est pas partagé, soit sa source de données n'est plus accessible au rôle courant (`canAccessSource` filtre la liste retournée par `listReportTemplates`).
