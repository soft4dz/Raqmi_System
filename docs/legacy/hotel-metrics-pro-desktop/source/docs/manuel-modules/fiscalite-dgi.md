# Fiscalité DGI

## Présentation

Module de conformité fiscale algérienne (Direction Générale des Impôts) : registre TVA ventes/achats, calcul et historique des déclarations TVA mensuelles, export de télédéclarations (format G50 simplifié), retenues à la source sur prestataires, liasse fiscale annuelle (codes G50/G4/G29 simplifiés), et connecteur SIFEC pour la facturation électronique / transmission à la DGI.

Composants : `src/pages/fiscalite/FiscaliteIndexPage.tsx` (onglets), `FiscaliteRegistreTvaPage.tsx`, `FiscaliteTvaAchatsPage.tsx`, `FiscaliteDeclarationTvaPage.tsx`, `FiscaliteTeledeclarationsPage.tsx`, `FiscaliteRetenuePage.tsx`, `FiscaliteLiassePage.tsx`, `sifec/SifecHubPage.tsx`, `sifec/SifecFacturesPage.tsx`, `sifec/SifecConfigPage.tsx`. Services backend : `electron/services/fiscalite-dz.service.ts`, `electron/services/fiscalite-avancee.service.ts`, `electron/services/sifec-connector.service.ts`.

Public cible : Comptabilité / Trésorerie — voir `docs/guides-utilisateurs/06-comptabilite-tresorerie.md`.

## Prérequis & accès

- Routes : `/fiscalite/registre-tva` (par défaut), `/fiscalite/tva-achats`, `/fiscalite/declaration-tva`, `/fiscalite/teledeclarations`, `/fiscalite/retenue-source`, `/fiscalite/liasse`, `/fiscalite/sifec`, `/fiscalite/sifec/factures`, `/fiscalite/sifec/config` — aucun wrapper de permission au niveau du routeur React ; l'entrée de menu « Fiscalité DGI » est visible pour tous les rôles dans `sidebarModules.ts`.
- Contrôle strict côté serveur, cohérent sur les trois services :
  - `assertCanFiscalite` (`fiscalite-dz.service.ts`, `fiscalite-avancee.service.ts`) et `assertCanSifec` (`sifec-connector.service.ts`) exigent tous `isGlobalAdminRole(actor.roleCode)`, c'est-à-dire **uniquement** `SUPERADMIN` ou `ADMIN_DEC`.
  - Tout autre rôle obtient « Permission refusée pour la fiscalité. » ou « Permission refusée pour le connecteur SIFEC. » sur chaque appel IPC — écrans vides ou en erreur pour les rôles non admin.
  - Exception : `registerTvaAchatFromBonLivraison` (alimentation automatique du registre TVA achats depuis une réception de bon de commande) n'effectue **aucun** contrôle de rôle — appelée depuis le module Achats, pas depuis l'IHM Fiscalité.

## Écrans & champs

1. **Registre TVA** (`FiscaliteRegistreTvaPage.tsx`) : sélecteur Période (mois) ; boutons Actualiser, Générer, Export CSV ; tableau Pièce, Date, Client, Type (`vente`/`avoir`), Base HT, TVA, TTC.
2. **TVA achats** (`FiscaliteTvaAchatsPage.tsx`) : sélecteur Période ; boutons « Importer bons validés » (import depuis les bons de commande validés de la période) et « Export CSV » ; tableau Date, N° pièce, Fournisseur, Base HT, TVA, TTC, Source (`manuel` / `achats` selon l'origine).
3. **Déclaration TVA** (`FiscaliteDeclarationTvaPage.tsx`) : sélecteur Période ; bouton « Calculer déclaration » ; cartes Base HT ventes, TVA collectée, TVA déductible, Crédit antérieur, Solde à payer + statut ; tableau « Historique des déclarations » (toutes périodes).
4. **Télédéclarations** (`FiscaliteTeledeclarationsPage.tsx`) : sélecteur Période + bouton « Exporter G50 TVA » (génère un CSV et enregistre une télédéclaration) ; champ « Référence DGI (après dépôt portail) » + bouton « Marquer déclarée » (sur la ligne sélectionnée) ; tableau des télédéclarations (Type, Période, Montant, Statut, Réf. DGI, date d'export).
5. **Retenue à la source** (`FiscaliteRetenuePage.tsx`) : formulaire Fournisseur*, Base HT, Taux (%), Date ; tableau historique Fournisseur, Date, Base HT, Taux, Montant retenu.
6. **Liasse fiscale** (`FiscaliteLiassePage.tsx`) : sélecteur Exercice (année) ; boutons Actualiser, « Générer liasse » (version simple), « Liasse avancée » (version étendue G50/G4/G29), Export CSV ; tableau Code G50, Libellé, Montant.
7. **SIFEC — Hub** (`sifec/SifecHubPage.tsx`) : cartes « En attente SIFEC », « Soumis », « Acceptés DGI », « Rejetés », « Erreurs », « Mode connecteur » (`sandbox`/`production`) ; message rappelant qu'en sandbox les transmissions sont simulées localement.
8. **SIFEC — Transmissions** (`sifec/SifecFacturesPage.tsx`) : bouton « Transmettre le lot » (factures non `accepte`) ; tableau N° facture, Date, Client, TTC, Statut SIFEC (`prepare`/`soumis`/`accepte`/`rejete`/`erreur`), UID, action « Envoyer » par facture.
9. **SIFEC — Config** (`sifec/SifecConfigPage.tsx`) : Mode (Sandbox/Production), URL API DGI, Référence clé API (vault), NIF déclarant, case « Connecteur actif », affichage du dernier test de connexion ; boutons Enregistrer et « Tester connexion ».

## Workflows standards

1. **Registre TVA ventes** : alimenté par `enregistrerTvaVente` (appelée depuis la Facturation à l'émission d'une facture/avoir, taux TVA par défaut `19` si non précisé) ; le bouton « Générer » ne fait que relister la période (`genererRegistreTvaMensuel` est un simple alias de lecture, il ne recalcule rien à partir d'autres sources).
2. **Registre TVA achats** : alimenté soit manuellement (formulaire, non exposé dans l'écran actuel mais disponible côté IPC `fiscalite:achats:create`), soit par import en lot des bons de commande validés de la période (« Importer bons validés » → `importTvaAchatsFromBons`, taux fixé à 19 %, ignore les bons déjà importés via `achat_ref_id`), soit automatiquement à la réception d'un bon de commande (`registerTvaAchatFromBonLivraison`, taux recalculé depuis le ratio TVA/Base HT de la pièce).
3. **Calcul de la déclaration TVA mensuelle** (« Calculer déclaration » → `calculerDeclarationTva`) :
   - `TVA collectée` = somme des `montant_tva` du registre ventes de la période (ventes − avoirs).
   - `TVA déductible` = somme des `montant_tva` du registre achats de la période.
   - `Crédit antérieur` = valeur absolue du solde de la déclaration calculée la plus récente antérieure, si ce solde était négatif (crédit de TVA reporté).
   - `Solde` = TVA collectée − TVA déductible − crédit antérieur (positif = à payer, négatif = crédit reporté).
   - Chaque calcul upsert la déclaration de la période au statut `calculee`.
4. **Export télédéclaration TVA G50** (« Exporter G50 TVA ») : recalcule la déclaration de la période puis génère un CSV à 6 champs (`BASE_HT_VENTES`, `TVA_COLLECTEE`, `TVA_DEDUCTIBLE`, `CREDIT_ANTERIEUR`, `SOLDE_A_PAYER`) et enregistre/actualise une télédéclaration au statut `exportee`.
5. **Marquage « déclarée »** : après dépôt sur le portail DGI (hors application), l'utilisateur saisit la référence DGI et clique « Marquer déclarée » → statut `declaree` sur la télédéclaration, et répercuté sur la déclaration TVA correspondante si `typeDecl = 'tva'`.
6. **Retenue à la source** : `enregistrerRetenueSource` calcule `montantRetenu = round(baseHt × taux) / 100`, où `taux` est attendu par le service comme un **nombre de pourcentage entier** (ex. `15` pour 15 %, valeur par défaut serveur si non transmis). Le formulaire `FiscaliteRetenuePage.tsx` initialise le champ à `taux: 5` (5 %) et transmet directement cette valeur en pourcentage entier au service, cohérent avec la convention `taux` de la colonne `retenues_source.taux` (défaut `15`) et avec `tauxTva` (défaut `19`) ailleurs dans le module.
7. **Liasse fiscale (simple)** : `genererLiasseFiscale` génère 3 lignes (`G50-001` CA HT, `G50-010` TVA collectée, `G29-001` résultat comptable simplifié = CA HT × 0,15) à partir du registre TVA ventes de l'exercice uniquement.
8. **Liasse fiscale avancée** (« Liasse avancée ») : `genererLiasseFiscaleAvancee` génère 9 lignes croisant ventes et achats de l'exercice : `G50-001` CA HT, `G50-002` Achats et charges HT, `G50-010` TVA collectée, `G50-011` TVA déductible, `G50-012` Crédit de TVA antérieur (cumul des soldes négatifs des déclarations de l'exercice), `G50-013` Solde TVA à payer, `G4-001` Résultat fiscal simplifié, `G29-001`/`G29-002`... voir Règles métier DZ ci-dessous pour le détail des taux appliqués.
9. **SIFEC — préparation et transmission** : `prepareFactureSifec` construit le payload (NIF émetteur/récepteur, montants, hash du document, horodatage) et un QR code (`buildQrPayload`, hash SHA-256 tronqué), statut `prepare`. `submitFactureSifec` transmet (simulation en mode `sandbox` : acceptation systématique avec un UID généré ; en mode `production`, échec systématique tant que l'intégration API DGI n'est pas implémentée dans le code lu) et journalise la transmission. « Transmettre le lot » enchaîne préparation + soumission pour toutes les factures non encore `accepte`. Un échec de transmission (`rejete`/`erreur`) crée un workflow de suivi (module `sifec`, priorité haute).

## Règles métier DZ

- **TVA** : taux par défaut appliqué dans le code = **19 %** (`enregistrerTvaVente`, `importTvaAchatsFromBons`, `createRegistreTvaAchat` — valeur `taux ?? 19`), conforme au taux normal de TVA en Algérie ; le taux réel de chaque pièce peut néanmoins être surchargé au cas par cas (`tauxTva` optionnel).
- **Retenue à la source** : taux par défaut appliqué côté service = **15 %** si non précisé (`enregistrerRetenueSource`, `const taux = input.taux ?? 15`), cohérent avec le défaut du formulaire (`taux: 5` en pourcentage entier).
- **IBS (Impôt sur les Bénéfices des Sociétés)** : la liasse fiscale avancée estime l'IBS à **26 %** du résultat fiscal simplifié (`ibsEstime = Math.max(0, resultatFiscal * 0.26)`, `fiscalite-avancee.service.ts:203`) — valeur codée en dur, à considérer comme une estimation simplifiée et non une liquidation officielle.
- **Format de télédéclaration** : export CSV à champs fixes `BASE_HT_VENTES`, `TVA_COLLECTEE`, `TVA_DEDUCTIBLE`, `CREDIT_ANTERIEUR`, `SOLDE_A_PAYER` — présenté comme une préparation du formulaire G50, pas un dépôt électronique direct auprès de la DGI (le dépôt effectif se fait hors application, sur le portail DGI ; l'application ne fait qu'enregistrer la référence DGI obtenue en retour).
- **SIFEC** : schéma de payload `DGI-SIFEC-ALG` version `1.0` (voir `buildSifecPayload`) ; le mode `sandbox` simule une acceptation systématique à des fins de certification/tests, le mode `production` nécessite une intégration API DGI avec identifiants officiels non implémentée dans le code lu.
- Aucun autre taux, barème ou échéance légale (dates limites de dépôt G50, pénalités de retard, etc.) n'est codé dans les fichiers lus — ne pas déduire de délai réglementaire non présent dans le code.

## Interconnexions

- **Facturation** (`facturation.md`) : source du registre TVA ventes (`enregistrerTvaVente`) et des métadonnées fiscales SIFEC (`factures_fiscales_metadata`).
- **Achats & approvisionnements** (`achats-approvisionnements.md`) : source du registre TVA achats, par import manuel (« Importer bons validés ») ou automatique à la réception d'un bon de commande.
- **Comptabilité SCF** (`comptabilite-scf.md`) : fonctionnellement liée (mêmes événements source : factures, achats) mais **sans jointure directe en base** entre les écritures comptables (`ecritures_comptables`) et les registres TVA (`registre_tva_ventes`/`registre_tva_achats`) — les deux modules sont alimentés indépendamment.
- **Dashboard PDG / Cockpit DEC** : non identifié de KPI fiscal dédié dans le code lu au-delà des écrans propres au module.

## Dépannage

- **« Permission refusée pour la fiscalité. »** / **« Permission refusée pour le connecteur SIFEC. »** : le compte connecté n'a ni le rôle `SUPERADMIN` ni `ADMIN_DEC`.
- **Retenue à la source anormalement faible** : vérifier la valeur saisie dans le champ « Taux (%) » avant enregistrement — le champ attend un pourcentage entier (ex. `5` pour 5 %), cohérent avec le calcul `montantRetenu = round(baseHt × taux) / 100`.
- **Déclaration TVA à 0** : vérifier que le registre TVA (ventes et achats) de la période contient bien des lignes avant de cliquer « Calculer déclaration » — le calcul ne recrée pas de données manquantes, il agrège l'existant.
- **Crédit antérieur inattendu** : provient automatiquement de la dernière déclaration calculée d'une période antérieure dont le solde était négatif — vérifier l'historique des déclarations si le montant semble erroné.
- **Transmission SIFEC toujours rejetée en mode production** : normal dans l'état actuel du code — l'intégration API DGI production n'est pas implémentée (`simulateSifecResponse` renvoie systématiquement un échec hors mode `sandbox`) ; utiliser le mode Sandbox pour les tests/certification.
- **Import « bons validés » qui n'importe rien** : les bons déjà importés (présents dans `registre_tva_achats` via `achat_ref_id`) sont ignorés — vérifier que les bons de commande de la période sont bien au statut `valide` et n'ont pas déjà été importés.
