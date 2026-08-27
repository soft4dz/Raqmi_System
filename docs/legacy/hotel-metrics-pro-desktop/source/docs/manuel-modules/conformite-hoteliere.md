# Conformité hôtelière

## Présentation

Ce module regroupe les trois obligations réglementaires propres à l'exploitation hôtelière en Algérie : le **registre de police** (fiche de chaque client hébergé), la **taxe de séjour** communale, et les **rapports statistiques tourisme** transmis aux autorités locales. Il agit comme un module de conformité opérationnelle rattaché à la réception/hébergement.

Composant : `src/pages/hotel-legal/HotelLegalPage.tsx`, route `/hotel-legal`.

Public : direction d'unité et réception (fiches police alimentées automatiquement au check-in), direction pour le pilotage de la taxe de séjour et des rapports tourisme. Voir `docs/guides-utilisateurs/09-receptionniste.md` et `03-directeur-unite.md`.

## Prérequis & accès

- Route `/hotel-legal` déclarée dans `src/routes/AppRoutes.tsx` **sans garde de permission** particulière (accessible à tout utilisateur authentifié).
- Menu : `src/layouts/sidebarModules.ts`, section `exploitation` → « Conformité hôtelière » (`/hotel-legal`), visible sans condition de rôle.
- Chaque action de service passe par `actorCanAccessHotel(actor, hotelId)` (`electron/services/hotel-legal.service.ts`) : l'utilisateur doit avoir accès à l'unité hôtelière concernée (restriction multi-hôtel standard de l'application), mais aucun rôle spécifique n'est requis au-delà de cet accès.
- Dépend du référentiel hôtels (`useHotelsList`) pour le sélecteur d'unité, et du module Hébergement pour l'alimentation automatique des fiches police au check-in.

## Écrans & champs

Écran unique à 3 onglets, avec un sélecteur d'hôtel obligatoire en en-tête (`hotelId`, requis pour activer les requêtes).

### Onglet « Fiches police »

Liste des fiches (`hotelLegal:fichePolice:list`), une carte par client hébergé :
- Nom, prénom
- N° pièce (`numeroPiece`), date d'entrée (`dateEntree`), n° chambre (si renseigné), nationalité (si renseignée)
- Statut (`present`, `parti`, `annule`)

Champs du modèle `FichePolice` (`src/shared/types/phase2.ts`) : `hotelId`, `nom`, `prenom`, `dateNaissance`, `nationalite`, `typePiece` (`cni`, `passeport`, `permis_sejour`, `autre` — contrainte migration `058_phase2_controle_hotellerie.sql`), `numeroPiece`, `dateEntree`, `dateSortiePrevue`, `dateSortieReelle`, `chambreNumero`, `statut`.

L'écran actuel n'affiche **que la liste en lecture** ; il n'existe pas de bouton de création manuelle ni de check-out dans cette page (les IPC `hotelLegal:fichePolice:create` et `hotelLegal:fichePolice:checkout` existent côté backend mais ne sont pas câblés dans `HotelLegalPage.tsx` — voir Workflows).

### Onglet « Taxe de séjour »

Formulaire de calcul : période (sélecteur mois `YYYY-MM`), taux DZD/nuit (`tauxTaxe`, valeur par défaut `200`). Bouton « Calculer la taxe de séjour » → affiche le résultat JSON brut (`taxeResult`) dans un bloc `<pre>` (pas de mise en forme tabulaire dédiée).

### Onglet « Rapports tourisme »

Sélecteur de période + bouton « Générer rapport ». Liste des rapports existants affichée en JSON brut (`<pre>{JSON.stringify(r, null, 2)}</pre>`) — pas de mise en forme dédiée dans l'IHM actuelle.

## Workflows standards

1. **Alimentation automatique de la fiche police au check-in** — lors du check-in d'une réservation (`hebergement.service.ts`, fonction de check-in, ligne ~380), le service appelle `createFichePoliceFromReservation(actorUserId, id)` en `try/catch` silencieux (« déjà créée ou données incomplètes »). Cette fonction (`electron/services/hotel-legal.service.ts`) préremplit une fiche à partir de la réservation (nom, prénom, chambre, dates), avec `typePiece: 'carte_identite'` et `numeroPiece: 'A COMPLETER'` par défaut — **le numéro de pièce doit être complété manuellement** ensuite (via l'IPC `hotelLegal:fichePolice:create`/mise à jour, non exposé dans `HotelLegalPage.tsx` actuellement).
2. **Check-out d'une fiche police** — l'IPC `hotelLegal:fichePolice:checkout(ficheId, dateSortie)` bascule le statut à `parti` et renseigne `dateSortieReelle`. Non déclenché depuis `HotelLegalPage.tsx` dans le code actuel ; probablement appelé ailleurs (flux de check-out d'hébergement) ou à câbler.
3. **Calculer la taxe de séjour d'une période** — Onglet Taxe de séjour → saisir la période et le taux → `calculerTaxeSejour()` → IPC `hotelLegal:taxeSejour:calculer` → recense toutes les fiches police de la période (`strftime('%Y-%m', date_entree) = periode`, statuts `present` ou `parti`), une ligne de taxe par nuitée, et calcule `montantTotal = nbNuitees × tauxUnitaire`. La déclaration passe au statut `calculee` (`UPSERT` sur `taxe_sejour_declarations`, unique par `hotel_id`+`periode`) — un nouveau calcul sur la même période **écrase** les lignes précédentes.
4. **Générer le rapport tourisme mensuel** — Onglet Rapports tourisme → `genererRapportTourisme()` → IPC `hotelLegal:tourisme:generer` → agrège nationalités, nombre de clients/nuitées et taux d'occupation (`nbNuitees / (nb_chambres × 30 jours)`, arrondi à 0,1 %) sur la période, avec `UPSERT` (statut `genere`).
5. **Export CSV des fiches police** — IPC `hotelLegal:fichePolice:exportCsv(hotelId)`, non câblé dans l'IHM actuelle (disponible côté service uniquement).

## Règles métier DZ

- **Registre hôtelier (fiche police)** : obligation de tenir un registre de tous les clients hébergés (nom, prénom, pièce d'identité, nationalité, dates de séjour). Le traitement des données correspondant est référencé dans le registre RGPD sous le code `HEBERG_FICHE_POLICE` (voir `conformite-donnees-personnelles.md`), avec une durée de conservation déclarée de **5 ans** (registre hôtelier).
- **Taxe de séjour** : taxe communale calculée par nuitée, taux unitaire paramétrable (200 DZD/nuit par défaut dans l'IHM et en base — `taux_unitaire REAL NOT NULL DEFAULT 200`). Statuts de déclaration : `brouillon`, `calculee`, `declaree`, `reversee` (le passage à `declaree`/`reversee` n'est pas exposé dans l'IHM analysée).
- **Rapport tourisme** : statistiques de fréquentation par nationalité destinées aux autorités du tourisme (statuts `brouillon`, `genere`, `transmis` — seul `genere` est atteint par l'IHM actuelle).

## Interconnexions

- **Hébergement & occupation** (`hebergement-occupation.md`) — le check-in crée automatiquement la fiche police (`hebergement.service.ts` → `createFichePoliceFromReservation`).
- **Données personnelles / Loi 18-07** (`conformite-donnees-personnelles.md`) — le traitement « fiche police » (`HEBERG_FICHE_POLICE`) est déclaré au registre des traitements RGPD, avec sa politique de conservation associée (`CONS_HOTEL`, 60 mois, liée à la politique GED `LEGAL_HOTEL`).
- **GED / Archivage légal** (`ged-archivage-legal.md`) — la politique de rétention `LEGAL_HOTEL` (10 ans, catégorie GED `legal`) couvre les documents liés aux registres hôteliers archivés dans la GED.
- **Rapports & exports** (`rapports-exports.md`) — les exports CSV de fiches police sont disponibles côté service (`exportFichesPoliceCsv`).

## Dépannage

- **Le sélecteur d'hôtel est vide / aucune donnée ne se charge** : les requêtes (`fiches-police`, `rapports-tourisme`) sont conditionnées par `hotelId > 0` — sélectionner explicitement un hôtel dans le menu déroulant.
- **Une fiche police reste avec le numéro de pièce « A COMPLETER »** : c'est la valeur de préremplissage automatique au check-in (voir Workflow 1) ; elle doit être corrigée manuellement, mais l'écran actuel ne propose pas de formulaire d'édition — une évolution de l'IHM ou une intervention directe est nécessaire.
- **Recalculer la taxe de séjour change les montants d'une période déjà calculée** : normal, `calculerTaxeSejour` supprime et recrée les lignes de la déclaration à chaque appel (`DELETE FROM taxe_sejour_lignes WHERE declaration_id=?` puis réinsertion) — à n'utiliser qu'avant validation/transmission officielle.
- **Point de contrôle audit interne** : toutes les créations/mises à jour de fiches police, calculs de taxe et rapports tourisme sont tracés via `writeAuditLog` (module `hotel_legal`) — consultable dans `journalisation-tracabilite.md`.
