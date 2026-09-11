# 02 — Fonctionnalités par module

**Révision analysée :** `b412b8c` (11 septembre 2026)
**Source :** les **469 routes réelles** de `src/RaqmiSystem.Api/Endpoints/` et
`src/RaqmiSystem.Desktop/ModuleCatalog.cs` — **pas** la documentation fonctionnelle, qui peut être
en avance sur le code.

**Total : 50 modules — 32 disponibles (✅), 18 planifiés (🔲).** Les compteurs sont gardés par un
constructeur statique qui jette si les totaux divergent (`ModuleCatalog.cs:45-49`).

---

## Socle — 3 modules, 3 disponibles

### 1 — Administration & utilisateurs ✅ *(14 routes)*
Comptes (création, modification, activation/désactivation), affectation des rôles, **affectation
des périmètres unité par utilisateur**, déverrouillage de compte, réinitialisation de mot de passe
(temporaire généré), changement de mot de passe en self-service, catalogue de permissions et de
rôles, rapport de migration RBAC.

### 2 — Paramétrage global ✅ *(2 routes)*
Identité de l'établissement, réglages du poste, santé du système (`/health`, `/health/database`).

### 3 — Unités hôtelières ✅ *(6 routes)*
Référentiel des unités et établissements : CRUD, activation/désactivation, code normalisé. Pivot
de tout le reste — `HotelUnitCode` est la clé de périmètre partout.

---

## Finance — 9 modules, 9 disponibles

### 4 — CA journalier ✅ *(14 routes)*
Saisie de la recette du jour **par catégorie paramétrable**, workflow Brouillon → Soumise →
Validée / Rejetée avec motif, résumé, tableau de bord par unité, CRUD du catalogue de catégories.
Rétrocompatibilité avec l'ancien format à quatre montants, dérivés des lignes.

### 4.5 — Clôture journalière & Night Audit ✅ *(6 routes)*
Clôture de la date métier par unité, réouverture, consultation de l'état. Night audit :
exécution et consultation, idempotent par index unique filtré. Une journée close bloque la saisie.

### 5 — Encaissements & trésorerie ✅ *(19 routes)*
Comptes bancaires et caisses, **encaissements** (création, modification, confirmation, annulation,
résumé), **ordres de paiement** (création, approbation, paiement, annulation).

### 5.2 — Comptabilité SCF ✅ *(33 routes — le plus fourni)*
Plan de comptes (classes SCF, comptes, journaux), **écritures en partie double** (création,
modification des lignes, comptabilisation, extourne, annulation), exercices et périodes avec
clôture, tiers, **lettrage / rapprochement**, grand livre par compte, **balance générale et
balance auxiliaire**, amorçage du plan SCF. La partie double est redoublée par des contraintes SQL.

### 5.4 — Fiscalité DGI & SIFEC ✅ *(21 routes)*
Registre TVA ventes, registre TVA achats (saisie + import), **calcul des déclarations TVA**,
**export G50**, marquage « déclarée », **retenues à la source**, **liasse fiscale** (génération
simple et avancée), **hub SIFEC** : transmission à l'unité ou en lot, configuration, test de
connexion.

### 6 — Budget & prévisions ✅ *(10 routes)*
Plans budgétaires, gestion des lignes (remplacement, ajout, suppression), approbation, clôture,
**analyse d'écart budget / réalisé**. Budget par catégorie de recettes paramétrable.

### 8 — Facturation ✅ *(13 routes avec le fichier clients)*
Factures clients : création, modification des lignes, **émission** avec numérotation, **règlement**
créant un encaissement réel en trésorerie, annulation. Génération PDF (QuestPDF), archivage,
réimpression.

### 9 — Créances & recouvrement ✅ *(5 routes)*
**Balance âgée**, relances, **score de risque par client**.

### 9.2 — Clients ✅
Fichier clients : CRUD, activation/désactivation, historique commercial.

---

## Exploitation — 13 modules, 8 disponibles, 5 planifiés

### 10 — Hébergement & occupation ✅ *(87 routes sur trois fichiers — le cœur du produit)*

- **Paramétrage du parc** : types de chambre, chambres, configuration des lits, extras, forfaits,
  politiques d'annulation, règles de yield.
- **Réservations** : disponibilité, création, **walk-in**, modification, garantie, affectation de
  chambre, **check-in**, préparation de départ, **check-out**, annulation, no-show, **changement
  de chambre**, prolongation, changement de type.
- **Folios** : consultation, folios multiples, ajout de charges, **transfert entre folios**,
  **facturation du folio** (`FolioInvoicingService.cs:135`).
- **Arrhes** : création, paiement, application, remboursement, confiscation.
- **Inventaire** : blocages de chambres, hors service / hors d'usage, restrictions de vente,
  **règles de surréservation**, politique d'établissement, taux d'occupation.

Anti-survente prouvé contre du vrai PostgreSQL en CI
(`tests/RaqmiSystem.Tests/Postgres/PostgresReservationConcurrencyTests.cs`).

### 10.1 — PMS front office ✅ *(10 routes)*
Date métier, **tape chart**, prévisionnel, arrivées, départs, **clients présents**, no-shows et
application automatique, night audit.

### 10.2 — Housekeeping & chambres ✅ *(19 routes)*
**Tableau gouvernante**, feuille de journée, changement d'état d'une chambre, tâches (création,
**génération automatique**, affectation, démarrage, achèvement, **inspection**, annulation),
**minibar** : articles et consommations.

### 10.4 — CRM & expérience client ✅ *(31 routes)*
**Vue client 360°**, segments, fiches clients, **consentement marketing**, **fidélité** (paliers +
comptes), **campagnes** (création, audience calculée, planification, lancement, achèvement,
annulation), **enquêtes de satisfaction + NPS**, journal des interactions.

### 10.6 — Groupes & MICE ✅ *(23 routes)*
Salles, **événements** (création, replanification, confirmation, annulation, lignes de prestation,
**planning / BEO**, **facturation**), **allotements** (confirmation, libération, annulation) et
**rooming lists**. L'allotement est déduit à la fois de la recherche de disponibilité et du garde
de création de réservation, par un calcul unique partagé.

### 11 — Stocks & consommations ✅ *(20 routes)*
Magasins et état du stock, articles, **mouvements valorisés au PMP**, **transferts
inter-magasins**, **alerte stock bas**, **inventaires physiques** avec validation des écarts.

### 11.5 — Cuisine, production & qualité ✅ *(15 routes)*
**Fiches techniques**, **calcul du coût matière lu du stock**, points de contrôle HACCP,
**relevés de température** et liste des non-conformes.
⚠️ **Hors périmètre, dit explicitement dans le code :** menu engineering, traçabilité des lots.

### 12 — Achats & approvisionnements ✅ *(13 routes)*
Fournisseurs, **bons de commande** (création, lignes, approbation avec numérotation, **réception
alimentant le stock**, annulation).
⚠️ **Hors périmètre, dit explicitement :** demandes d'achat, demandes de prix, factures
fournisseurs.

### 14.5 — Tarifs & conventions ✅ *(18 routes)*
Plans tarifaires, **périodes tarifaires**, **conventions clients**, **résolution de tarif**.

### Planifiés
- **11.6 — Points de vente (POS)** 🔲 — plan de salle, tickets, transfert au folio
- **12.5 — Appels d'offres** 🔲 — lots, ouverture des plis, attribution
- **13 — Maintenance & interventions** 🔲 — équipements, ordres de travail, préventif
- **13.5 — Intégrations matérielles** 🔲 — serrures, PBX, TPE CIB, imprimantes
- **18 — Qualité & réclamations clients** 🔲

---

## Juridique & commercial — 2 modules, 0 disponible
- **20 — Contrats & conventions** 🔲
- **20.2 — Commercial & partenariats** 🔲

---

## Ressources humaines — 2 modules, 1 disponible

### 21 — RH & paie ✅ *(41 routes — le 2ᵉ plus fourni)*
Départements, postes, **collaborateurs** (CRUD, suspension, réactivation, fin de contrat),
**contrats**, **pointages** (saisie + validation), **absences** (demande, approbation, rejet,
annulation), **paramètres de paie**, **périodes de paie** : primes, **génération des bulletins**,
validation individuelle, validation de période, clôture. Moteur de paie algérien avec tests dédiés
(`AlgerianPayrollEngineTests`).

### 21.2 — Pointeuses & badgeuses 🔲
Le module RH **consomme** des pointages, mais l'import ZKTeco et le rapprochement des badges ne
sont pas développés — le code le dit explicitement.

---

## Contrôle — 5 modules, 2 disponibles

### 22 / 30 — Audit & journalisation ✅ *(2 routes)*
Consultation de la piste d'audit avec **pagination et filtres** (date, utilisateur, action), purge
paramétrable, export. L'écriture d'audit est branchée dans tous les services métier.

### 22.2 — Workflows & validations ✅ *(12 routes)*
**Circuits d'approbation** par type de sujet, **instances** : création, approbations en attente,
approbation et rejet étape par étape. Un garde (`ApprovalsGate`) est branché sur les opérations
sensibles.

### Planifiés
**22.4 — Checklists de contrôle** 🔲 · **22.6 — Journal des anomalies** 🔲 ·
**22.8 — Décisions & instructions** 🔲

---

## Conformité & légal — 4 modules, **0 disponible**

- **23 — Conformité hôtelière** 🔲 — **fiches de police, taxe de séjour, tourisme**
- **23.2 — Protection des données** 🔲 — registre des traitements, consentements
- **23.4 — Modules légaux** 🔲 — immobilisations, CASNOS, inventaire légal
- **23.6 — Veille juridique & réglementaire** 🔲

> **C'est le groupe le plus problématique.** La fiche de police et la taxe de séjour sont des
> obligations légales immédiates pour exploiter un hôtel en Algérie : sans elles, un hôtel ne peut
> pas utiliser ce logiciel comme système unique. Voir le chantier **P-01** du plan 04.

---

## Pilotage — 6 modules, 5 disponibles

### 24 / 24.2 / 24.4 — Tableaux de bord, Dashboard PDG, Cockpit DEC ✅ *(2 routes serveur)*
Indicateurs consolidés par période et unité, **vision groupe multi-unités**, alertes de direction,
pilotage exploitation et contrôles quotidiens. Agrégation pure des modules existants : aucune
table, aucune migration, aucune permission nouvelle.

### 25 — Rapports automatiques ✅ *(3 routes)*
Catalogue de rapports paramétrables, exécution, **export CSV**, journal des exécutions.

### 25.4 — Comparatif inter-unités & bibliothèque KPI ✅ *(16 routes)*
**Bibliothèque de KPI** (liste, détail, historique), tableau de bord, **comparaison entre unités et
N/N-1**, alertes, **seuils paramétrables**, **mappings comptes comptables → KPI**, **snapshots**.
Calculateurs dédiés : finance, F&B, hébergement.

### 25.2 — Alertes & notifications 🔲

---

## Spécifique / Documentaire / Système — 5 modules, 3 disponibles

### 28 — Sauvegarde & restauration ✅ *(3 routes)*
État des sauvegardes, déclenchement à la demande, paliers de rétention.
**La restauration est volontairement hors écran** — acte d'administration serveur documenté.

### 29 — Registre des postes & erreurs clients ✅ *(4 routes)*
Heartbeat des postes, remontée des erreurs client, liste des postes avec dernier contact, journal
des erreurs.
⚠️ Anciennement « Synchronisation multi-postes », renommé : **tous les postes écrivent dans la même
base PostgreSQL, il n'y a rien à synchroniser**. Il n'existe **aucun mode hors ligne**.

### Chaîne documentaire ✅ *(2 routes, non catalogué comme module)*
Génération PDF de facture (QuestPDF), archivage, récupération du document et de ses métadonnées.

### Planifiés
**26 — PortMaster** 🔲 (bateaux, emplacements, contrats) · **27 — Gestion documentaire** 🔲 (GED,
versions, signature, archivage légal)

---

## Synthèse

| Groupe | Disponibles | Planifiés |
|---|---:|---:|
| Socle | 3 | 0 |
| Finance | 9 | 0 |
| Exploitation | 8 | 5 |
| Juridique & commercial | 0 | 2 |
| Ressources humaines | 1 | 1 |
| Contrôle | 2 | 3 |
| **Conformité & légal** | **0** | **4** |
| Pilotage | 5 | 1 |
| Spécifique / Documentaire / Système | 4 | 2 |
| **Total** | **32** | **18** |

**Trois lectures :**

1. **La finance est complète (9/9)** et c'est le point fort commercial : comptabilité SCF en
   partie double, fiscalité DGI/SIFEC, paie algérienne, trésorerie, budget. C'est davantage que la
   plupart des PMS hôteliers vendus en Algérie.
2. **L'exploitation hôtelière est solide** mais il manque le POS et la maintenance, souvent des
   attentes de base en rendez-vous client.
3. **La conformité est à zéro sur quatre.** C'est le prochain chantier fonctionnel, avant tout
   nouveau module.
