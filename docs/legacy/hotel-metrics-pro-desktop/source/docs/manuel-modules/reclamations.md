# Réclamations clients

## Présentation

Gestion des réclamations clients et suivi qualité : enregistrement d'une réclamation (canal de réception, catégorie, priorité), suivi de son statut jusqu'à résolution, référence séquentielle par hôtel.

Page : `src/pages/reclamations/ReclamationsPage.tsx`. Service : `electron/services/reclamations.service.ts`. Types renderer : `src/shared/types/reclamations.ts`.

Public cible : réceptionniste, chef de département, contrôleur d'exploitation — voir `docs/guides-utilisateurs/09-receptionniste.md`, `docs/guides-utilisateurs/08-chef-departement.md` et `docs/guides-utilisateurs/04-controleur-exploitation.md`.

## Prérequis & accès

- Route : `/reclamations` (« Réclamations » du module « Qualité & relation client »), toujours visible dans `sidebarModules.ts`.
- Aucun filtrage de périmètre hôtel automatique côté serveur observé dans `reclamations.service.ts` (contrairement au module Anomalies) : `listReclamations(hotelId?, statut?)` ne restreint pas par défaut aux hôtels de l'acteur si `hotelId` n'est pas fourni — le filtrage repose sur le choix explicite fait dans l'écran.
- Aucun contrôle de permission dédié observé dans `reclamations.ipc.ts` au-delà de l'authentification standard.

## Écrans & champs

Écran unique :

1. **En-tête** avec bouton « Nouvelle réclamation ».
2. **Filtres** : hôtel (« Tous les hôtels »), statut (Toutes / Ouvertes / En cours / Résolues).
3. **Liste** : `reference` (format `REC-{année}-{séquence sur 4 chiffres}`, généré par hôtel), `objet`, `priorite` (basse/normale/haute/urgente, colorée), `categorie` (chambre/restauration/service/propreté/bruit/facturation/autre), `statut`, `clientNom`, canal (accueil/e-mail/téléphone/web/autre), `dateReception`, `hotelNom`. Bouton « Résoudre » si statut ≠ `resolue`/`fermee`.
4. **Modale de création** : Objet (obligatoire), Nom du client, Email, Canal, Catégorie, Description.

## Workflows standards

1. **Créer une réclamation** : bouton « Nouvelle réclamation » → formulaire → `ipcClient.reclamations.create(payload)` (canal `reclamations:create`). Si `clientNom` est vide, la valeur par défaut « Client » est utilisée côté renderer avant l'appel. Une référence unique est générée automatiquement (`nextReference`, séquence `reclamations_seq` par hôtel).
2. **Résoudre une réclamation** : bouton « Résoudre » → `ipcClient.reclamations.update(id, { statut: 'resolue', dateResolution: <date du jour> })` (canal `reclamations:update`). Le service permet aussi de renseigner `reponse`, `satisfaction`, `assigneA`, `priorite` via ce même canal (non exposés dans le formulaire actuel de l'écran).
3. **Filtrage** : changement d'hôtel ou de statut relance `ipcClient.reclamations.list(hotelId, statut)` (canal `reclamations:list`).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — c'est un outil de gestion de la relation client interne, sans obligation légale algérienne propre.

## Interconnexions

- **Cockpit DEC** (`docs/manuel-modules/dec-cockpit.md`) : le widget « Réclamations » compte les réclamations non `cloturee`/`resolue`.
- **Dashboard PDG** (`docs/manuel-modules/dashboard-pdg.md`) : le KPI `RECLAMATIONS_OUVERTES` utilise le même critère de comptage.
- **Hébergement & occupation** (`docs/manuel-modules/hebergement-occupation.md`) : source probable des réclamations de catégorie « chambre » créées à l'accueil, sans lien technique direct observé dans le code (pas d'appel croisé identifié).

## Dépannage

- **Deux réclamations avec la même référence** : ne devrait pas se produire — la séquence `reclamations_seq` est incrémentée par hôtel (`hotel_id = 0` pour les réclamations sans hôtel) à chaque création ; en cas de doublon, vérifier l'intégrité de la table `reclamations_seq`.
- **Statistiques de satisfaction absentes** : `satisfactionMoy` reste `null` tant qu'aucune réclamation n'a de note de satisfaction renseignée (champ non exposé dans le formulaire de création/résolution actuel de l'écran — à saisir via une évolution du formulaire ou directement en base si nécessaire).
- **Réclamation visible alors qu'elle ne devrait concerner qu'un autre hôtel** : contrairement au module Anomalies, ce module n'applique pas de restriction de périmètre par défaut côté serveur — toujours vérifier le filtre hôtel actif à l'écran.
