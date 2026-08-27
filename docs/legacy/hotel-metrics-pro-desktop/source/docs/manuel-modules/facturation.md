# Facturation

## 1. Présentation

Le module **Facturation** gère le cycle de vie complet des factures clients de l'hôtel : création, soumission, validation légale (numérotation officielle, verrouillage, écriture comptable, TVA), encaissement des paiements, avoirs, et registre conforme exportable. Il consolide aussi un tableau de bord de pilotage (facturé, encaissé, taux de recouvrement).

Route d'entrée : `/facturation` (layout à onglets `FacturationIndexPage.tsx`), avec 4 sous-routes :
- `/facturation` — tableau de bord (`FacturationBoard.tsx`)
- `/facturation/factures` — liste des factures (`FacturesListPage.tsx`), `/facturation/factures/:id` pour le détail (`FactureDetailPage.tsx`)
- `/facturation/registre` — registre légal des factures validées (`FacturationRegistrePage.tsx`)
- `/facturation/nouvelle` — création (`NouvelleFacturePage.tsx`)
- Un onglet « Clients » du même layout renvoie vers `/clients` ([`clients.md`](clients.md))

Ce module s'adresse en premier lieu à la **comptabilité/trésorerie** (guide [`06-comptabilite-tresorerie.md`](../guides-utilisateurs/06-comptabilite-tresorerie.md)).

## 2. Prérequis & accès

- Route `/facturation` affichée dans le menu latéral (section « Exploitation », `src/layouts/sidebarModules.ts`) **sans condition de rôle apparente dans le menu**.
- **Contrôle réel côté serveur, plus strict que le menu** : toute action de mutation (`electron/services/facturation.service.ts`) passe par `assertCanFacturer`, qui exige `isGlobalAdminRole(actor.roleCode)` — c'est-à-dire **uniquement les rôles `SUPERADMIN` ou `ADMIN_DEC`** (même restriction que le module Clients, voir [`clients.md`](clients.md) §2). Les lectures (dashboard, liste des factures, détail) sont en revanche filtrées par périmètre hôtel (`applyActorHotelFilter`/`actorCanAccessHotel`) sans exiger le rôle admin.
- `listRegistreFactures` et l'export CSV (`exportRegistreFacturesCsv`) exigent également `assertCanFacturer` (admin global).
- Dépendances : **Clients** (référentiel `clients_facturation`, [`clients.md`](clients.md)), **Hébergement** (génération de facture depuis une réservation, [`hebergement-occupation.md`](hebergement-occupation.md)), **Comptabilité SCF** (écriture générée à la validation), **Fiscalité DGI** (TVA collectée), **Créances & recouvrement** (créance créée si solde restant à la validation).

## 3. Écrans & champs

### 3.1 Tableau de bord (`FacturationBoard.tsx`, `/facturation`)
- KPI : Facturé (TTC), Encaissé ce mois, En attente de paiement (+ nombre de factures en retard), Taux de recouvrement (encaissé/facturé).
- Bandeau d'alerte si des factures sont en retard d'échéance, avec lien vers la liste filtrée.
- Graphique « Évolution mensuelle » (facturé vs encaissé, 6 derniers mois) et bloc « Par établissement » (facturé/encaissé/en attente par hôtel).
- Tableau des 8 dernières factures (N°, unité, client, date, TTC, reste, statut).

### 3.2 Liste des factures (`FacturesListPage.tsx`, `/facturation/factures`)
- Filtres : Unité, Statut, recherche (n° facture, client), période Du/Au.
- Tableau : N°, Unité, Client, Date, Échéance (surlignée en rouge si dépassée et facture `validee`), HT, TTC, Reste, Statut, Actions.
- Actions contextuelles par statut : **Soumettre** (si `brouillon`), **Valider** (si `soumise`), **Annuler** (si `brouillon`/`soumise`, confirmation requise), **Supprimer** (si `brouillon`/`annulee`, confirmation requise).
- Statuts (`FactureStatut`) : `brouillon`, `proforma`, `soumise`, `validee`, `envoyee`, `payee_partielle`, `payee`, `annulee`, `avoir_emis`.

### 3.3 Nouvelle facture (`NouvelleFacturePage.tsx`, `/facturation/nouvelle`)
- En-tête : Établissement* (obligatoire), Client (sélection dans le référentiel Clients, ou saisie libre du nom affiché), Date d'émission* (par défaut aujourd'hui), Date d'échéance, Notes/Observations.
- Lignes de facturation (au moins une) : Désignation, Quantité, P.U. HT, TVA % (19 % par défaut), Total TTC calculé en direct. Ajout/suppression de lignes.
- Totaux : Total HT, TVA, Total TTC recalculés à chaque saisie.
- Validation front : désignation non vide et prix unitaire > 0 sur chaque ligne.

### 3.4 Détail facture (`FactureDetailPage.tsx`, `/facturation/factures/:id`)
- En-tête : numéro, badge statut, hôtel + client, actions contextuelles (PDF si `validee`/`payee`, Soumettre si `brouillon`, Valider si `soumise`, Annuler si `brouillon`/`soumise`).
- Bloc Informations (émission, échéance, client, unité, notes) et bloc Récapitulatif (HT, TVA, TTC, encaissé, reste dû).
- Tableau des lignes de facturation (désignation, qté, PU HT, TVA %, montant HT, TVA, TTC).
- Section Paiements reçus : liste (date, mode, référence, montant) + formulaire d'ajout (visible si statut `validee` et reste > 0) : Date, Montant, Mode (`especes`, `cheque`, `virement`, `carte`, `effet`, `autre`), Référence. Suppression de paiement possible par ligne.

### 3.5 Registre des factures (`FacturationRegistrePage.tsx`, `/facturation/registre`)
- Filtre période Du/Au, export CSV.
- Tableau : N°, Type (facture/avoir/proforma), Date, Client, NIF, HT, TVA, TTC, Statut — alimenté par la table `factures_registre`, remplie **uniquement au moment de la validation légale** d'une facture (voir §4.2). C'est donc un registre des documents **validés**, distinct de la liste générale des factures qui inclut aussi les brouillons.
- Remarque technique : cette page appelle directement `window.electronAPI.facturation.listRegistre`/`exportRegistreCsv` plutôt que le wrapper `ipcClient`, contrairement aux autres écrans du module.

## 4. Workflows standards

### 4.1 Créer et soumettre une facture
1. `/facturation/nouvelle` → renseigner hôtel, client, lignes → « Créer la facture » (`facturation:createFacture`). La facture est créée au statut `brouillon` avec un numéro provisoire (`BRO-<timestamp>`).
2. Tant qu'elle est `brouillon`, la facture (en-tête + lignes) reste modifiable (`facturation:updateFacture`).
3. « Soumettre » (`facturation:soumettre`) : transition `brouillon → soumise`.

### 4.2 Valider une facture (numérotation légale, verrouillage)
1. Depuis le détail ou la liste, « Valider » (`facturation:valider`), possible depuis `soumise`, `proforma` ou `brouillon`.
2. **Seuil de workflow** : si le montant TTC dépasse un seuil paramétrable (`app_settings.workflow_seuil_facture_ttc`, 500 000 DA par défaut) **ou** si le client est de type `entreprise`, un workflow d'approbation est automatiquement créé et doit être approuvé avant que la validation finale puisse aboutir (`Cette facture nécessite une approbation workflow avant validation finale.`) — voir [`workflows.md`](workflows.md).
3. Une fois les conditions remplies, le service : alloue un **numéro légal définitif** (`FAC-<exercice>-NNNNN`, ou `AV-...` pour un avoir) via un compteur séquentiel par série/exercice, **verrouille la facture** (`verrouillee = 1`), l'enregistre dans le **registre légal** (`factures_registre`), génère des **métadonnées de facturation électronique** (NIF émetteur/receveur, hash du document, statut SIFEC `prepare`), **génère l'écriture comptable** correspondante (`genererEcritureFacture`, module Comptabilité SCF), **enregistre la TVA collectée** (`enregistrerTvaVente`, module Fiscalité DGI), et **crée une créance** si un solde reste dû (`createCreanceFromFacture`, module Créances).
4. La facture passe au statut `validee`.

### 4.3 Modifier une facture verrouillée
- Une facture `brouillon` reste librement modifiable.
- Une facture **verrouillée** (validée) ne peut plus être modifiée que par un rôle admin global, et **uniquement avec un motif de modification obligatoire** (`motifModification`), tracé en audit.

### 4.4 Encaisser un paiement
1. Sur une facture `validee` avec un reste à payer > 0, « Enregistrer un paiement » → Date, Montant, Mode, Référence → `facturation:addPaiement`.
2. Le service crée l'écriture de paiement, synchronise l'encaissement vers le module Trésorerie (`syncEncaissementFacturePaiement`), recalcule le montant payé et fait évoluer le statut : `validee` → `payee_partielle` (paiement partiel) → `payee` (solde intégralement réglé).
3. Un paiement peut être supprimé (`facturation:deletePaiement`), ce qui recalcule le statut en conséquence.

### 4.5 Émettre un avoir
1. Depuis une facture `validee`/`payee`/`payee_partielle`/`envoyee`, créer un avoir (`facturation:createAvoir`) — reprend par défaut les lignes de la facture d'origine (préfixées « Avoir — ») ou des lignes personnalisées.
2. Le nouvel avoir est créé comme une facture liée (`type_document='avoir'`, `serie='AV'`, `facture_origine_id`), passe au statut `soumise`, et doit être validé comme une facture normale pour obtenir sa numérotation légale. La facture d'origine passe au statut `avoir_emis`.

### 4.6 Exporter un PDF / consulter le registre
- « PDF » (visible si statut `validee`/`payee`) → `facturation:exportPdf` génère un PDF incluant, le cas échéant, le **timbre fiscal** configuré (voir §5).
- `/facturation/registre` liste les documents validés avec export CSV (`Numéro;Type;Date;Client;NIF;HT;TVA;TTC;Statut;Exercice`).

## 5. Règles métier DZ

- **TVA** : chaque ligne de facture porte un taux de TVA (par défaut 19 %, modifiable ligne par ligne) ; les montants HT/TVA/TTC sont calculés et arrondis à 2 décimales (`calcLigne`). La TVA collectée est enregistrée dans le module Fiscalité DGI à la validation (`enregistrerTvaVente`).
- **Numérotation légale séquentielle** : les factures ne reçoivent leur numéro définitif (série `FAC` ou `AV`, par exercice comptable) qu'à la validation, via un compteur transactionnel garantissant l'unicité et la continuité de la séquence (`allocateNumeroLegal`). Avant validation, la facture porte un numéro provisoire non officiel (`BRO-<timestamp>`).
- **Verrouillage post-validation** : une fois validée, la facture est verrouillée (`verrouillee=1`) et ne peut plus être modifiée que par un admin avec motif tracé — conforme à l'exigence de non-altération des pièces comptables légales.
- **Timbre fiscal** : géré uniquement au niveau de l'export PDF (`electron/services/facturation-pdf.service.ts`), à partir du paramètre applicatif `invoice_timbre_amount` (0 par défaut, donc **non appliqué tant qu'il n'est pas configuré**). S'il est configuré, le montant est ajouté au TTC affiché sur le PDF (« Timbre fiscal : ... », « TOTAL TTC » incluant le timbre) — **ce montant n'est pas répercuté dans `montant_ttc` en base**, seulement sur le document imprimé.
- **Facturation électronique (SIFEC)** : à la validation, des métadonnées de facturation électronique sont préparées (NIF émetteur/receveur, QR payload, hash du document, statut `prepare`) — l'intégration effective avec le connecteur SIFEC est un mécanisme distinct (`sifec-connector.service.ts`), hors périmètre détaillé de cette fiche.
- **Écriture comptable automatique** : chaque facture validée génère une écriture dans le module Comptabilité SCF (`genererEcritureFacture`), garantissant la traçabilité comptable.
- **Créance automatique** : si un solde reste dû après validation, une créance est créée automatiquement dans le module Créances & recouvrement.
- **Accès restreint** : la création, modification et validation de factures est réservée aux rôles admin globaux (`SUPERADMIN`/`ADMIN_DEC`), une restriction plus stricte que ne le laisse penser la visibilité du menu.

## 6. Interconnexions

- **Clients** (`/clients`, [`clients.md`](clients.md)) : chaque facture peut référencer un client du référentiel ; les statistiques de facturation par client sont affichées sur la fiche client.
- **Hébergement & occupation** (`/hebergement`, [`hebergement-occupation.md`](hebergement-occupation.md)) : une facture peut être générée directement depuis une réservation (`createFactureFromReservation`), avec une ligne de séjour et `reservation_id` renseigné.
- **CA journalier (ERP)** (`/recettes/journalieres`, [`recettes-journalieres.md`](recettes-journalieres.md)) : les factures validées/payées du jour hors réservation hébergement alimentent la rubrique AUTRES du CA journalier (`syncAutresCaFromErp`).
- **Comptabilité SCF** (`/comptabilite`, [`comptabilite-scf.md`](comptabilite-scf.md)) : écriture comptable générée automatiquement à la validation d'une facture.
- **Fiscalité DGI** (`/fiscalite`, [`fiscalite-dgi.md`](fiscalite-dgi.md)) : TVA collectée enregistrée à la validation.
- **Créances & recouvrement** (`/creances`, [`creances-recouvrement.md`](creances-recouvrement.md)) : créance créée automatiquement si un solde reste dû après validation ; le solde de chaque facture (`montantRestant`) alimente le suivi des créances.
- **Encaissements & trésorerie** (`/encaissements`, [`encaissements-tresorerie.md`](encaissements-tresorerie.md)) : chaque paiement enregistré sur une facture est synchronisé vers la trésorerie (`syncEncaissementFacturePaiement`).
- **Workflows** (`/workflows`, [`workflows.md`](workflows.md)) : validation soumise à approbation workflow au-delà d'un seuil de montant ou pour les clients entreprise.

## 7. Dépannage

- **« Permission refusée. Rôle administrateur requis. »** : toute action de facturation (création, modification, validation, paiement, avoir, registre) est réservée aux rôles `SUPERADMIN`/`ADMIN_DEC` — cause la plus fréquente de blocage malgré un menu visible pour tous.
- **« Cette facture nécessite une approbation workflow avant validation finale. »** : le montant dépasse le seuil configuré ou le client est une entreprise — faire approuver le workflow associé (module [`workflows.md`](workflows.md)) avant de revalider.
- **« Transition invalide : X → Y »** : action tentée hors de l'ordre autorisé (ex. valider une facture déjà annulée) — vérifier le statut courant de la facture.
- **« Facture verrouillée. Modification réservée à un administrateur. »** / **« Motif obligatoire pour modifier une facture verrouillée. »** : une facture validée ne peut être corrigée que par un admin avec un motif renseigné, tracé en audit.
- **« Seules les factures brouillon peuvent être modifiées. »** : en dehors du cas admin+motif ci-dessus, l'édition des lignes/en-tête n'est possible qu'au statut `brouillon`.
- **« Seules les factures validées peuvent recevoir un paiement. »** : encaisser d'abord la validation de la facture avant de saisir un paiement.
- **« Seules les factures brouillon ou annulées peuvent être supprimées. »** : une facture soumise ou validée ne peut pas être supprimée — l'annuler d'abord si nécessaire (uniquement possible depuis `brouillon`/`soumise`).
- **« Seule une facture validée peut faire l'objet d'un avoir. »** : vérifier le statut de la facture d'origine avant de créer un avoir.
- **Numéro de facture au format `BRO-...`** : c'est normal pour une facture non encore validée — le numéro légal définitif n'est attribué qu'à la validation.
- **Timbre fiscal absent du PDF** : vérifier que le paramètre `invoice_timbre_amount` est configuré (Paramétrage global) — par défaut il vaut 0 et n'apparaît pas sur le document.
- **Écart entre le registre et la liste des factures** : normal — le registre (`/facturation/registre`) ne contient que les documents ayant atteint le statut `validee` (numérotation légale attribuée), alors que la liste générale inclut aussi les brouillons/soumises/annulées.
