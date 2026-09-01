# 7. Plan de migration

Principe : **migration → rattachement → renommage contrôlé**, jamais suppression → réécriture. Chaque lot :
une branche, build vert, 914+ tests verts, garde readiness vert, aucune fonctionnalité `Disponible`
retirée, aucune route/table/permission supprimée sans alias d'une version.

Les six phases reprennent la stratégie imposée ; les lots sont ordonnés par les dépendances du livrable 5.

## Phase 0 — Validation (ce dossier)

- Arbitrer les 8 décisions du README.
- Trancher le lot 0 non commité : accepter (commit `feat(navigation): catalogue fonctionnel 22 domaines`),
  amender (rétablir l'affichage cadenassé dans la barre latérale) ou écarter (`git stash`).
- Commit séparé `chore(sln): normaliser RaqmiSystem.sln` ; suppression des répertoires vides parasites.

Sortie : dossier approuvé, mapping des 50 entrées signé, propriétaires de données signés.

## Phase 1 — Réorganiser l'existant sans régression

| Lot | Contenu | Touche | Ne touche pas |
|---|---|---|---|
| 1.1 Catalogue hiérarchique | `DomainNode → ModuleNode → SubmoduleNode → ScreenNode` dans `Application.Navigation` ; génération depuis une définition unique ; `TabIndex` conservés par adaptateur ; conversion `ModuleStatus` → readiness 4 niveaux | Application.Navigation, Desktop (accueil, sidebar), tests | routes, tables, permissions, écrans |
| 1.2 Rendu WPF | accueil = arbre filtrable (domaine, module) ; barre latérale par domaine ; fil d'Ariane `Domaine → Module → Sous-module → Écran` ; `SidebarLayout` retiré après une version | MainWindow (extraction du catalogue vers un contrôle dédié) | vues métier |
| 1.3 Readiness | matrice par `ScreenNode` : Domain, Application, API, PostgreSQL, RBAC, Desktop, Tests, Documentation, Smoke ; statut calculé ; `check-module-readiness.ps1` lit le nouveau catalogue | tools, docs/stabilization | — |
| 1.4 Documentation | catalogue actuel généré depuis le code ; bannière legacy ; `documentation-index.md` | docs | — |

Critères de sortie : les 30 écrans restent ouvrables par les mêmes profils ; aucun écran supplémentaire
déclaré `Functional` ; tests de navigation (arbre, filtrage par permission, absence de doublon, alias
`TabIndex`) ; smoke administrateur et profil restreint.

## Phase 2 — Stabiliser les domaines déjà fonctionnels

| Lot | Contenu |
|---|---|
| 2.1 Registre RBAC | `PermissionRegistry` `domaine.ressource.action` (ressource, action, description, propriétaire) ; 83 clés historiques mappées ; politiques acceptant clé historique **ou** nouvelle ; seeder migrant les rôles système ; rapport des rôles personnalisés ; migration d'abord Finance, PMS, Achats, RH |
| 2.2 Périmètre | `UserUnitAssignment` (utilisateur, unité/établissement, rôle optionnel, validité) ; claim de périmètre dans le JWT ; contrôle dans les services propriétaires ; filtre de navigation ; rôle `reception` |
| 2.3 Idempotence | `Idempotency-Key` sur les routes d'écriture P0 (encaissement, facture, écriture, réservation, commande) ; table `idempotency_keys` ; tests de rejeu |
| 2.4 Gate PostgreSQL | job CI avec conteneur PostgreSQL : migrations depuis zéro et depuis N-1, suite d'intégration sur base réelle pour Finance, PMS, Achats, RH |
| 2.5 Ports de lecture | casser les cycles Lodging ↔ Housekeeping / Mice / Crm et Revenue ↔ Closing par les ports du livrable 5 ; lecteurs de faits pour Kpi/Pilotage/Reporting |
| 2.6 Hygiène | extraction par cas d'usage des classes > 1 500 lignes touchées par les lots ; renommage `Application.Maintenance` → `SystemMaintenance` |

Critères : matrice RBAC testée par route (absent / historique / nouveau) ; aucune perte d'accès autorisé ;
aucune extension silencieuse ; suite PostgreSQL verte ; smoke WPF des profils Réception, Directeur d'unité,
DG, Administrateur.

## Phase 3 — Accounting & Integration Core

| Lot | Contenu |
|---|---|
| 3.1 Événements métier | enveloppe (`EventId`, `OccurredAt`, `HotelUnitCode`, `CorrelationId`, `IdempotencyKey`, payload versionné) ; outbox PostgreSQL écrite dans la transaction métier ; dispatcher interne synchrone puis asynchrone |
| 3.2 Tiers unifiés | `Accounting.Party` référence `CustomerCode` ou `SupplierCode` ; création à la demande par le propriétaire ; unicité |
| 3.3 Posting Engine | `IAccountingPostingEngine`, `PostingRule` versionnée (événement, unité, comptes, journal, TVA), génération d'écritures équilibrées sur période ouverte, contre-passation auditée, rapprochement événement ↔ règle ↔ écriture |
| 3.4 Premier lot d'émetteurs | Facturation (émission, paiement, annulation), Trésorerie (encaissement, décaissement), PMS (posting night audit, acomptes) |
| 3.5 Second lot | Achats/Stocks (réception, facture fournisseur), Paie (période clôturée) ; immobilisations quand 14 existe |
| 3.6 Integration Hub | `Integration` : ports par famille (channel, paiement, serrure, badgeuse, imprimante), registre, journal d'interfaces, supervision ; aucun module métier couplé à un fournisseur |
| 3.7 Référentiels | extraction TVA, devises, pays/wilayas/communes, unités de mesure vers 02 sans changer les valeurs |

Critères : une émission répétée ne produit qu'une écriture ; Σ D = Σ C ; période fermée refusée ; toute
écriture générée pointe vers son événement et sa règle ; E2E PMS et Facturation jusqu'à la comptabilité
verts sur PostgreSQL.

## Phase 4 — Compléter les workflows métier prioritaires

| Lot | Contenu |
|---|---|
| 4.1 Workflow étendu | sujets : demande d'achat, commande, congé, paiement, remise, clôture, décision, action corrective ; retour correction, délégation, escalade, échéance, commentaire, pièce jointe ; `IApprovalGate` généralisé |
| 4.2 Notifications | `Notification`, `NotificationPreference`, `BusinessObjectReference`, canaux interne/Mon Espace ; émetteurs : workflow, échéances, stock critique, créance échue, réservation particulière, anomalie |
| 4.3 Mon Espace | tableau de bord personnel, Mes tâches (projection `MyWorkItem`), Mes validations, Notifications, Mon activité, Mes demandes, Mes délégations (temporaires, auditées), Mon profil, Mes préférences, Ma sécurité (sessions) ; favoris et agenda ; **aucune donnée métier propre** |
| 4.4 Achats complets | expression de besoin, demande d'achat, consultation/comparatif, facture fournisseur, avoir, 3-way matching (`Commande ↔ Réception ↔ Facture`), échéances, propositions de paiement |
| 4.5 Facturation | avoirs, notes de débit, remises, échéanciers, facturation consolidée, devis/pro forma génériques |
| 4.6 Stocks | lots, expiration, FEFO, emplacements, stock de sécurité, rotation |
| 4.7 Administration système | migrations outillées, restauration testée, health checks étendus, logs, versions client/serveur, rollback, licence, diagnostic |
| 4.8 E2E | Achats : demande → validation → commande → réception → facture → paiement → comptabilité ; RH : pointage → variable → paie → paiement → comptabilité |

Critères : un utilisateur ne voit que ses éléments autorisés ; toute action repasse par le service
propriétaire ; quatre scénarios E2E P0 verts ; sauvegarde/restauration démontrée.

## Phase 5 — Channel Manager, Booking Engine, intégrations externes

| Lot | Contenu |
|---|---|
| 5.1 Messagerie | `Conversation`, `ConversationMember`, `Message`, `MessageAttachment`, `MessageReadReceipt`, référence métier autorisée seulement si lecture de l'objet ; mentions, canaux unité/direction/service, recherche, archivage, épinglage ; SignalR après validation du modèle |
| 5.2 GED | `Document`, `DocumentVersion`, `DocumentLink` (objet métier), métadonnées, confidentialité, partage, historique ; OCR/signature/archivage légal par adaptateurs |
| 5.3 Distribution | premier `IChannelManagerProvider` réel ; mapping chambres/tarifs ; publication disponibilités/tarifs/restrictions ; rejeu des réservations OTA par `ILodgingService` |
| 5.4 Booking Engine | API publique de réservation directe s'appuyant sur `ILodgingService` ; aucune disponibilité calculée hors PMS |
| 5.5 F&B / POS | points de vente, tables, commandes, tickets, transfert au folio, clôture de caisse, KDS ; food/beverage cost, gaspillage, menu engineering ; allergènes, traçabilité |
| 5.6 Domaines P1/P2 | Housekeeping étendu, CRM (prospects, réclamations), RH (badgeuses par 21, congés soldés, formation, discipline, santé), Maintenance & Patrimoine (14), MICE étendu, Juridique (16), Qualité/Audit/Contrôle interne (15), PortMaster (18), Parking (19) |

Critères : E2E F&B : commande POS → stock → folio/paiement → CA → comptabilité → KPI ; parité disponibilité
PMS ↔ Booking Engine ↔ canal testée ; aucune validation par message.

## Phase 6 — BI avancée et automatisation

- Data Warehouse et historisation ; modèles analytiques ; rapports planifiés ; KPI maintenance ;
  drill-down généralisé ; benchmark inter-unités enrichi.
- Automatisations (relances, échéances, alertes prédictives) ; copilote éventuel en dernier.

## Priorités (rappel)

| Priorité | Sujets | Phases |
|---|---|---|
| P0 | Socle ERP, Finance & Comptabilité, Accounting Posting Engine, PMS, Facturation, Achats/Stocks, RBAC, Tests E2E, Administration système | 1, 2, 3, 4 |
| P1 | F&B, Housekeeping, CRM, RH/Paie, Maintenance, Pilotage/KPI, MICE | 4, 5 |
| P2 | GED, Juridique, PortMaster, Parking, Revenue Distribution | 5 |
| P3 | BI avancée, automatisations, IA/Copilot | 6 |

## Stratégie de tests

| Niveau | Exigence | Outillage |
|---|---|---|
| Domain | invariants et transitions d'état de chaque agrégat touché | xUnit existant |
| Application | orchestration, idempotence, périmètre, ports | xUnit + doubles de ports |
| API | contrat HTTP, validation, matrice RBAC (absent / historique / nouveau) par route | `RaqmiApiFactory` |
| PostgreSQL | migrations zéro et N-1, contraintes, index, concurrence, transactions, outbox | conteneur PostgreSQL en CI (nouveau job) |
| Navigation | arbre, filtrage permission/périmètre/licence/readiness, alias `TabIndex`, absence de doublon | xUnit sur `Application.Navigation` |
| WPF | chargement de chaque écran, erreurs API, reconnexion, changement de droits | smoke automatisé (`RaqmiSystem.DocShots` étendu) + checklist manuelle |
| E2E | PMS, Achats, F&B, RH jusqu'à la comptabilité et aux KPI | suite dédiée sur PostgreSQL |
| Exploitation | backup/restore, update/rollback, health, observabilité | scripts on-premise + CI |

## Modèle de readiness

| Niveau | Définition | Preuves minimales |
|---|---|---|
| Planned | périmètre et dépendances documentés | fiche de sous-module |
| Technical Preview | noyau technique ou parcours incomplet, données non critiques | Domain + API + tests unitaires |
| Functional | parcours annoncé utilisable | Domain, Application, API, PostgreSQL (migration), RBAC, Desktop, tests, documentation |
| Production Ready | exploitable en production | + PostgreSQL réel en CI, E2E du parcours, smoke WPF, revue sécurité, exploitation (backup/restore), homologation |

Le statut est calculé depuis les preuves de la fiche du `ScreenNode` ; il n'est jamais saisi. Aucun module
ne passe `Functional` pendant le gel de stabilisation sans modification explicite de la matrice.

## Gouvernance

- Une branche par lot (`reorg/<phase>.<lot>-<sujet>`), PR vers `main` après `stabilization/module-readiness`
  et `feature/accounting-scf-core` fusionnées dans cet ordre.
- CI obligatoire : `dotnet.yml`, `stabilization.yml`, gate PostgreSQL (phase 2), E2E (phase 3+).
- Chaque lot met à jour : catalogue, readiness, documentation du module, `documentation-index.md`.
- Toute clé, route ou table historique retirée doit avoir été marquée obsolète une version avant, avec
  télémétrie d'usage nulle.
