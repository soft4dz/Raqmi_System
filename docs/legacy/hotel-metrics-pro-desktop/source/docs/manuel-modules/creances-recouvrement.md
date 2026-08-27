# Créances & recouvrement

## Présentation

Suivi transversal des impayés dans une table globale (`global_creances`) alimentée par les autres modules (aujourd'hui : la Facturation), avec balance âgée par tranche d'ancienneté, historique de relances et relances automatiques programmées selon le retard de paiement.

Composant : `src/pages/creances/CreancesPage.tsx`. Service backend : `electron/services/creances.service.ts`.

Public cible : Comptabilité / Trésorerie — voir `docs/guides-utilisateurs/06-comptabilite-tresorerie.md`.

## Prérequis & accès

- Route : `/creances`, sans wrapper de permission au niveau du routeur (`src/routes/AppRoutes.tsx:258`).
- Entrée de menu « Créances » toujours visible dans `sidebarModules.ts` (aucune condition `visible`) — accessible à tout rôle authentifié.
- Contrôle serveur : `listCreances` filtre par `actor.hotelIds` pour les rôles non admin globaux ; en revanche, **aucune vérification de rôle explicite** n'encadre les actions d'écriture lues dans le code (`addCreanceRelance`, `updateCreanceStatut`, `enregistrerPaiementCreance`, `runRelancesAutomatiques`, `setRelancesAutomatiquesActives`) — tout utilisateur authentifié ayant accès à la créance concernée peut les déclencher. Chaque action reste tracée dans le journal d'audit (`writeAuditLog`, module `creances`).
- `getBalanceAgee` ne fait même pas de contrôle d'accès par hôtel (le paramètre `actorUserId` est explicitement ignoré dans le code — `void actorUserId;`), au-delà du filtre `hotelId` optionnel transmis par l'écran.

## Écrans & champs

Écran unique (`CreancesPage.tsx`) :

1. **En-tête** : case à cocher « Relances automatiques » (`creances:getRelancesAuto` / `setRelancesAuto`), bouton « Exécuter relances » (`creances:runRelancesAuto`).
2. **Filtre** : sélecteur Hôtel (« Tous les hôtels » par défaut).
3. **Balance âgée** : 4 cartes par tranche d'ancienneté — `0-30`, `31-60`, `61-90`, `90+` (jours depuis échéance) — avec montant total et nombre de créances.
4. **Liste des créances** : client (`clientLabel`), référence de pièce, date d'échéance, statut, montant restant / montant total, bouton « Relancer » (visible si `montantRestant > 0` et statut ≠ `reglee`).

Statuts (`CreanceStatut`) : `ouverte`, `partielle`, `reglee`, `litige`, `irrecouvrable`, `annulee`. Niveaux de risque (`NiveauRisque`) : `faible`, `normal`, `eleve`, `critique`.

## Workflows standards

1. **Création d'une créance depuis une facture** (`creances:fromFacture`, non déclenché depuis cet écran mais depuis le module Facturation) : possible uniquement pour une facture au statut `validee`, `payee_partielle` ou `envoyee` avec un solde restant > 0 ; une créance est déjà existante pour la facture, elle est réutilisée (pas de doublon). Le niveau de risque est calculé automatiquement (`computeRisque`) :
   - `critique` si retard > 90 jours ou montant > 500 000 DA,
   - `eleve` si retard > 60 jours ou montant > 200 000 DA,
   - `normal` si retard > 30 jours,
   - `faible` sinon.
2. **Relance manuelle** : bouton « Relancer » → `creances:relance` avec des valeurs fixées côté écran (canal `email`, objet « Relance paiement ») ; incrémente le niveau de relance, statut `preparee`, met à jour `last_relance_at`.
3. **Relances automatiques programmées** (`runRelancesAutomatiques`, déclenchable manuellement via « Exécuter relances » ou activable en tâche de fond via la case à cocher) : pour chaque créance ouverte/partielle avec solde > 0, calcul des jours écoulés depuis `date_echeance` et choix du canal :
   - ≥ 90 jours → `mise_en_demeure`,
   - ≥ 60 jours → `email`,
   - ≥ 30 jours → `telephone`,
   - < 30 jours → aucune relance.
   Une relance n'est pas recréée si une relance du même canal existe déjà pour la créance (hors statut `annulee`), ce qui évite les doublons à chaque exécution.
4. **Paiement / changement de statut** : les fonctions `creances:paiement` et `creances:updateStatut` existent côté IPC/service (paiement partiel → statut `partielle` ; solde ≤ 0 → statut `reglee`), mais aucun bouton dédié n'apparaît dans `CreancesPage.tsx` — elles sont probablement destinées à être appelées depuis un autre écran (ex. Facturation) ou restent à câbler côté IHM.
5. **Balance âgée** : recalculée à la volée à chaque chargement de l'écran, à partir de `date_echeance` par rapport à la date du jour (pas de valeur figée en base).

## Règles métier DZ

Aucune règle fiscale DZ spécifique à ce module — c'est un outil de gestion du risque client et de relance, non une déclaration réglementaire.

## Interconnexions

- **Facturation** (`facturation.md`) : source des créances (`createCreanceFromFacture`).
- **Rapprochements** (`rapprochements.md`) : le montant des créances ouvertes/partielles à la date de la journée alimente le préremplissage du rapprochement financier (`finance-reconciliation.service.ts`).
- **Dashboard PDG** (`dashboard-pdg.md`) : indicateur `CREANCES_OUVERTES` calculé à partir de `global_creances` (statuts `ouverte`/`partielle`).
- **PortMaster — Recouvrement** (`portmaster-facturation.md`) : un canal IPC dédié `portmaster:recouvrement:creances` existe (`electron/preload.ts:175`), suggérant un flux de recouvrement port distinct mais potentiellement apparenté ; non exploré en détail dans cette fiche.

## Dépannage

- **Bouton « Relancer » absent** : la créance est déjà réglée (`montantRestant = 0`) ou au statut `reglee`.
- **« Exécuter relances » ne crée rien** : vérifier que la case « Relances automatiques » est active (paramètre `creances_relances_auto` dans `app_settings`) et qu'aucune relance du même canal n'existe déjà pour les créances concernées (le mécanisme est volontairement idempotent).
- **N'importe quel utilisateur peut modifier le statut ou enregistrer un paiement d'une créance visible** : à surveiller côté contrôle interne — le service ne restreint pas ces actions à un rôle particulier (contrairement à la Comptabilité SCF ou la Fiscalité DGI) ; se référer au journal d'audit (`docs/manuel-modules/journalisation-tracabilite.md`) pour la traçabilité.
- **Balance âgée qui varie sans action visible** : normal, elle est recalculée à chaque affichage à partir de la date du jour, pas stockée.
