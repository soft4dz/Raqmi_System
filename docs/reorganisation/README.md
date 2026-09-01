# Dossier de réorganisation fonctionnelle — index

Statut : **dossier préalable, en attente de validation**. Aucun renommage de table, de route ni de permission
n'a été effectué. Le build, la suite de tests (914 tests) et le garde de readiness (31/31) sont verts sur
l'état audité.

Référence d'audit : branche `feature/accounting-scf-core` (= `main` + 10 commits), 1er septembre 2026.

| # | Livrable | Fichier | Contenu |
|---|---|---|---|
| 1 | Audit de l'existant | [01-audit-existant.md](01-audit-existant.md) | chiffres vérifiés, inventaire par couche, livré vs annoncé, constats de qualité |
| 2 | Cartographie actuelle | [02-cartographie-actuelle.md](02-cartographie-actuelle.md) | 24 contextes bornés × (Domain, Application, API, PostgreSQL, RBAC, WPF, tests, doc) |
| 3 | Cartographie cible | [03-cartographie-cible.md](03-cartographie-cible.md) | 22 domaines × sous-modules avec couverture actuelle, services transversaux, sources de vérité, navigation, profils |
| 4 | Mapping ancien → nouveau | [04-mapping-existant-vers-cible.md](04-mapping-existant-vers-cible.md) | 50 entrées du catalogue, namespaces, routes, schémas, permissions, onglets |
| 5 | Dépendances | [05-dependances.md](05-dependances.md) | graphe observé dans le code, cycles à casser, flux cibles, ordre d'implémentation |
| 6 | Risques | [06-risques.md](06-risques.md) | registre des risques et parades |
| 7 | Plan de migration | [07-plan-migration.md](07-plan-migration.md) | 6 phases, lots, critères de sortie, stratégie de tests, readiness |

Synthèse de décision : [../architecture-fonctionnelle-cible.md](../architecture-fonctionnelle-cible.md).

## Décisions à prendre avant le premier lot de code

1. **Les 22 domaines et leurs identifiants stables** (`01` à `22`) tels que définis dans
   `src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.cs`.
2. **Le mapping des 50 entrées actuelles** (livrable 4, table A), en particulier les quatre rattachements
   discutables : `4` CA journalier → 03 Finance ; `22.2` Workflows → 01 Mon Espace (écran) + service
   transversal ; `23.4` Modules légaux → 14 Patrimoine ; `18` Réclamations → 04 CRM.
3. **Le référentiel client** : `Billing.Customer` reste l'unique fichier client, étendu par `Crm.GuestProfile`
   (1-1) et rattaché au tiers comptable `Accounting.Party` par référence — pas de second fichier.
4. **Le modèle de périmètre** : l'identité ne porte aujourd'hui **aucune affectation utilisateur ↔ unité**
   (claim `permission` seulement). Le filtrage de navigation « par unité/établissement » exige un modèle
   d'affectation serveur (livrable 7, phase 2). À confirmer avant de le promettre.
5. **Les services transversaux** (Workflow, Notifications, Messagerie, RBAC, Audit, Accounting Posting
   Engine, KPI Engine, Integration Hub) ne sont pas des domaines de navigation ; ils sont consommés par
   les domaines et rendus dans Mon Espace ou Administration.
6. **La compatibilité** : routes, permissions, tables et identifiants historiques restent valides au moins
   une version après l'introduction de leur remplaçant, avec alias serveur audité.
7. **Le lot 0 déjà écrit** (non commité sur la branche) : catalogue des 22 domaines, filtre de domaine sur
   l'accueil, barre latérale par domaine, `SidebarLayout` marqué obsolète, test de couverture. Il est
   compatible (aucune route, table ni permission touchée) et vert. Deux points restent à trancher :
   la barre latérale **masque** désormais les écrans verrouillés au lieu de les montrer cadenassés, et le
   filtre de domaine s'ajoute au catalogue plat plutôt que de le remplacer. Accepter, amender ou écarter.
8. **Le profil Réception** : aucun rôle système ne le représente aujourd'hui (`cashier` en est le plus
   proche). Le créer en phase 2 avec les clés fines PMS existantes.

Une fois ces points arbitrés, le premier lot d'implémentation est celui de la phase 1 du plan
(livrable 7) : catalogue hiérarchique Domaine → Module → Sous-module → Écran et adaptateur de compatibilité.
