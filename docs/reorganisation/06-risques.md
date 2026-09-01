# 6. Risques

Échelle : probabilité et impact de 1 (faible) à 3 (fort). Priorité = P × I.

| # | Risque | P | I | Parade | Phase |
|---|---|:-:|:-:|---|---|
| R01 | Réorganisation « big bang » : renommages massifs, régressions diffuses | 2 | 3 | lots verticaux ; build, tests et readiness verts à chaque commit ; un lot = une branche | toutes |
| R02 | Renommage immédiat des routes, tables ou permissions → rupture du client WPF et des rôles personnalisés | 2 | 3 | aucune suppression avant une version de compatibilité ; alias serveur audités ; rapport des rôles personnalisés | 1, 2 |
| R03 | **Périmètre unité absent** : la navigation « par unité/établissement » est promise alors que ni l'identité ni le JWT ne portent d'affectation | 3 | 3 | modèle `UserUnitAssignment` + claim de périmètre + contrôle dans les services (pas seulement dans le menu) ; ne pas afficher de filtre par unité avant | 2 |
| R04 | Trois référentiels de tiers (`finance.customers`, `purchasing.suppliers`, `accounting.parties`) sans lien explicite → doublons et lettrage impossible | 3 | 3 | `Party` référence `CustomerCode` ou `SupplierCode` ; création de tiers pilotée par le propriétaire ; contrôle d'unicité | 3 |
| R05 | Double comptabilisation par le Posting Engine (rejeu, double émission) | 2 | 3 | idempotency key par événement ; outbox transactionnelle ; rapprochement événement ↔ règle ↔ écriture | 3 |
| R06 | Événement perdu après commit (état métier validé sans écriture) | 2 | 3 | outbox dans la même transaction ; aucun transport asynchrone avant l'outbox | 3 |
| R07 | Absence de clé d'idempotence sur les routes d'écriture existantes (encaissement, facture, écriture) | 3 | 2 | ajouter `Idempotency-Key` optionnelle puis obligatoire sur les routes P0 ; tests de rejeu | 2, 3 |
| R08 | Workflow contourné (validation par message, par booléen local, ou sujet non couvert) | 2 | 3 | garde serveur sur chaque transition métier soumise ; `ApprovalSubjectType` étendu par sujet ; messagerie sans effet métier | 2, 4 |
| R09 | Navigation WPF considérée comme sécurité | 2 | 3 | tests API par route avec droit absent / historique / nouveau ; le masquage ne remplace jamais une politique | 1, 2 |
| R10 | Tests SQLite/InMemory trompeurs (contraintes, index, concurrence PostgreSQL) | 3 | 2 | gate PostgreSQL réel en CI (conteneur), migrations appliquées de zéro et depuis N-1 | 2 |
| R11 | Statuts trop optimistes (`Disponible` lu comme production) | 2 | 2 | readiness à 4 niveaux calculé depuis les preuves ; `Production Ready` interdit sans PostgreSQL + E2E + smoke | 1, 2 |
| R12 | Classes volumineuses (`CrmService`, `LodgingView`, `MainWindow`, `InventoryService`) → régressions lors des rattachements | 3 | 2 | extraction par cas d'usage sans réécriture ; interdiction d'ajouter du code aux fichiers > 1 500 lignes sans extraction | toutes |
| R13 | Cycles Infrastructure (Lodging ↔ Housekeeping/Mice/Crm, Revenue ↔ Closing) figeant les frontières | 2 | 2 | ports de lecture nommés (livrable 5) ; interdiction de nouvelle requête inter-contexte hors port | 2, 4 |
| R14 | Référentiel TVA logé dans `Billing.InvoiceLine` et consommé par PMS, Achats, Settings | 2 | 2 | extraire vers `Referentials.VatRate` sans changer les valeurs ; alias statique le temps de la migration | 4 |
| R15 | Lot 0 écrit avant validation (catalogue 22 domaines, sidebar par domaine) et deux changements de comportement : écrans verrouillés masqués ; filtre ajouté au catalogue plat | 2 | 1 | décision 7 du README ; si accepté, commit isolé, mise à jour du smoke test (« verrouillé et invisible » vs « verrouillé et visible ») | 0 |
| R16 | `Application.Maintenance` désigne la sauvegarde ; collision avec le domaine 14 Maintenance métier | 2 | 1 | renommer en `SystemMaintenance` avant la première entité de maintenance métier | 4 |
| R17 | Documentation legacy (85 fichiers) lue comme fonctionnel livré | 2 | 2 | bannière « référence historique » ; catalogue actuel et readiness générés depuis le code | 1 |
| R18 | Confusion `Customer` / `GuestProfile` / `Party` lors de la fiche 360 et du Posting Engine | 2 | 2 | un identifiant métier (`CustomerCode`) porté partout ; jamais de copie d'attributs | 3 |
| R19 | Messagerie temps réel (SignalR) introduite avant le modèle transactionnel et les droits | 1 | 2 | SignalR seulement après tests de révocation d'accès et de références autorisées | 5 |
| R20 | Feature flags / licence absents : impossible de masquer un domaine par contrat | 2 | 1 | nœud de catalogue avec `LicenseFeature` optionnel, évalué côté serveur dans `/me` | 1 |
| R21 | Tables MICE dans le schéma `lodging` interprétées comme appartenant au PMS | 1 | 1 | documenté ; pas de déplacement (IDs et FK stables) | — |
| R22 | Sortie de solution réécrite par VS 18 mélangée à un lot fonctionnel | 1 | 1 | commit dédié `chore(sln)` | 0 |
| R23 | Réception planifiée sans rôle système (`cashier` réutilisé par défaut) | 2 | 2 | créer `reception` en phase 2 avec les clés fines PMS ; ne pas élargir `cashier` | 2 |
| R24 | Mon Espace devenant une copie de données métier (tâches recopiées, KPI recalculés) | 2 | 3 | projections en lecture (`MyWorkItem` = référence + libellé + échéance) ; toute action renvoie au service propriétaire | 3 |
| R25 | Booking Engine ou Channel Manager recalculant l'inventaire | 1 | 3 | obligation de passer par `ILodgingService` ; tests de parité disponibilité ↔ création | 5 |
