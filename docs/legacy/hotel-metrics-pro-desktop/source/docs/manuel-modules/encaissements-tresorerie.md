# Encaissements & trésorerie

## Présentation

Gestion de la trésorerie de l'établissement : tableau de bord des flux d'encaissement, liste des encaissements par mode de paiement avec circuit de confirmation/rejet, journal de caisse (entrées/sorties avec solde courant) et comptes bancaires par hôtel. La confirmation d'un encaissement génère automatiquement une écriture comptable SCF.

Composants : `src/pages/tresorerie/TresorerieIndexPage.tsx` (onglets), `TresorerieBoard.tsx`, `EncaissementsListPage.tsx`, `SaisieEncaissementPage.tsx`, `JournalCaissePage.tsx`, `ComptesBancairesPage.tsx`. Service backend : `electron/services/tresorerie.service.ts`.

Public cible : Comptabilité / Trésorerie — voir `docs/guides-utilisateurs/06-comptabilite-tresorerie.md`.

## Prérequis & accès

- Routes : `/encaissements` (tableau de bord + onglets `liste`, `caisse`, `comptes`) et `/encaissements/nouveau` — aucun wrapper de permission au niveau du routeur ; l'entrée de menu « Encaissements & trésorerie » est toujours visible dans `sidebarModules.ts` (pas de condition `visible`).
- Contrôle serveur différencié par action (`tresorerie.service.ts`) :
  - Saisir un encaissement, ajouter une opération de caisse, lister : `actorCanAccessHotel` uniquement (accès à l'hôtel, aucun rôle spécifique requis).
  - **Confirmer / rejeter / supprimer un encaissement, créer/modifier/supprimer un compte bancaire, supprimer une opération de caisse** : réservé aux rôles admin globaux (`assertCanManageTresorerie` → `isGlobalAdminRole`, c'est-à-dire `SUPERADMIN`/`ADMIN_DEC`), sinon erreur « Permission refusée : gestion encaissements. ».

## Écrans & champs

1. **Tableau de bord** (`TresorerieBoard.tsx`) : 4 KPI (Encaissé aujourd'hui, Encaissé ce mois, En attente + nombre d'opérations, Taux de couverture = confirmé / (confirmé + en attente) × 100) ; graphique en aire « Évolution des encaissements » (30 jours, montants confirmés) ; camembert « Par mode de paiement » (`especes`, `cheque`, `virement`, `carte`, `effet`, `autre`) ; tableau « Encaissements par unité » (confirmé/en attente/total) ; tableau « Derniers encaissements » (10 plus récents).
2. **Liste des encaissements** (`EncaissementsListPage.tsx`) : filtres (unité, mode, statut, période) ; colonnes Date, Unité, Mode, Référence, Description, Compte, Montant, Statut (`en_attente`/`confirme`/`rejete`), Actions (Confirmer/Rejeter si `en_attente`, Supprimer).
3. **Nouvel encaissement** (`SaisieEncaissementPage.tsx`) : Établissement*, Date*, Montant (DA)*, Mode de paiement*, Référence, Compte bancaire de destination (comptes actifs de l'hôtel), Statut initial (« En attente de confirmation » ou « Directement confirmé »), Description.
4. **Journal de caisse** (`JournalCaissePage.tsx`) : filtres unité + période ; formulaire d'ajout (Date, Libellé*, Entrée DA, Sortie DA — au moins l'un des deux non nul) ; tableau avec solde courant cumulé et totaux (entrées, sorties, solde final) ; suppression ligne par ligne.
5. **Comptes bancaires** (`ComptesBancairesPage.tsx`) : filtre unité ; formulaire création/édition (Intitulé*, Banque, N° compte, Solde initial, Actif) ; tableau avec édition inline et suppression (désactivation logique, `actif = 0`, l'enregistrement n'est jamais supprimé physiquement).

## Workflows standards

1. **Saisie d'un encaissement** (`tresorerie:encaissements:create`) : bloquée si la journée comptable est déjà clôturée pour l'hôtel (`isDateJournalLocked` — voir `electron/services/daily-closure.service.ts`), avec l'erreur « Journée clôturée. Encaissement impossible. ». Le statut initial (`en_attente` ou `confirme`) est au choix de l'utilisateur.
2. **Modification** : impossible si l'encaissement est déjà `confirme` (« Impossible de modifier un encaissement confirmé. ») ou si la journée est clôturée.
3. **Confirmation** (icône verte, admin uniquement) : `tresorerie:encaissements:confirmer` → bloquée si journée clôturée ; déclenche automatiquement `genererEcritureEncaissement` (`electron/services/comptabilite.service.ts`) : débit compte trésorerie (530000 Caisse si espèces, 512000 Banque sinon), crédit 411000 Clients, sur le journal `CA` ou `BQ`.
4. **Rejet** (icône rouge, admin uniquement, motif obligatoire) : `tresorerie:encaissements:rejeter`.
5. **Suppression** (admin uniquement) : suppression logique (`deleted_at`), bloquée si journée clôturée.
6. **Journal de caisse** : ajout libre (accès hôtel suffisant) ; le solde affiché est un cumul recalculé à l'affichage sur les lignes de la période filtrée (pas stocké ligne à ligne) — élargir la période si le solde affiché semble incohérent.
7. **Comptes bancaires** : création/édition/désactivation réservées aux rôles admin globaux.

## Règles métier DZ

Aucune règle fiscale DZ propre à ce module ; il constitue en revanche un point d'entrée de la comptabilité réglementaire algérienne : chaque confirmation d'encaissement déclenche une écriture SCF automatique (voir `comptabilite-scf.md`).

## Interconnexions

- **Comptabilité SCF** (`comptabilite-scf.md`) : écriture générée automatiquement à la confirmation d'un encaissement.
- **CA journalier (ERP)** (`recettes-journalieres.md`) : la clôture journalière (`daily-closure.service.ts`) bloque la saisie/modification/confirmation/rejet/suppression des encaissements sur une date déjà clôturée pour l'hôtel.
- **Rapprochements** (`rapprochements.md`) : les encaissements confirmés du jour, groupés par mode, alimentent le préremplissage automatique du rapprochement financier.
- **Dashboard PDG** (`dashboard-pdg.md`) : indicateur `ENCAISSEMENTS_JOUR`.

## Dépannage

- **« Journée clôturée. … impossible. »** (saisie, modification, confirmation, rejet ou suppression) : la clôture journalière de l'hôtel est déjà effectuée pour cette date — voir `recettes-journalieres.md`.
- **« Permission refusée : gestion encaissements. »** : confirmer/rejeter/supprimer un encaissement, ou gérer un compte bancaire, nécessite le rôle `SUPERADMIN` ou `ADMIN_DEC`.
- **« Impossible de modifier un encaissement confirmé. »** : rejeter puis ressaisir si nécessaire, ou passer par un administrateur pour une correction manuelle.
- **Solde du journal de caisse qui paraît incohérent** : le solde est recalculé uniquement sur la période filtrée affichée (date début → date fin) — élargir la période pour retrouver le solde d'ouverture réel.
