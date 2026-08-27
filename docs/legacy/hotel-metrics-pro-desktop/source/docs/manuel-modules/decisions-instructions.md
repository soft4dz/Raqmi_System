# Décisions & instructions

## Présentation

Outil de diffusion et de suivi des décisions/instructions de direction : un auteur rédige une décision, la diffuse à une liste de destinataires (ou à tous si aucun destinataire n'est précisé), et peut suivre le taux de lecture par personne.

Page : `src/pages/decisions/DecisionsPage.tsx`. Service : `electron/services/decisions.service.ts`.

Public cible : direction, chefs de département destinataires — voir `docs/guides-utilisateurs/02-pdg.md` et `docs/guides-utilisateurs/08-chef-departement.md`.

## Prérequis & accès

- Route : `/decisions` (« Décisions & instructions » du module « Qualité & relation client »), toujours visible dans `sidebarModules.ts`.
- Aucun contrôle de permission dédié à la création observé dans `decisions.ipc.ts` — tout utilisateur authentifié peut créer une décision et choisir ses destinataires dans la liste complète des utilisateurs (`ipcClient.users.list()`).
- Visibilité en lecture : `listDecisionsForUser` ne retient que les décisions dont l'utilisateur est auteur, destinataire explicite, ou — si la décision n'a **aucun** destinataire déclaré — toute décision non archivée (diffusion « à tous »).

## Écrans & champs

Écran unique :

1. **En-tête** avec filtre « Non lues » (bascule) et bouton « Nouvelle décision ».
2. **Liste** : `titre`, badge `priorite` (basse/normale/haute/urgente), badge `type` (stratégique/opérationnelle/rh/financière/technique/autre), compteur « Lu X/Y » si des destinataires sont définis, `dateEmission`, `dateEcheance` éventuelle, `auteurNom`. Actions : bouton œil (déplier/replier le détail — marque automatiquement la décision comme lue au premier déplaiement) et bouton Archiver.
3. **Détail déplié** : `contenu` intégral, liste des destinataires avec statut de lecture (✓ lu / non lu).
4. **Modale de création** : Titre (obligatoire), Contenu/Instructions (obligatoire), sélection multiple de destinataires (boutons-puces), Type, Priorité, Date d'échéance.

## Workflows standards

1. **Créer et diffuser une décision** : formulaire → `ipcClient.decisions.create({ titre, contenu, type, priorite, dateEcheance?, destinataireIds? })` (canal `decisions:create`). Si aucun destinataire n'est sélectionné, la décision est considérée diffusée à tous les lecteurs concernés (aucune ligne dans `decisions_destinataires`).
2. **Marquer comme lue** : ouverture du détail (clic sur l'œil) déclenche `ipcClient.decisions.marquerLu(id)` (canal `decisions:marquerLu`) qui horodate `lu_at` pour l'utilisateur courant.
3. **Filtrer les non-lues** : bascule « Non lues » → relance `listForUser({ unreadOnly: true })`, qui ne conserve que les décisions où l'utilisateur est destinataire explicite et n'a pas encore lu.
4. **Consulter les destinataires** : au dépliage, `ipcClient.decisions.destinataires(id)` (canal `decisions:destinataires`) renvoie la liste nominative avec statut de lecture.
5. **Archiver** : bouton Archiver → `ipcClient.decisions.archiver(id)` (canal `decisions:archiver`) → `statut='archivee'`, la décision disparaît des listes actives (`listDecisions`/`listDecisionsForUser` filtrent `statut != 'archivee'`).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — c'est un outil de communication interne de direction, sans obligation légale algérienne propre.

## Interconnexions

- Aucun couplage technique direct identifié avec un autre module métier (pas de création automatique de décision depuis un autre service) — le module est autonome.
- Complémentaire aux **Workflows** (`docs/manuel-modules/workflows.md`) : les décisions sont des communications à sens unique avec accusé de lecture, alors que les workflows portent une validation formelle avec approbation/refus d'une entité métier.

## Dépannage

- **Une décision « à tous » n'apparaît pas dans « Non lues » pour un utilisateur** : le filtre « non lues » ne s'applique qu'aux décisions ayant des destinataires explicites (`dests.length > 0`) — une décision sans destinataire déclaré est toujours renvoyée par la liste générale, jamais marquée « non lue » individuellement.
- **Compteur « Lu X/Y » absent** : normal si la décision n'a aucun destinataire explicite (`nbDestinataires = 0`) — dans ce cas il n'y a pas de suivi de lecture nominatif.
- **Décision invisible pour un utilisateur qui devrait la voir** : vérifier qu'il figure bien dans la liste des destinataires choisis à la création, ou qu'il est l'auteur — sinon, seules les décisions sans destinataire lui sont visibles.
