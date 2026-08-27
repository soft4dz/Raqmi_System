# PortMaster — accueil, référentiel, bateaux, emplacements

## 1. Présentation

**PortMaster** est le sous-module ERP dédié à la gestion du port de plaisance rattaché à l'hôtel : référentiel physique du port (bassins, quais, emplacements d'amarrage), flotte des bateaux enregistrés, mouvements des navires (arrivées, départs, changements de poste) et tableau de bord de pilotage de l'activité portuaire.

Cette fiche couvre le **hub applicatif** et les écrans **référentiel / flotte / emplacements / mouvements**. La partie commerciale (contrats, clients, factures, tarifs, validations, recouvrement) est documentée dans [`portmaster-facturation.md`](portmaster-facturation.md).

Route d'entrée : `/portmaster`. Composant racine du hub : `src/pages/portmaster/PortMasterHubPage.tsx`, qui affiche un launcher d'applications (`OdooAppLauncher`, `src/components/apps/OdooAppLauncher.tsx`) piloté par la configuration `src/pages/portmaster/portmasterApps.config.ts` (liste des 11 applications PortMaster réparties en 5 groupes : Pilotage, Référentiel, Clients & flotte, Facturation, Opérations).

Ce module s'adresse au **responsable PortMaster**, voir le guide [`05-responsable-portmaster.md`](../guides-utilisateurs/05-responsable-portmaster.md) pour les tâches quotidiennes et procédures orientées métier. Cette fiche technique en détaille les écrans, champs et appels IPC réels.

## 2. Prérequis & accès

- Toutes les routes `/portmaster/*` sont protégées par le composant `RequirePortmaster` (`src/routes/RequirePortmaster.tsx`), qui redirige vers `/dashboard` si `canAccessPortmaster(role)` est faux.
- `canAccessPortmaster` (`src/shared/permissions.ts`) renvoie `hasPermission(roleCode, PERMISSIONS.PORTMASTER_FULL)` — permission `portmaster.full`.
- Seul le rôle **`RESPONSABLE_PORT`** porte cette permission dans `ROLE_PERMISSIONS` (`src/shared/permissions.ts`) ; les rôles `SUPERADMIN` et `ADMIN_DEC` y ont accès via `isAdminRole()` (accès total implicite).
- Côté serveur, chaque service PortMaster (`electron/services/portmaster*.service.ts`) revérifie l'accès via une fonction locale `assertPortmaster(actorUserId)` qui appelle `userHasPermission(actorUserId, 'portmaster.full')` ou `isGlobalAdminRole(actor.roleCode)`, sinon lève une erreur via `assertPermission`. La vérification n'est donc pas que côté interface.
- `canExportReports()` inclut aussi `canAccessPortmaster(roleCode)` : un utilisateur PortMaster peut donc accéder aux exports de rapports transverses.
- Aucune dépendance de paramétrage hôtel : contrairement aux autres modules exploitation, les requêtes PortMaster ne filtrent pas par `hotelId` (le port est traité comme une entité unique, pas multi-unités).

## 3. Écrans & champs

### 3.1 Hub PortMaster (`/portmaster`, `PortMasterHubPage.tsx`)
Launcher de type « Odoo apps » : grille de vignettes cliquables, une par application (Tableau de bord, Référentiel port, Emplacements, Clients port, Bateaux, Contrats, Factures, Tarifs port, Recouvrement, Mouvements, Validations), avec recherche texte (`searchPlaceholder`) et regroupement par catégorie. Composant purement navigationnel, aucun appel IPC propre.

### 3.2 Tableau de bord (`/portmaster/dashboard`, `PortDashboardPage.tsx`)
- Chargement des données via le hook `usePortDashboard` (`src/hooks/usePortDashboard.ts`) qui appelle `ipcClient.portmaster.dashboard(filters)` → IPC `portmaster:dashboard` → `portService.getPortDashboard`.
- Barre de filtres période (`DashboardFiltersBar`, sans filtre hôtel ni rubrique) : année, mois optionnel.
- **KPI** (`PortDashboardKpis`, `src/components/port/dashboard/PortDashboardKpis.tsx`) : 4 tuiles — CA Facturé, Encaissements, Créances (montants), Occupation (`emplacementsOccupes / emplacementsTotal` + taux d'occupation en %).
- **Graphiques** (`PortDashboardCharts`) : alimentés par le `PortDashboardDto` — répartition des bateaux par type de navire, courbe CA facturé vs encaissements sur 12 mois (`revenueChart`).
- Champs additionnels du `PortDashboardDto` non repris en tuile mais utilisés ailleurs sur l'écran ou en alerte : emplacements libres/réservés, contrats actifs, contrats arrivant à échéance sous 30 jours, bateaux présents, bateaux en situation irrégulière, validations en attente, reste à recouvrer, encaissements du mois, variation CA/encaissements (`variationCaPct`/`variationEncaissementsPct`, actuellement toujours `null` — calcul non implémenté côté service, commentaire explicite « À implémenter » dans `portmaster.service.ts`).
- **Plan d'amarrage visuel** (`VisualMooringPlan`, `src/components/port/dashboard/VisualMooringPlan.tsx`) : emplacements regroupés par zone (ou par préfixe du code, ex. `A-01` → zone « A », si `zone` n'est pas renseignée), une pastille colorée par emplacement (vert = libre/disponible, bleu = occupé, orange = réservé, rouge = bloqué, gris = maintenance), légende des 4 premiers statuts, infobulle au survol (code, statut, longueur max).
- **Panneau Alertes & Événements** : liste des 5 premières alertes issues de `listAlertes` (catégorie, sévérité `info`/`warning`/`danger`, message), triées par sévérité décroissante.
- Boutons « Actualiser » et « Exporter PDF » (le bouton PDF est présent dans l'écran ; l'action d'export dashboard existe côté `ipcClient.export.dashboardPdf`).

### 3.3 Référentiel portuaire (`/portmaster/referentiel`, `ReferentielPortPage.tsx`)
- **Recherche globale** (bassin, quai, emplacement, bateau, client) → `ipcClient.portmaster.searchReferentiel(query)` → IPC `portmaster:referentiel:search`. Les résultats bateaux/clients sont des liens directs vers leurs fiches (`/portmaster/bateaux/:id`, `/portmaster/clients/:id`).
- **Filtres** : Bassin (select, `listBassins`), Quai (select dépendant du bassin, `listQuais`), champ texte « Code, bateau… ».
- **Grille de cartes emplacements** (`listEmplacementsDetail`) : code, badge de statut (`EmplacementStatutBadge`), bassin/quai, libellé, longueur max (m), bateau amarré et nom du client le cas échéant.
- Types de référentiel (`src/shared/types/portmaster.ts`) : `BassinItem` (code, libellé, nb quais, nb emplacements), `QuaiItem` (code, bassin, nb emplacements, nb occupés), `EmplacementDetailItem` (code, libellé, quai, bassin, longueur/largeur/profondeur max, type d'emplacement, statut, bateau, client, n° contrat).

### 3.4 Bateaux (`/portmaster/bateaux`, `BateauxPage.tsx`)
- Liste (`DataTable`) : Nom, Immatriculation, Type, Propriétaire, Emplacement (code), Statut (badge `actif`/autre), actions.
- Recherche texte avec debounce 300 ms (`ipcClient.portmaster.listBateaux(search)`), filtre sur nom/immatriculation/propriétaire côté service.
- Action **modifier** (icône crayon) → `/portmaster/bateaux/:id`.
- Action **désactiver** (icône `UserX`, uniquement si `statut === 'actif'` et pas de contrat actif) → confirmation `window.confirm` puis `ipcClient.portmaster.deactivateBateau(id)`.
- Bouton « Nouveau bateau » → `/portmaster/bateaux/new`.

### 3.5 Fiche bateau (`/portmaster/bateaux/new`, `/portmaster/bateaux/:id`, `BateauFormPage.tsx`)
Formulaire simple (pas d'onglets) : Nom du navire\* , Immatriculation, Type, Propriétaire\* (texte libre — pas de lien vers le référentiel Clients), E-mail contact, Téléphone contact, Longueur (m), Largeur (m), Statut (`actif` / `inactif`), Notes.
- Création : `ipcClient.portmaster.createBateau(input)` → IPC `portmaster:bateaux:create` → `SaveBateauInput` obligatoire `nom` + `proprietaire`.
- Modification : `ipcClient.portmaster.updateBateau(id, input)` → IPC `portmaster:bateaux:update`.
- Redirection vers la liste après enregistrement.
- Remarque importante : le champ **Propriétaire** est une chaîne texte libre stockée sur `port_bateaux.proprietaire` — il n'y a **pas de clé étrangère vers `port_clients`** au niveau du bateau lui-même (le lien client se fait via le contrat/la facture). À ne pas confondre avec le champ « client » affiché sur les cartes du référentiel, qui provient du contrat actif.

### 3.6 Emplacements (`/portmaster/emplacements`, `EmplacementsPage.tsx`)
- Vue en lecture seule, regroupée **par zone** (`e.zone`, ou « Autre » si non renseignée) via `ipcClient.portmaster.listEmplacements()`.
- Chaque carte : code, badge de statut, libellé, longueur max, et si occupé : nom du bateau + numéro de contrat.
- Aucune action de création/modification sur cet écran (le référentiel bassins/quais/emplacements n'a pas d'écran de gestion CRUD identifié dans le code lu — seule la consultation est exposée côté PortMaster ; la modification des statuts se fait indirectement via les contrats et mouvements).

### 3.7 Mouvements (`/portmaster/mouvements`, `MouvementsPage.tsx`)
- Table des mouvements (100 derniers, `listMouvements`) : Date, Bateau, Type (libellés `TYPE_LABELS` : Arrivée / Départ / Changement d'emplacement), emplacement De, emplacement Vers, Motif.
- Formulaire « Nouveau mouvement » (repliable) : Bateau\* (options actives, `bateauxOptions`), Type\* (`arrivee`/`depart`/`changement_emplacement`), Emplacement départ (affiché pour départ/changement), Emplacement destination (affiché pour arrivée/changement), Date\*, Motif.
- Les listes d'emplacements proposées dans le formulaire proviennent de `listEmplacementsLibres()` (emplacements au statut `libre`/`disponible` uniquement).

## 4. Workflows standards

### 4.1 Enregistrer un nouveau bateau
1. `Bateaux` → « Nouveau bateau ».
2. Renseigner nom et propriétaire (obligatoires), caractéristiques techniques (longueur/largeur), contacts.
3. Enregistrer → `portmaster:bateaux:create`. Le bateau est créé avec le statut par défaut `actif`.
4. Un bateau ne peut être rattaché à un emplacement que via la création d'un **contrat** ou d'un **mouvement** (voir [`portmaster-facturation.md`](portmaster-facturation.md) et §4.3 ci-dessous).

### 4.2 Désactiver un bateau
1. `Bateaux` → icône « désactiver » sur la ligne concernée (visible uniquement si aucun contrat actif n'est rattaché).
2. Confirmation → `portmaster:bateaux:deactivate` → le service `deactivateBateau` bloque l'opération avec l'erreur « Impossible : contrat actif sur ce bateau. » si un contrat actif existe encore, sinon passe `statut = 'inactif'` et renseigne `deleted_at` (suppression logique).

### 4.3 Enregistrer un mouvement de bateau
1. `Mouvements` → « Nouveau mouvement ».
2. Sélectionner le bateau et le type de mouvement.
3. Selon le type, renseigner l'emplacement de départ et/ou de destination (validations côté service : arrivée exige un emplacement destination, départ exige un emplacement source, changement exige les deux — sinon erreur explicite).
4. Enregistrer → IPC `portmaster:mouvements:create`. Effets de bord automatiques dans `portmaster-mouvements.service.ts` :
   - si un **contrat actif** existe pour ce bateau, son `emplacement_id` est mis à jour pour suivre le nouvel emplacement (arrivée ou changement) ;
   - le statut des emplacements concernés (ancien et nouveau) est resynchronisé (`occupe` si un contrat actif y est rattaché, sinon `disponible`) ;
   - le mouvement est mis en file de synchronisation multi-postes (`enqueueSync('port_mouvement', ...)`), échec silencieux si `sync_config` n'existe pas encore.

### 4.4 Consulter le référentiel et localiser un bateau/client
1. `Référentiel port` → utiliser la recherche globale (bassin, quai, emplacement, bateau ou client) ou les filtres Bassin/Quai/texte.
2. Cliquer sur un résultat bateau ou client pour ouvrir sa fiche directement.

### 4.5 Suivre les alertes opérationnelles depuis le dashboard
1. `Tableau de bord` → panneau « Alertes & Événements ».
2. Catégories générées par `portmaster-alertes.service.ts` : contrats arrivant à échéance sous 30 jours ou déjà expirés (`danger`/`warning`), contrats en attente de validation, documents expirés (bateau/client), bateaux en « situation irrégulière », bateaux actifs sans contrat actif, factures impayées, dossiers clients incomplets.
3. Chaque alerte pointe, quand disponible, vers un lien direct (`link`) vers la fiche concernée.

## 5. Règles métier DZ

Aucune règle légale ou fiscale algérienne spécifique (taxe portuaire, redevance domaniale, etc.) n'a été trouvée dans le code de ce sous-ensemble (référentiel, bateaux, emplacements, mouvements) — ces écrans gèrent uniquement le référentiel physique et opérationnel du port. Les règles de TVA applicables à la facturation portuaire (taux configurable, défaut 19 %) sont documentées dans [`portmaster-facturation.md`](portmaster-facturation.md).

## 6. Interconnexions

- **PortMaster — contrats, clients, facturation** ([`portmaster-facturation.md`](portmaster-facturation.md)) : un contrat actif détermine l'occupation d'un emplacement (`syncEmplacementStatut`) et le statut « occupé/libre » affiché dans le référentiel, les emplacements et le dashboard ; la génération de facture depuis un contrat utilise la longueur du bateau et un tarif (module Tarifs) pour calculer le montant.
- **Créances & recouvrement** ([`creances-recouvrement.md`](creances-recouvrement.md)) : le « reste à recouvrer » agrégé au niveau des contrats PortMaster alimente le KPI `resteARecouvrer` et `creancesClients` du dashboard PortMaster ; le recouvrement portuaire détaillé (relances) est un écran propre décrit dans [`portmaster-facturation.md`](portmaster-facturation.md), distinct du module Créances généraliste de l'hôtel.
- **Encaissements & trésorerie** ([`encaissements-tresorerie.md`](encaissements-tresorerie.md)) : les encaissements portuaires (`port_encaissements`) sont gérés dans leur propre table, séparée de la trésorerie hôtelière générale — pas d'écriture croisée automatique identifiée dans le code lu.
- **Rapports & exports** ([`rapports-exports.md`](rapports-exports.md)) : `canAccessPortmaster` donne également accès à `canExportReports()` ; le dashboard propose un export PDF dédié (`ipcClient.export.dashboardPdf`).
- **Synchronisation multi-postes** ([`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)) : chaque mouvement de bateau est mis en file via `enqueueSync('port_mouvement', ...)`.
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : chaque création/modification/désactivation de bateau et chaque mouvement génère une entrée `writeAuditLog` (module `portmaster`).

## 7. Dépannage

- **Accès refusé / redirection vers `/dashboard`** : le rôle connecté n'a pas la permission `portmaster.full` (seul `RESPONSABLE_PORT` l'a nativement, plus les rôles admin globaux). Vérifier l'affectation de rôle dans `/admin/users`.
- **« Impossible : contrat actif sur ce bateau. »** à la désactivation : résilier ou clôturer le contrat actif du bateau avant de le désactiver.
- **« Emplacement d'arrivée obligatoire. » / « Emplacement de départ obligatoire. » / « Emplacements source et destination obligatoires. »** : champs manquants dans le formulaire de mouvement selon le type sélectionné.
- **Un emplacement reste « occupé » après un départ** : vérifier qu'un mouvement de type « Départ » a bien été enregistré ou que le contrat correspondant a été résilié — le statut de l'emplacement n'est recalculé qu'au moment d'un mouvement ou d'un enregistrement de contrat (`syncEmplacementStatut`), pas en tâche de fond automatique.
- **Le référentiel n'affiche aucun écran de création de bassin/quai** : conforme au code lu — ces écrans exposent uniquement la consultation ; la création de bassins/quais/emplacements se fait en base ou via un outil hors périmètre des pages React examinées.
- **Champ « Propriétaire » du bateau différent du client facturé** : normal, ce champ est une saisie libre non liée au référentiel Clients ; le client réellement facturé est celui rattaché au contrat ou renseigné à la création de la facture (voir [`portmaster-facturation.md`](portmaster-facturation.md)).
