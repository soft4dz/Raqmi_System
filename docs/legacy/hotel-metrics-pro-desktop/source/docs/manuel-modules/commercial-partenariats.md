# Commercial & partenariats

## Présentation

Le module Commercial gère le **pipeline d'opportunités commerciales** (groupes, agences, entreprises, événements) et le **référentiel des partenaires** (agences de voyage, tour-opérateurs, entreprises conventionnées) d'un établissement. Il sert de CRM léger pour suivre la prospection jusqu'à la signature (« gagné ») ou l'échec (« perdu »), avec un pipeline pondéré par probabilité.

Composant unique : `src/pages/commercial/CommercialPage.tsx`, route `/commercial`.

Ce module s'adresse principalement à la direction commerciale et à la direction d'unité. Voir aussi le guide de profil correspondant dans `docs/guides-utilisateurs/` (aucun guide dédié « commercial » n'existe à ce jour ; le module est accessible depuis le profil PDG/Direction d'unité — `docs/guides-utilisateurs/02-pdg.md`, `03-directeur-unite.md`).

## Prérequis & accès

- Route `/commercial` déclarée dans `src/routes/AppRoutes.tsx` **sans garde de permission** (pas de `Require...`) : tout utilisateur authentifié qui accède à l'URL peut consulter et créer des opportunités/partenaires.
- Entrée de menu dans `src/layouts/sidebarModules.ts`, section `commercial-ged` (« Commercial & documents ») : `{ label: 'Commercial', to: '/commercial' }`, sans condition `visible` — visible à tous les rôles dans le menu.
- Aucun contrôle de permission côté service (`electron/services/commercial.service.ts` ne fait aucun appel à `assertPermission` ni `actorCanAccessHotel`).
- Dépend implicitement du référentiel utilisateurs (`users`) pour associer un commercial à chaque opportunité, et du référentiel hôtels pour le filtrage optionnel par `hotelId`.

## Écrans & champs

Écran unique à onglets (`tab` : `opportunites` | `partenaires`), avec une rangée de 4 indicateurs (KPI) en haut de page, alimentée par `ipcClient.commercial.stats()` :
- Partenaires (total actifs)
- Opportunités actives (hors gagné/perdu/annulé)
- Pipeline estimé (somme des montants estimés des opportunités actives, en DA)
- Taux de conversion (% gagné / total)

### Onglet « Opportunités »

Liste des opportunités **actives uniquement** (le statut `gagne`, `perdu` ou `annule` les sort de la liste affichée). Pour chaque carte :
- Titre, type (`groupe`, `agence`, `entreprise`, `evenement`, `autre`), statut (badge coloré)
- Partenaire lié (si renseigné), probabilité %, montant estimé (DA), date d'échéance
- Deux actions rapides : **Gagné** / **Perdu** (mettent à jour le statut directement, sans étape intermédiaire dans l'IHM)

Formulaire « Nouvelle opportunité » (modale) : titre* (obligatoire), partenaire (select optionnel), type, probabilité % (0–100), montant estimé (DA), date d'échéance.

### Onglet « Partenaires »

Liste des partenaires actifs : raison sociale, code, type (`agence`, `groupe`, `entreprise`), commission (%), e-mail.

Formulaire « Nouveau partenaire » (modale) : code*, raison sociale*, type, contact, e-mail, commission % (`remisePct`).

## Workflows standards

1. **Créer un partenaire** — Onglet Partenaires → « Nouveau partenaire » → `ipcClient.commercial.createPartenaire()` → IPC `commercial:createPartenaire` → `createPartenaire()` (`electron/services/commercial.service.ts`) → `INSERT INTO partenaires`. Le partenaire est actif par défaut (`is_active`), avec un délai de crédit par défaut de 30 jours (`creditJours`, non exposé dans le formulaire actuel).
2. **Créer une opportunité** — Onglet Opportunités → « Nouvelle opportunité » → `ipcClient.commercial.createOpportunite()` → IPC `commercial:createOpportunite` → `createOpportunite(actorId, input)` → `INSERT INTO opportunites`, avec `commercial_id = actorId` (le créateur est automatiquement affecté comme commercial suivi).
3. **Faire évoluer une opportunité** — seuls les boutons « Gagné » et « Perdu » sont câblés dans l'IHM (`updateStatut.mutate({ id, statut: 'gagne' | 'perdu' })` → IPC `commercial:updateOpportunite`). Le statut par défaut d'une opportunité créée est `prospect` (colonne `statut` de la table `opportunites`, migration `044_commercial.sql`) ; les statuts intermédiaires `en_negociation` (schéma base) existent dans le modèle de données mais **aucun contrôle de l'écran actuel ne permet de les définir** — une opportunité reste donc au statut initial jusqu'à ce qu'elle soit marquée gagnée ou perdue, sauf modification directe en base ou évolution future de l'écran.
4. **Suivre le pipeline** — les KPI et le pipeline pondéré (montant × probabilité) sont recalculés à chaque chargement via `getCommercialStats()`.

## Règles métier DZ

Aucune règle métier DZ spécifique (fiscale ou réglementaire) n'est appliquée automatiquement par ce module. Il s'agit d'un pipeline commercial interne sans génération directe de facture ni de taxe. La commission (`remisePct`) associée à un partenaire est une donnée déclarative stockée sur le partenaire ; le code ne montre aucun mécanisme qui l'applique automatiquement à une facture ou un tarif (aucune référence à `remisePct` en dehors de `commercial.service.ts` et des exports de rapports).

## Interconnexions

- **Rapports & exports** (`docs/manuel-modules/rapports-exports.md`) — deux sources d'export dédiées existent dans `electron/services/reports/reports-sources.exploitation.ts` : `opportunites` (« Pipeline commercial », colonnes incluant le montant pondéré `montant_estime × probabilite / 100`) et `partenaires` (« Partenaires commerciaux »), toutes deux catégorisées « Commercial » et soumises à la permission d'export de rapports.
- **Utilisateurs** (`administration-utilisateurs.md`) — chaque opportunité référence l'utilisateur créateur comme « commercial » suivi (`commercial_id`).
- **Hébergement / Tarifs** — le champ `hotelId` d'une opportunité permet un rattachement optionnel à une unité, mais aucun lien automatique n'existe avec les réservations ou les tarifs conventionnés (`tarifs-conventions.md`) dans le code actuel.

## Dépannage

- **Une opportunité créée n'apparaît pas dans la liste** : vérifier son statut — la liste affichée filtre les statuts `gagne`, `perdu`, `annule`. Une opportunité marquée « Perdu » par erreur ne peut pas être rouverte depuis l'IHM (pas de bouton de réouverture) ; il faut la corriger via une nouvelle opportunité ou une intervention en base.
- **Impossible de créer un partenaire** : le bouton « Créer » reste désactivé tant que `code` et `raisonSociale` ne sont pas renseignés (contrainte front). Si l'erreur survient malgré des champs remplis, vérifier l'unicité du `code` (contrainte probable en base).
- **KPI figés à `--`** : la requête `commercial:stats` a échoué (réseau IPC, base non initialisée) — vérifier le journal d'audit et les logs Electron principal.
- **Point de contrôle audit interne** : comme le module n'a aucune restriction de rôle ni de permission, tout utilisateur peut créer/modifier des opportunités et partenaires — un contrôle interne doit s'appuyer sur le journal d'audit (`journalisation-tracabilite.md`) plutôt que sur une restriction d'accès applicative.
- **Incohérence de libellés de statut** : la fonction `statutColor()` du composant (`src/pages/commercial/CommercialPage.tsx`) prévoit un badge pour les valeurs `prospection`, `qualification`, `proposition`, `negociation`, alors que la base de données (`044_commercial.sql`) et les sources de rapports (`reports-sources.exploitation.ts`) utilisent `prospect` et `en_negociation`. En pratique, ces statuts intermédiaires ne sont jamais écrits par l'IHM actuelle (voir workflow 3), donc l'écart n'a pas d'impact visible, mais il est à corriger si l'écran est complété pour permettre les transitions intermédiaires.
