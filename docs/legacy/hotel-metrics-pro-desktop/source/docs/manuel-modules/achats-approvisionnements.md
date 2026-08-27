# Achats & approvisionnements

## 1. Présentation

Ce module gère les fournisseurs et les bons de commande (BC) : création, validation (avec workflow d'approbation au-delà d'un seuil), envoi au fournisseur, réception (totale ou partielle). La réception alimente automatiquement le stock et le registre de TVA sur achats.

Composant : `src/pages/achats/AchatsPage.tsx`. Route : `/achats`. Backend : `electron/services/achats.service.ts`.

## 2. Prérequis & accès

- Authentification requise, mot de passe changé. Aucune permission `can...` spécifique ne protège `/achats` dans `AppRoutes.tsx`.
- Module désactivable via **Administration → Modules activés** (`achats-approvisionnements` figure dans `CONFIGURED_MODULE_IDS`).
- La validation d'un bon dont le montant TTC dépasse un seuil paramétrable (`app_settings.workflow_seuil_achat_ttc`, 200 000 DA par défaut) déclenche un workflow d'approbation — voir [`workflows.md`](workflows.md).

## 3. Écrans & champs

Deux onglets (`AchatsPage.tsx`), avec filtre hôtel optionnel (« Tous les hôtels » possible).

**Onglet « Bons »** :
- KPIs : total bons, bons en attente (`brouillon`), montant total TTC.
- Liste des bons de commande (`BonCommande`) : numéro, fournisseur, statut (`brouillon`, `valide`, `envoye`, `recu_partiel`, `recu`, `annule` — badge coloré), date de commande, date de livraison prévue, montant TTC.
- Actions contextuelles selon statut : **Valider** (`brouillon`), **Envoyer** (`valide`), **Réceptionner** (`valide`, `envoye`, `recu_partiel`).
- Modale « Nouveau bon de commande » : fournisseur, lignes (désignation, quantité, prix unitaire HT — TVA par défaut 19 %), notes.
- Modale « Réception » : pour chaque ligne restant à recevoir, quantité reçue (bornée par le reste à recevoir).

**Onglet « Fournisseurs »** :
- Liste (`Fournisseur`) : raison sociale, code, e-mail, téléphone.
- Modale « Nouveau fournisseur » : code, raison sociale, e-mail, téléphone (le service accepte aussi contact, adresse, RC, NIF, NIS mais ces champs ne sont pas exposés dans le formulaire actuel).

Une bannière verte confirme les mouvements de stock générés après réception, avec lien direct vers [`stocks-consommations.md`](stocks-consommations.md) (`/stocks`).

## 4. Workflows standards

**Créer et valider un bon de commande** :
1. « Nouveau bon » → `ipcClient.achats.createBon` → `createBon()` calcule HT/TVA/TTC par ligne et numérote `BC-{année}-{séquence}` (`bons_commande`).
2. « Valider » → `ipcClient.achats.validerBon` → `validerBon()` :
   - Si `montantTtc <= seuil` (200 000 DA par défaut) : passage direct au statut `valide`.
   - Sinon : création (si besoin) d'un workflow d'approbation (`createWorkflow` + `submitWorkflow`, module `achats`, type `bon_commande`) — la validation du bon échoue tant que le workflow n'est pas à l'état `valide`, `valide_dec` ou `cloture`. Voir [`workflows.md`](workflows.md).
3. « Envoyer » (bon `valide` uniquement) → `ipcClient.achats.envoyerBon` → passage au statut `envoye`.

**Réceptionner un bon** (statuts `valide`, `envoye`, `recu_partiel`) :
1. « Réceptionner » → `ipcClient.achats.getBonLignes` charge les lignes non soldées.
2. Confirmer les quantités reçues → `ipcClient.achats.livrerBon` → `livrerBon()`, exécuté en transaction :
   - Met à jour `qte_recue` par ligne.
   - Pour chaque ligne liée à un produit stock, crée un mouvement d'entrée (`createMouvement`, type `entree`) dans [`stocks-consommations.md`](stocks-consommations.md).
   - Passe le bon à `recu` (toutes lignes soldées) ou `recu_partiel`.
   - Enregistre une ligne de TVA déductible dans le registre TVA achats (`registerTvaAchatFromBonLivraison`, module fiscalité) si un montant HT a été reçu.

## 5. Règles métier DZ

- Chaque ligne d'un bon de commande porte un taux de TVA (19 % par défaut, modifiable ligne par ligne dans le formulaire de création).
- Toute réception avec montant HT positif alimente automatiquement le **registre de TVA sur achats** (`registre_tva_achats`, périodicité mensuelle déduite de la date d'opération) — base pour la déclaration TVA DGI, voir [`fiscalite-dgi.md`](fiscalite-dgi.md).
- Un seuil de validation workflow (`app_settings.workflow_seuil_achat_ttc`, 200 000 DA par défaut) impose une approbation supplémentaire pour les bons de commande dépassant ce montant — contrôle interne sur les achats significatifs.

## 6. Interconnexions

- **Stocks & consommations** ([`stocks-consommations.md`](stocks-consommations.md)) : chaque réception génère des mouvements d'entrée en stock, qui déclenchent eux-mêmes une écriture comptable SCF (journal `AC`).
- **Fiscalité DGI** ([`fiscalite-dgi.md`](fiscalite-dgi.md)) : alimentation du registre de TVA sur achats à chaque réception.
- **Workflows** ([`workflows.md`](workflows.md)) : approbation obligatoire au-delà du seuil configuré pour valider un bon de commande.
- **Budget & prévisions** et **Audit & contrôle interne** : référencés dans `src/modules/moduleCatalog.ts` (`connectedTo`) comme parties prenantes, sans flux de code automatique identifié au-delà de la traçabilité `writeAuditLog` sur chaque action (création, validation, envoi, réception).

## 7. Dépannage

- **« Ce bon d'achat nécessite une approbation workflow (montant > seuil) »** : le bon dépasse le seuil configuré — suivre son approbation dans [`workflows.md`](workflows.md) avant de pouvoir le valider à nouveau.
- **« Seul un bon brouillon peut être validé » / « Seul un bon validé peut être envoyé au fournisseur »** : respecter la séquence de statuts `brouillon → valide → envoye → recu_partiel/recu` ; les actions sont conditionnées côté UI par le statut courant.
- **« Réception impossible pour ce statut de bon »** : un bon `brouillon` ou `annule` ne peut pas être réceptionné — le valider (et l'envoyer) au préalable.
- **« Aucune quantité à recevoir »** : toutes les lignes du bon sont déjà soldées (`qteRecue >= quantite`).
- **Mouvements de stock non visibles après réception** : vérifier que les lignes du bon étaient bien associées à un `produitId` du référentiel — les lignes en texte libre sans produit rattaché ne génèrent pas de mouvement de stock.
