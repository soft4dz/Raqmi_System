# Comptabilité SCF

## Présentation

Comptabilité générale conforme au SCF (Système Comptable Financier algérien) : plan comptable par classes (1 à 9), journaux auxiliaires, écritures en partie double, balance générale et gestion des exercices comptables (ouverture/clôture). Une grande partie des écritures est générée **automatiquement** par les autres modules (Facturation, Encaissements, Stocks, POS restauration) ; la « Saisie OD » permet les écritures manuelles (opérations diverses).

Composants : `src/pages/comptabilite/ComptabiliteIndexPage.tsx` (onglets), `ComptabiliteHubPage.tsx`, `ComptabilitePlanPage.tsx`, `ComptabiliteSaisiePage.tsx`, `ComptabiliteJournauxPage.tsx`, `ComptabiliteBalancePage.tsx`, `ComptabiliteExercicesPage.tsx`. Service backend : `electron/services/comptabilite.service.ts`. IPC : `electron/ipc/comptabilite.ipc.ts`.

Public cible : Comptabilité / Trésorerie — voir `docs/guides-utilisateurs/06-comptabilite-tresorerie.md`.

## Prérequis & accès

- Routes : `/comptabilite` (hub), `/comptabilite/plan`, `/comptabilite/saisie`, `/comptabilite/journaux`, `/comptabilite/balance`, `/comptabilite/exercices` — aucun wrapper de permission au niveau du routeur React ; l'entrée de menu « Comptabilité SCF » est visible pour tous les rôles dans `sidebarModules.ts`.
- Contrôle strict côté serveur : `assertCanComptabilite` (`electron/services/comptabilite.service.ts:128-134`) exige `isGlobalAdminRole(actor.roleCode)`, c'est-à-dire **uniquement** `SUPERADMIN` ou `ADMIN_DEC`. Tout autre rôle voit ses appels IPC échouer avec « Permission refusée. Rôle administrateur requis pour la comptabilité. » — les écrans se chargent alors vides ou affichent une erreur.

## Écrans & champs

1. **Hub** (`ComptabiliteHubPage.tsx`) : cartes de navigation vers les 5 écrans ci-dessous.
2. **Plan comptable** (`ComptabilitePlanPage.tsx`) : filtre par classe (1 à 9) ; tableau N° compte, Libellé, Classe, Solde (type `debit`/`credit`), Actif.
3. **Saisie OD** (`ComptabiliteSaisiePage.tsx`) : Date, Libellé, Pièce (optionnel), puis un tableau de lignes (Compte, Libellé de ligne, Débit, Crédit) avec ajout/suppression de ligne (minimum 2 lignes) ; totaux Débit/Crédit en pied de tableau ; le bouton Enregistrer est désactivé si l'écriture est déséquilibrée. Toujours postée sur le journal fixe `OD`.
4. **Journaux** (`ComptabiliteJournauxPage.tsx`) : filtres Journal / période ; tableau des écritures (Date, Journal, Pièce, Libellé, Débit, Crédit, Statut `brouillon`/`valide`).
5. **Balance** (`ComptabiliteBalancePage.tsx`) : sélection d'un Exercice (l'exercice `ouvert` est présélectionné) + période optionnelle ; tableau Compte, Libellé, Classe, Débit, Crédit, Solde (en rouge si négatif).
6. **Exercices** (`ComptabiliteExercicesPage.tsx`) : formulaire de création (Code, Libellé, Date début, Date fin) ; tableau des exercices avec statut `ouvert`/`ferme`, date de clôture, bouton « Clôturer » (confirmation requise, action irréversible dans le code lu).

## Workflows standards

1. **Écriture manuelle** (Saisie OD → `comptabilite:ecritures:create`) : `creerEcriture` vérifie l'équilibre débit = crédit (tolérance 0,01), qu'un exercice est ouvert (`getExerciceOuvert`), que la date de l'écriture est comprise dans les bornes de l'exercice, et que chaque compte existe et est actif. Créée au statut `brouillon`, sauf pour les écritures automatiques ci-dessous (`autoValider = true`, directement `valide`).
2. **Validation d'une écriture brouillon** (`comptabilite:ecritures:valider`) : passage au statut `valide`, horodatage `validated_at`.
3. **Clôture d'un exercice** (`comptabilite:exercices:cloturer`) : refusée s'il reste des écritures en `brouillon` sur l'exercice (« N écriture(s) en brouillon — validez avant clôture. ») ; sinon statut `ferme`, horodaté — aucune fonction de réouverture identifiée dans le code lu.
4. **Écritures automatiques** générées par d'autres modules (toutes directement `valide`, définies dans `comptabilite.service.ts`) :
   - `genererEcritureFacture` (journal `VE`) : facture validée → débit 411000 Clients / crédit 707100 CA hébergement + 445710 TVA collectée (sens inversé pour un avoir).
   - `genererEcritureEncaissement` (journal `CA` si espèces, `BQ` sinon) : confirmation d'un encaissement → débit 530000 Caisse ou 512000 Banque / crédit 411000 Clients.
   - `genererEcritureVariationStock` (journal `AC` si entrée issue d'une réception de bon de commande, `OD` sinon) : mouvement de stock → 311000 Stocks en contrepartie de 601000 Achats consommés (sortie) ou de 401000 Fournisseurs (entrée sur réception BC).
   - `genererEcritureVenteRestauration` (journal `CA`/`BQ` selon le mode de paiement) : vente POS restauration → trésorerie / 706100 CA restauration + 445710 TVA collectée (si TVA > 0).
   Chaque échec d'écriture automatique est journalisé en audit (action `ERROR`, module `comptabilite`) et **ne bloque pas** l'opération d'origine (la fonction retourne `null` sans lever d'exception visible côté utilisateur).
5. **Grand livre par compte** : la fonction `getGrandLivre` et le canal IPC `comptabilite:grandLivre` existent côté service, mais **aucun écran du frontend actuel ne les appelle** (aucune référence trouvée dans `src/`) — fonctionnalité disponible uniquement via l'API, pas encore exposée dans l'IHM.

## Règles métier DZ (SCF)

- Plan comptable structuré par classes 1 à 9 (le premier chiffre du numéro de compte détermine la classe), conforme à la nomenclature SCF algérienne.
- Comptes SCF codés en dur dans `COMPTES_SCF` (`electron/services/comptabilite.service.ts:113-126`) pour les écritures automatiques : Clients `411000`, Fournisseurs `401000`, TVA collectée `445710`, TVA déductible `445660`, Stocks `311000`, Achats consommés `601000`, CA Hébergement `707100`, CA Restauration `706100`, Banque `512000`, Caisse `530000`, Salaires `641000`, CNAS patronal `645100`.
- Journaux observés dans le code : `VE` (ventes), `AC` (achats), `CA` (caisse), `BQ` (banque), `OD` (opérations diverses) — la liste complète et les libellés exacts sont gérés en base via `listJournaux`, non figés côté service.
- Toute écriture doit être équilibrée (total débit = total crédit, tolérance 0,01) et datée à l'intérieur des bornes de l'exercice comptable ouvert.
- Un exercice ne peut être clôturé tant qu'il subsiste des écritures au statut `brouillon`, conformément à l'exigence SCF de tenue rigoureuse et complète des comptes avant clôture annuelle.

## Interconnexions

- **Facturation** (`facturation.md`) : génère l'écriture de vente (journal `VE`) à la validation d'une facture ou d'un avoir.
- **Encaissements & trésorerie** (`encaissements-tresorerie.md`) : génère l'écriture (journal `CA`/`BQ`) à la confirmation d'un encaissement.
- **Stocks & consommations** (`stocks-consommations.md`) : génère l'écriture (journal `AC`/`OD`) à chaque mouvement de stock valorisé.
- **Points de vente (POS)** (`pos-restauration.md`) : génère l'écriture de vente restauration (journal `CA`/`BQ`).
- **Fiscalité DGI** (`fiscalite-dgi.md`) : le registre TVA et les déclarations DGI reposent sur des tables dédiées (`registre_tva_ventes`, `registre_tva_achats`) séparées des écritures comptables SCF — aucun lien direct en base entre `ecritures_comptables` et ces tables n'a été identifié dans le code lu ; les deux modules restent donc alimentés indépendamment à partir des mêmes événements métier (facture, encaissement).

## Dépannage

- **« Permission refusée. Rôle administrateur requis pour la comptabilité. »** : le compte connecté n'a ni le rôle `SUPERADMIN` ni `ADMIN_DEC`.
- **« Écriture déséquilibrée : débit X ≠ crédit Y. »** : corriger les montants des lignes dans la Saisie OD avant d'enregistrer.
- **« Aucun exercice comptable ouvert. »** : créer un exercice depuis l'onglet Exercices avant toute saisie ou toute opération générant une écriture automatique.
- **« Date hors exercice {code}. »** : la date de l'écriture (manuelle ou automatique) ne tombe pas dans les bornes `dateDebut`/`dateFin` de l'exercice ouvert — vérifier les dates de l'exercice en cours.
- **« N écriture(s) en brouillon — validez avant clôture. »** : valider ou traiter les écritures en brouillon (onglet Journaux) avant de clôturer l'exercice.
- **Écriture automatique manquante** (ex. facture confirmée sans écriture `VE` correspondante) : vérifier le journal d'audit (module `comptabilite`, action `ERROR`) — la génération peut avoir échoué silencieusement (compte inactif, aucun exercice ouvert à la date, etc.) sans bloquer l'opération source.
