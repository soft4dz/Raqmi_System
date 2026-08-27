# Workflows

## Présentation

Module transversal de validation par workflow : centralise les demandes en attente d'approbation, quel que soit le module métier d'origine (achats, facturation, rapprochement financier, clôture, etc.), avec un cycle de vie unique (brouillon → soumis → validation(s) → validé/refusé) et un historique complet des décisions.

Page : `src/pages/workflows/WorkflowsPage.tsx`. Sous-composant : `src/components/workflow/WorkflowHistoryWidget.tsx`. Service : `electron/services/workflow.service.ts`.

Public cible : tout valideur métier — voir `docs/guides-utilisateurs/04-controleur-exploitation.md` (contrôle/validation), `docs/guides-utilisateurs/03-directeur-unite.md`.

## Prérequis & accès

- Route : `/workflows` (« Workflows » du module « Contrôle interne » dans `sidebarModules.ts`). Aucune restriction de visibilité de menu n'est appliquée dans `sidebarModules.ts` (l'entrée est toujours visible) ; le filtrage effectif se fait côté données.
- `listPendingWorkflows` restreint automatiquement les résultats au périmètre hôtel de l'utilisateur (sauf rôle admin global) : seuls les workflows sans hôtel (`hotel_id IS NULL`) ou rattachés à un hôtel de son périmètre sont renvoyés.
- Aucune vérification de permission dédiée « approuver/refuser » n'est visible dans `workflow.ipc.ts` au-delà de l'authentification standard (`wrapIpc`) — l'accès en approbation dépend donc essentiellement du périmètre hôtel retourné par la liste.

## Écrans & champs

Écran unique en deux colonnes :

1. **Filtre hôtel** : « Tous les hôtels » ou un hôtel précis.
2. **Liste des workflows en attente** (statuts `soumis`, `en_validation`, `valide_unite`) : pour chaque carte — `module` et `entityType` + `entityId` (ex. « achats · purchase_request #42 »), `statut`, `priorite`, `commentaire` éventuel. Actions rapides : **Approuver** (icône verte) et **Refuser** (icône rouge, ouvre une modale de motif).
3. **Panneau latéral d'historique** (`WorkflowHistoryWidget`) : affiché quand un workflow est sélectionné, liste les entrées de `workflow_history` (action, ancien/nouveau statut, auteur, motif/commentaire, date).
4. **Modale de refus** : champ obligatoire « Motif du refus », boutons Annuler / Confirmer le refus.

## Workflows standards

1. **Création d'une demande** (déclenchée par les modules métier, pas par cet écran) : `ipcClient.workflow.create({ module, entityType, entityId, hotelId?, priorite?, commentaire? })` (canal `workflow:create`). Si un workflow existe déjà pour le triplet `module`/`entityType`/`entityId`, il est réutilisé (pas de doublon). Statut initial `brouillon`.
2. **Soumission** : `ipcClient.workflow.submit(...)` (canal `workflow:submit`) fait passer `brouillon` ou `refuse` → `soumis`, horodate `submitted_at`.
3. **Approbation** (depuis cet écran) : bouton « Approuver » → `ipcClient.workflow.approve(id, 'valide')` (canal `workflow:approve`). Possible uniquement depuis les statuts `soumis`, `en_validation` ou `valide_unite` ; incrémente `niveau_validation` et, si le nouveau statut est `valide`/`valide_dec`/`cloture`, horodate `completed_at`.
4. **Refus** : bouton « Refuser » → motif obligatoire → `ipcClient.workflow.reject(id, motif)` (canal `workflow:reject`) → statut `refuse`, `motif_refus` renseigné, `completed_at` horodaté. Le workflow refusé peut être resoumis (retour à `soumis`).
5. **Consultation d'un workflow lié à une entité** : `ipcClient.workflow.find(module, entityType, entityId)` (canal `workflow:find`), utilisé par les modules métier pour afficher le statut de validation d'un enregistrement (ex. une facture, une demande d'achat).
6. Chaque étape (`create`, `submit`, `approve`, `reject`) écrit une ligne dans `workflow_history` et une entrée dans le journal d'audit (`writeAuditLog`, module `workflow`).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — c'est un mécanisme générique de validation interne, sans obligation légale algérienne propre. Les règles DZ éventuelles (ex. seuils de signature) sont portées par les modules métier qui créent les workflows.

## Interconnexions

Modules identifiés dans le code comme créateurs de workflows (`createWorkflow(...)`) :

- **Achats & approvisionnements** (`docs/manuel-modules/achats-approvisionnements.md`, `electron/services/achats.service.ts`) — demandes d'achat.
- **Facturation** (`docs/manuel-modules/facturation.md`, `electron/services/facturation.service.ts`).
- **Modules légaux** (`docs/manuel-modules/modules-legaux.md`, `electron/services/inventaire-legal.service.ts`) — inventaire légal.
- **Rapprochements** (`docs/manuel-modules/rapprochements.md`, `electron/services/finance-reconciliation.service.ts`).
- **Clôture journalière** (`docs/manuel-modules/recettes-journalieres.md`, `electron/services/daily-closure.service.ts`).
- Un écart de rapprochement non justifié crée à la fois un workflow **et** une alerte Cockpit DEC (`docs/manuel-modules/dec-cockpit.md`) — les deux mécanismes sont complémentaires et indépendants.

## Dépannage

- **Un workflow attendu n'apparaît pas dans la liste** : vérifier le filtre hôtel, et que le statut est bien parmi `soumis`/`en_validation`/`valide_unite` — les workflows `brouillon` (non soumis) ne remontent jamais dans `listPending`.
- **« Approbation impossible depuis statut X. »** : le workflow n'est pas dans un statut approuvable (déjà validé, refusé ou clôturé) — consulter l'historique pour comprendre la dernière transition.
- **« Motif de refus obligatoire. »** : le champ motif de la modale de refus est vide ou ne contient que des espaces.
- **Doublon de demande** : normal — `createWorkflow` réutilise le workflow existant pour un même triplet module/entityType/entityId plutôt que d'en créer un second.
