# Concept 1 — « Poste de travail »

> Exploration indépendante de refonte de l'accueil du client WPF. Angle imposé : **orienté action**.
> État de référence : branche `reorg/phase-1`, HEAD `e7dcaad`. Rien d'autre que ce dossier n'est modifié.
> Maquette : [`maquette.html`](./maquette.html) (autonome, sept profils, deux thèmes, états visibles).

## 1. Intention

L'accueil actuel répond à « où en est le produit ? » (catalogue de 50 cartes). Ce concept fait
répondre l'accueil à la seule question qu'un réceptionniste, un directeur d'unité ou un caissier se
pose en ouvrant l'application : **« qu'est-ce que je dois faire maintenant ? »**

Le parti pris est entier :

1. **L'accueil est une liste de files de travail**, pas un tableau de bord. Chaque carte est un
   *compte d'objets qui attendent un geste* (« 2 arrivées en retard », « 4 ordres de paiement à
   approuver »), jamais un indicateur de performance. Le taux d'occupation, l'ADR, le RevPAR n'y ont
   pas leur place : ils sont dans 20 Pilotage.
2. **Regroupé par urgence, en trois bandes** : *En retard* (ce que le serveur qualifie déjà de
   dépassé), *Aujourd'hui* (la journée métier), *À surveiller* (signaux faibles). Les bandes sont
   des classes d'états **renvoyés par le serveur** (`OverdueArrivals`, `IsLate`, `IsOverdue`,
   `ClosingBacklog`, `Over90`, `status=Draft`…), jamais des seuils calculés dans le WPF.
3. **Chaque carte ouvre l'écran qui agit**, par le chemin unique `NavigateToModule`. Le bouton de la
   carte porte le verbe (« Traiter les arrivées », « Approuver ») quand le profil détient la
   permission d'action ; il devient « Voir » et la carte passe en mode *Suivi* quand il ne l'a pas.
4. **Composé à partir des permissions du jeton**, jamais d'un rôle : la même fonction pure produit
   l'accueil de la réception et celui de la direction. Une permission absente = une carte absente,
   pas une carte grisée qui promet.
5. **Honnête par construction** : une carte n'existe que si un endpoint la nourrit aujourd'hui.
   Notifications, messagerie, tâches transverses, agenda, favoris n'apparaissent nulle part sur
   l'accueil — ils restent des nœuds `Planifié` de l'arbre 01, visibles dans le catalogue, pas ici.

Ce que ce concept **n'est pas** : ni un portail « Mon Espace » au sens de `navigation-shell.md`
§ 5.2 (tuiles par sous-module de 01), ni un cockpit de pilotage. Il assume que l'accueil est un
*établi* : ce qui compte, c'est ce qu'on y pose le matin et ce qu'on en retire le soir.

## 2. Structure

### 2.1 Wireframe (profil Directeur d'unité, 1240 × 760, sans défilement pour les deux premières bandes)

```text
┌ Onglet 0 « Accueil » — barre latérale repliée, fil d'Ariane masqué (SyncSidebarToTab) ───────────────┐
│ (Poste de travail) (Catalogue des modules)                                       ← sélecteur de section │
│                                                                                                          │
│ Bonjour, Samir Merzouk                       Unité [ALG-CEN · Hôtel Riadh Alger Centre ▾]  ⟳ Actualiser │
│ mardi 1 septembre 2026 · date métier 31/08/2026 · 1 journée à clôturer       actualisé à 08:12 (F5)     │
│ 3 en retard · 8 à faire aujourd'hui · 3 à surveiller          [Mon profil] [Mes préférences] [Ma sécurité]│
│                                                                                                          │
│ ● EN RETARD · 3                                                                                          │
│ ┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐ ┌───────────────────┐                  │
│ │ ALG-CEN           │ │ ALG-CEN           │ │ ALG-CEN           │ │ Groupe      Suivi │                  │
│ │ 2                 │ │ 1                 │ │ 1                 │ │ 2                 │                  │
│ │ Arrivées en retard│ │ Départ en retard  │ │ Journée à clôturer│ │ Journées non      │                  │
│ │ candidates no-show│ │ solde folio ouvert│ │ 31/08 · J-1       │ │ clôturées (groupe)│                  │
│ │ [Traiter →]       │ │ [Traiter →]       │ │ [Clôturer →]      │ │ [Voir le cockpit] │                  │
│ └───────────────────┘ └───────────────────┘ └───────────────────┘ └───────────────────┘                  │
│                                                                                                          │
│ ● AUJOURD'HUI · 8                                                                                        │
│ │ 14 Arrivées │ 9 Départs │ 6 Chambres à préparer │ 3 Tâches à inspecter │ 2 Validations en attente │    │
│ │ 3 Commandes à réceptionner │ 2 Événements du jour │ 1 Unité sans saisie de recette (Groupe) │         │
│                                                                                                          │
│ ● À SURVEILLER · 3                                                                                       │
│ │ 3 Recettes en attente de validation · Suivi │ 5 Articles sous le minimum │ 1 Chambre hors service │   │
│                                                                                                          │
│ DERNIERS ÉCRANS OUVERTS (ce poste)                                                                       │
│ (Arrivées · PMS front office) (Clôture journalière) (Réservations · Hébergement)                        │
│                                                                                                          │
│ ┌ Où en est le produit ? ─────────────────────────────────────────────────────────────────────────────┐ │
│ │ 31 modules disponibles sur 50 · 11 domaines fonctionnels sur 22 · 0 prêt pour la production           │ │
│ │ [Ouvrir le catalogue des modules →]                                                                    │ │
│ └────────────────────────────────────────────────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Hauteur mesurée sur la maquette à 1240 × 760 (en-tête 76, marge 24, sélecteur 34, bandeau ≈ 175,
en-tête de bande 22, carte ≈ 190 avec légende sur deux lignes, espacements 20) : la bande « En
retard » se termine vers 580 px et la première rangée d'« Aujourd'hui » commence au-dessus du pli
(≈ 100 px visibles avant le bandeau de session). **Le bandeau et la bande urgente sont entiers sans
défiler**, là où l'accueil actuel n'affiche aucune carte de module sans défilement à cette taille ;
à 1080 px de haut, les trois bandes tiennent. La densité Compact (carte 88 px de minimum, padding
12/10, espacements 14) gagne ≈ 50 px sur les deux premières bandes, pas davantage : elle retire de
l'air, jamais de texte.

### 2.2 Sections

#### A. Sélecteur de section — « Poste de travail » / « Catalogue des modules »

- **Contenu** : deux boutons radio (style `FilterChip`, `GroupName=HomeSection`) en tête de
  l'onglet 0. Le catalogue existant (blocs 2-4 de l'accueil actuel : puces de domaine, bandeau
  d'avancement, recherche, puces de statut/priorité, 50 `ModuleCatalogCard`, état vide) est
  **déplacé tel quel** dans la seconde section. Rien n'est supprimé.
- **Source / permission** : aucune (statique).
- **Action** : `Ctrl+K` bascule sur « Catalogue » et donne le focus à `HomeSearchTextBox` (le
  raccourci garde sa cible). `Alt+Origine` revient sur « Poste de travail ».
- **Pourquoi pas un 32ᵉ onglet** : le garde `check-module-readiness.ps1` lit toutes les balises
  `<TabItem>` de `MainWindow.xaml` dans l'ordre ; une section dans l'onglet 0 (contenu d'un
  `UserControl` de `Views/`) ne le touche pas.

#### B. Bandeau

- **Contenu** : salutation `HomeGreetingText` (« Bonjour, {DisplayName} », valeur de
  `LoginResponse.User`), date du jour `HomeDateText` (locale, `fr-FR` comme aujourd'hui), **date
  métier de l'unité** (`GET /lodging/business-date` → `BusinessDate`, `IsLate`, `PendingDays` ;
  affichée seulement si `lodging.read` et une unité), ligne de synthèse « n en retard · n à faire
  aujourd'hui · n à surveiller » (`LiveSetting=Polite`, annoncée après chaque actualisation),
  « actualisé à HH:mm » (heure locale du poste), trois boutons fantômes **Mon profil** (→ onglet 9,
  Paramétrage global › Santé du système, qui rend déjà `/api/v1/me`), **Mes préférences** (→ onglet
  9 › Poste de travail : thème, densité, URL API, unité du poste), **Ma sécurité** (→
  `ShowChangePasswordDialog` existant). Ce sont les trois seuls sous-modules de 01 partiellement
  couverts (`03-cartographie-cible.md` § 01) ; ils ouvrent des écrans qui existent, sans compteur.
- **Sélecteur d'unité** : `ComboBox` alimentée par `GET /organization/hotel-units` quand le jeton
  porte `units.read` ; sinon texte « Unité du poste : {code} » lu dans `DesktopSettings`. L'unité est
  un **réglage par poste** (`DesktopSettings.StationUnitCode`, même schéma que `Apparence`/`Densite`),
  cohérent avec la décision 4 du README (aucune affectation utilisateur ↔ unité côté serveur) et
  avec le battement de poste qui envoie déjà une unité. Un poste de réception appartient à une
  unité : c'est exactement la logique « poste de travail ».
- **Bouton Actualiser** : `x:Name="RefreshWorkQueuesButton"` → `F5` le trouve par convention
  (`ShortcutRouter`, charte § 3.13) sans rien déclarer.
- **État vide** : sans unité et avec des files unitaires composées, un encart `WarningBanner`
  sous le bandeau : « Ce poste n'est rattaché à aucune unité » / « Arrivées, départs, chambres et
  événements s'afficheront après le choix dans Paramétrage global › Poste de travail. »

#### C. Bande « En retard »

- **Contenu** : cartes dont l'état est *déjà* qualifié de dépassé par le serveur. Compteur
  `HomeStatValueText` 27, libellé 13,5 SemiBold, légende 11,5 (`CaptionText`) portant un champ
  serveur (« la plus ancienne : 30/08 · J-2 » = `OldestClosingDelay`), étiquette de périmètre
  (code d'unité, « Groupe », « Ma décision »), bouton d'action.
- **Règle des zéros** : une carte à 0 n'est pas affichée ; si toutes sont à 0, la bande montre
  l'état vide « Rien en retard » / « Les arrivées, départs, clôtures et sauvegardes en retard
  apparaîtront ici. » (pictogramme au trait, `EmptyStateTitleText`, `EmptyStateHintText`,
  `IsHitTestVisible=False`).
- **Files** : voir le registre § 3 (bande *Overdue*).

#### D. Bande « Aujourd'hui »

- **Contenu** : la journée métier — ce qui est attendu ou en attente d'un geste. Même gabarit.
- **Règle des zéros** : une carte à 0 **reste** visible, atténuée (`SurfaceSubtleBrush`, texte
  `TextSecondaryBrush` à pleine lisibilité) : « 0 arrivée aujourd'hui » est une information. Bande
  jamais vide dès qu'une file est composée ; sans aucune file (profil sans donnée d'exploitation),
  état vide « Rien à traiter » / « Vos validations et vos files de travail apparaîtront ici dès
  qu'un droit vous les ouvrira. » (`navigation-shell.md` § 7, formulé sans promettre les tâches et
  notifications inexistantes).

#### E. Bande « À surveiller »

- **Contenu** : signaux faibles (stock sous le minimum, chambres hors service, postes, dernière
  sauvegarde à l'heure) **et** les files que le profil peut lire sans pouvoir agir (mode *Suivi* :
  bouton « Voir », étiquette `Suivi` en pastille `StatusDraft`, fond `SurfaceSubtleBrush`).
- **Règle des zéros** : comme « En retard » (0 masqué, état vide « Rien à surveiller »).

#### F. Derniers écrans ouverts (ce poste)

- **Contenu** : six puces `FilterChip` au plus, libellé = nom d'écran de l'arbre + module
  (`FunctionalArchitectureCatalog.TryGetPrimaryPath`), icône du domaine. Alimentées par
  `NavigateToModule` et **persistées par poste** (`DesktopSettings.RecentTabs`, liste d'index
  d'onglets) — un confort de poste, pas des favoris par compte (qui n'existent pas côté serveur).
- **Permission** : une puce dont l'onglet est verrouillé pour le profil courant est masquée, comme
  dans la barre latérale (décision 7 du README).
- **État vide** : la ligne entière est masquée (pas de « Aucun écran récent »).

#### G. « Où en est le produit ? »

- **Contenu** : une ligne de chiffres **statiques du client** (`ModuleCatalog.ExpectedAvailable`
  / `ExpectedTotal`, compteurs de maturité de `FunctionalArchitectureCatalog.Domains`) — ce sont des
  faits du binaire, pas des données serveur, donc admissibles hors § 3.10 ; un bouton
  `SecondaryButton` « Ouvrir le catalogue des modules » qui bascule la section A. Les 50 cartes
  restent atteignables en un clic, avec leurs filtres, leur recherche et leur état vide.

## 3. Registre des files de travail

Chaque ligne est une donnée qui **existe** aujourd'hui, avec la méthode cliente déjà présente
dans `RaqmiApiClient`. Aucune route ni méthode cliente nouvelle n'est requise pour la v1.

| Id | Carte | Bande | Périmètre | Lecture (compose) | Action (mode *À faire*) | Source → champ | Écran cible (onglet · permission) |
|---|---|---|---|---|---|---|---|
| `arrivals-late` | Arrivées en retard | En retard | Unité | `lodging.read` | `lodging.checkin` | `GET /lodging/front-desk` → `OverdueArrivals.Count` | 30 PMS front office › Arrivées · `lodging.read` |
| `departures-late` | Départs en retard | En retard | Unité | `lodging.read` | `lodging.checkout` | front-desk → `OverdueDepartures.Count` | 30 › Départs |
| `closing-unit` | Journée(s) à clôturer | En retard si `IsLate`, sinon absente | Unité | `lodging.read` | `closing.close` | `GET /lodging/business-date` → `PendingDays`, `LastClosedDate` | 5 Clôture journalière · `closing.read` |
| `arrivals` | Arrivées du jour | Aujourd'hui | Unité | `lodging.read` | `lodging.checkin` | front-desk → `Arrivals.Count` | 30 › Arrivées |
| `departures` | Départs du jour | Aujourd'hui | Unité | `lodging.read` | `lodging.checkout` | front-desk → `Departures.Count` | 30 › Départs |
| `hk-dirty` | Chambres à préparer | Aujourd'hui | Unité | `housekeeping.read` | `housekeeping.write` | `GET /housekeeping/board` → `DirtyRooms` | 21 Housekeeping · `housekeeping.read` |
| `hk-inspect` | Tâches à inspecter | Aujourd'hui | Unité | `housekeeping.read` | `housekeeping.inspect` | board → `AwaitingInspectionTasks` | 21 |
| `hk-ooo` | Chambres hors service | À surveiller | Unité | `housekeeping.read` | — | board → `OutOfOrderRooms` | 21 |
| `approvals` | Validations en attente de ma décision | Aujourd'hui | Ma décision | `approvals.decide` | `approvals.decide` | `GET /approvals/instances/pending` → `Count` (filtré par rôle côté serveur) | 16 Workflows & validations · `approvals.read` |
| `dec-revenue` | Recettes à valider | Aujourd'hui | Groupe | `dashboard.read` | `revenue.validate` | `GET /pilotage/dec-cockpit` → `PendingValidationCount`, `PendingValidationAmount` | 2 Recettes journalières · `revenue.read` |
| `dec-backlog` | Journées non clôturées | En retard | Groupe | `dashboard.read` | `closing.close` | dec-cockpit → `ClosingBacklogDayCount`, `OldestClosingDelay` | 20 Cockpit DEC · `dashboard.read` |
| `dec-rejected` | Recettes rejetées à corriger | En retard | Groupe | `dashboard.read` | `revenue.write` | dec-cockpit → `RejectedCount` | 2 |
| `dec-payment-orders` | Ordres de paiement à approuver | Aujourd'hui | Groupe | `dashboard.read` | `treasury.approve` | dec-cockpit → `PendingPaymentOrderCount`, `PendingPaymentOrderAmount` | 6 Trésorerie · `treasury.read` |
| `revenue-missing` | Unités sans saisie de recette | Aujourd'hui | Groupe | `dashboard.read` | `revenue.write` | `GET /revenue/daily/dashboard` → `UnitsMissing` / `TotalUnits` | 3 Tableau de bord · `dashboard.read` |
| `po-pay` | Ordres de paiement à régler | Aujourd'hui | Groupe | `treasury.read` | `treasury.write` | `GET /treasury/payment-orders?status=Approved` → nombre de lignes | 6 |
| `receipts-draft` | Encaissements en brouillon | Aujourd'hui | Unité si connue, sinon groupe | `treasury.read` | `treasury.write` | `GET /treasury/receipts/summary?from=to=aujourd'hui` → `DraftCount` | 6 |
| `receipts-today` | Encaissé aujourd'hui | Aujourd'hui (information) | idem | `treasury.read` | — | même route avec `status=Confirmed` → `GrandTotal`, `ConfirmedCount` | 6 |
| `aging-90` | Créances à plus de 90 jours | En retard | Groupe | `receivables.read` | — | `GET /receivables/aging` → `Total.Over90` (montant) ; `Total.Total` en légende | 13 Créances · `receivables.read` |
| `low-stock` | Articles sous le minimum | À surveiller | Groupe | `inventory.read` | — | `GET /inventory/low-stock` → nombre de lignes | 24 Stocks · `inventory.read` |
| `counts-draft` | Inventaires à valider | Aujourd'hui | Groupe | `inventory.read` | `inventory.validate` | `GET /inventory/counts?status=Draft` → nombre | 24 › Inventaires |
| `po-approve` | Commandes d'achat à approuver | Aujourd'hui | Groupe | `purchasing.read` | `purchasing.approve` | `GET /purchasing/orders?status=Draft` → nombre | 25 Achats · `purchasing.read` |
| `po-receive` | Commandes à réceptionner | Aujourd'hui | Groupe | `purchasing.read` | `purchasing.receive` | `GET /purchasing/orders?status=Approved` → nombre | 25 › Réception |
| `haccp` | Relevés HACCP non conformes (jour) | Aujourd'hui | Groupe | `kitchen.read` | `kitchen.write` | `GET /kitchen/readings?nonCompliantOnly=true&from=to=aujourd'hui` → nombre | 26 Cuisine › Hygiène · `kitchen.read` |
| `absences` | Absences à approuver | Aujourd'hui | Groupe | `hr.read` | `hr.write` | `GET /hr/absences?status=Requested` → nombre | 22 RH & paie › Temps et absences · `hr.read` |
| `payroll` | Bulletins en brouillon (période ouverte) | Aujourd'hui | Groupe | `hr.read` | `hr.payroll` | `GET /hr/payroll/periods` → première période `Status ≠ Closed` : `DraftPayslipCount` / `PayslipCount` | 22 › Paie |
| `events-today` | Événements du jour | Aujourd'hui (information) | Unité | `mice.read` | — | `GET /mice/events?hotelUnitCode&from=to=aujourd'hui` → nombre | 28 Groupes & MICE · `mice.read` |
| `backup` | Sauvegarde | En retard si `IsOverdue`, sinon À surveiller | Système | `maintenance.read` | `maintenance.backup` | `GET /maintenance/backups/status` → `AgeHours`, `IsOverdue`, `OverdueThresholdHours` | 18 Sauvegarde · `maintenance.read` |
| `workstations` | Postes en service | À surveiller | Système | `sync.read` | — | `GET /sync/stations` → `Workstations.Count`, `DistinctAppVersions` | 27 Postes & erreurs · `sync.read` |

Remarques d'honnêteté :

- **Aucune soustraction ni seuil côté client.** « Créances échues » n'est pas `Total − NotDue`
  (un calcul) mais la tranche `Over90` telle que le serveur la renvoie ; la tranche est nommée dans
  la carte. Les « nombres de lignes » d'une liste filtrée par statut sont des comptes de réponse,
  pas des agrégats ; ils sont présentés comme tels (« 3 commandes ») et jamais additionnés.
- **`GrandTotal` n'est affiché que sur l'appel `status=Confirmed`** ; le sens de `GrandTotal` sans
  filtre n'est pas documenté et la carte ne le devine pas.
- **La date métier est celle du serveur** : `BusinessDate` est le lendemain de la dernière journée
  clôturée (`BusinessDay.Resolve`), `IsLate` vaut `BusinessDate < CalendarDate` et `PendingDays` en
  est l'écart. Le bandeau affiche donc « date métier 31/08 · 1 journée à clôturer » quand le
  calendrier dit 01/09 — le client ne compare aucune date, il relaie trois champs.
- **Le cockpit DEC est groupe-entier** : ses cartes portent l'étiquette « Groupe » même chez un
  directeur d'unité. La clôture *de son unité* vient de `/lodging/business-date` (étiquette code
  d'unité). Deux cartes, deux périmètres nommés, pas de fusion.
- **`approvals`** n'est composée qu'avec `approvals.decide` : `GET /pending` renvoie 403 aux
  porteurs d'`approvals.read` seul (`cashier`, `hr.manager`, `reader`). Ils n'ont ni carte ni
  compteur ; l'onglet 16 reste ouvrable par la barre latérale et le catalogue.
- **Rien n'est inventé pour 01** : tâches, notifications, messagerie, agenda, favoris, demandes,
  délégations n'ont aucune carte. Ils restent dans le catalogue comme nœuds `Planifié` avec leur
  badge, via la section « Catalogue des modules ».

## 4. Règles de composition (fonction pure)

```text
pour chaque file du registre :
  si !has(Lecture)                         → absente
  sinon mode = (Action ≠ null ∧ !has(Action)) ? Suivi : À faire
  cibleVerrouillée = !has(ÉcranCible.Permission)          (miroir de ModuleTile.IsLocked)
  si Périmètre = Unité ∧ aucune unité de poste  → état SansUnité (regroupé en un encart)
bande finale = bande du registre, ou bande dynamique sur un booléen serveur (IsLate, IsOverdue)
ordre dans une bande : À faire avant Suivi, puis ordre du registre
```

- `has(clé)` est fourni par la fenêtre (`HasModulePermission`) ; recommandation : le faire passer par
  `PermissionRegistry.AcceptedClaims` (Domain, déjà référencé par Desktop) pour qu'un rôle
  personnalisé porteur de clés cibles seules compose le même accueil que l'API lui accorde.
- Hors session (`currentUserPermissions == null`) le composeur reçoit `has = _ => false` : accueil
  vide, invisible de toute façon derrière la carte de connexion.
- Les **sources** (appels) sont déduites des files composées : un profil sans `dashboard.read`
  n'appelle jamais le cockpit DEC ; un profil `hr.manager` n'appelle que `/hr/absences` et
  `/hr/payroll/periods`. Ordre d'appel = du plus léger au plus lourd (`business-date`, `pending`,
  `front-desk`, `board`, `receipts/summary`, `low-stock`, `payment-orders`, `orders`, `absences`,
  `periods`, `events`, `readings`, `backups/status`, `stations`, `revenue/dashboard`, `aging`,
  `dec-cockpit`) pour que les cartes de réception se remplissent en premier.

## 5. États

| État | Où | Rendu |
|---|---|---|
| Chargement | par carte, dès la composition | trois barres `SurfaceSubtleBrush` à la place du compteur et de la légende, libellé visible, bouton désactivé ; `AutomationProperties.Name` « {Libellé}, chargement » ; barre `BusyProgressBar` du bandeau de session pendant chaque appel |
| Prêt | carte | compteur, légende serveur, bouton d'action ou « Voir » |
| Zéro | carte | masquée (En retard, À surveiller) ou atténuée (Aujourd'hui) |
| Bande vide | bande | pictogramme au trait + `EmptyStateTitleText` + `EmptyStateHintText`, `IsHitTestVisible=False` |
| Indisponible | toutes les cartes d'une source dont `RunAsync` a échoué | compteur « — », pastille `StatusRejected` « Indisponible », légende « Détail dans le bandeau de session · F5 pour réessayer » ; le message d'erreur lui-même est celui de `RunApiActionAsync` (§ 3.12), pas une boîte de dialogue |
| Sans unité | encart unique sous le bandeau + cartes unitaires absentes | `WarningBanner` (à promouvoir de `TreasuryView` vers le thème) |
| Cible verrouillée | bouton de la carte | `IsEnabled=False`, cadenas `ModuleCardLockIcon`, info-bulle « Accès non autorisé pour votre profil » (`ModuleTile.AccessDeniedToolTip`), `ToolTipService.ShowOnDisabled` ; la carte reste lisible : le chiffre est un droit de lecture, l'écran un autre |
| Thème changé en session | bandeau de session | comportement actuel (`RedemarrageConseille`) ; l'accueil est construit au démarrage dans le bon thème |

Chargement et double soumission : chaque source est appelée **séquentiellement** dans son propre
`context.RunAsync` (charte § 3.1, client monothread). `RunApiActionAsync` avale l'erreur et la
pousse dans le bandeau ; la vue sait si l'appel a abouti parce que son délégué pose un drapeau en
fin d'exécution — pas de `try/catch` réseau dans la vue. Une source en échec ne bloque pas les
suivantes. Pendant un appel, `MainTabs` est désactivé (`SetBusy`) : la barre latérale reste
active et l'on peut quitter l'accueil ; les cartes se remplissent au fil des réponses.

## 6. Variantes par profil

Les listes de droits sont celles de `SecuritySeeder`, pas des projections documentaires. L'unité
de poste est supposée renseignée (`ALG-CEN`), sauf mention.

| Profil (rôle) | Bandeau | En retard | Aujourd'hui | À surveiller | Ce qu'il ne voit pas, et pourquoi |
|---|---|---|---|---|---|
| **Réception** (rôle personnalisé à créer : `lodging.read/checkin/checkout/reserve/room_move`, `customers.read`, `crm.read`, `housekeeping.read`, `settings.read`) | date métier de l'unité ; unité en texte (pas d'`units.read`) | Arrivées en retard, Départs en retard — *À faire* ; Journée à clôturer — *Suivi*, cible verrouillée (pas de `closing.read`) | Arrivées, Départs — *À faire* ; Chambres à préparer, Tâches à inspecter — *Suivi* (pas d'écriture housekeeping) | Chambres hors service | aucune validation (`approvals.*` absents), aucun chiffre financier, aucune carte 01 ; sans unité de poste : encart et bandes réduites aux deux états vides + raccourcis + produit |
| **Directeur d'unité** (`unit.manager`) | sélecteur d'unité (`units.read`) ; date métier | Arrivées/Départs en retard, Journée à clôturer (`closing.close`) — *À faire* ; Journées non clôturées et Recettes rejetées (Groupe) — *Suivi* (pas de `revenue.validate` ni de vue groupe pour clôturer d'autres unités : le verbe reste « Voir ») | Arrivées, Départs, Chambres à préparer, Tâches à inspecter, Validations en attente de ma décision, Commandes à réceptionner, Événements du jour, Unité sans saisie de recette (Groupe, `revenue.write`) — *À faire* | Recettes à valider (Groupe, *Suivi*), Articles sous le minimum, Chambres hors service, Inventaires à valider (*Suivi*, pas d'`inventory.validate`) | Ordres de paiement (pas de `treasury.read` : carte DEC présente mais cible verrouillée — cadenas, chiffre lisible), créances (pas de `receivables.read`), RH, sauvegardes, postes |
| **Direction générale** (`direction`) | sélecteur d'unité (facultatif : les cartes unitaires suivent l'unité choisie) | Journées non clôturées, Recettes rejetées — *Suivi* ; Créances > 90 j ; Sauvegarde en retard si `IsOverdue` | Validations en attente de ma décision, Ordres de paiement à approuver, Commandes d'achat à approuver, Inventaires à valider — *À faire* ; Recettes à valider, Unités sans saisie, Absences à approuver, Bulletins en brouillon — *Suivi* (pas de `revenue.validate`, `revenue.write`, `hr.write`, `hr.payroll`) ; Arrivées/Départs de l'unité choisie — *Suivi* | Articles sous le minimum, Postes en service (versions distinctes), Relevés HACCP, Sauvegarde à l'heure | rien de 01 au-delà des trois boutons ; le tableau de bord PDG reste à un clic dans 20 |
| **Caisse** (`cashier`) | unité en texte (pas d'`units.read`) ; date métier | Arrivées/Départs en retard — *À faire* (`checkin`/`checkout`) | Arrivées, Départs, Chambres à préparer (`housekeeping.write`), Ordres de paiement à régler (`treasury.write`), Encaissements en brouillon, Encaissé aujourd'hui (information, unité du poste) | Chambres hors service | validations (`approvals.read` seul → pas d'appel, pas de carte), clients/factures (droits absents), tout pilotage |
| **RH** (`hr.manager`) | sélecteur d'unité (`units.read`) mais aucune file unitaire | état vide « Rien en retard » | Absences à approuver, Bulletins en brouillon (période ouverte) — *À faire* | état vide « Rien à surveiller » | tout le reste ; c'est l'accueil **le plus court** : deux cartes, trois boutons, raccourcis, produit — et il est utile |
| **Administrateur** (`system.administrator`) | sélecteur d'unité | toutes les files « En retard » du registre, *À faire* ; Sauvegarde si `IsOverdue` | toutes, *À faire* (jusqu'à 17 cartes : la densité Compact lui est destinée) | Articles sous le minimum, Chambres hors service, Postes en service, Sauvegarde | rien ; c'est le seul profil pour lequel l'accueil peut dépasser deux écrans de haut |
| **Lecture seule** (`reader`) | sélecteur d'unité | Arrivées/Départs en retard, Journées non clôturées, Recettes rejetées, Créances > 90 j — tous *Suivi* | Arrivées, Départs, Chambres, Recettes à valider, Unités sans saisie, Inventaires, Commandes, Événements, Relevés HACCP — tous *Suivi* | Articles sous le minimum, Chambres hors service | aucun verbe d'action nulle part : la ligne de synthèse dit « 0 à faire · 12 à suivre — profil en lecture seule » ; validations absentes (pas de `decide`) ; ni trésorerie ni RH ni système |

Un profil sans aucune donnée d'exploitation (par exemple un rôle personnalisé n'ayant que
`settings.read`) voit : le bandeau, les trois boutons Mon profil / Mes préférences / Ma sécurité,
les trois états vides, ses derniers écrans ouverts sur ce poste, la carte « Où en est le produit ? »
et l'accès au catalogue. Aucune page blanche, aucune promesse.

## 7. Tokens et composants utilisés

- **Conteneurs** : `CardBorder` (carte de file, bandeau, carte produit), `SubtleCardBorder` /
  `SurfaceSubtleBrush` (carte *Suivi* ou à 0), `WarningBanner` + `WarningBannerText` (encart sans
  unité — à promouvoir de `TreasuryView.xaml` vers `RaqmiTheme.xaml`).
- **Textes** : `HomeGreetingText` 26, `HomeDateText` 13, `HomeSectionLabel` 11 (titres de bande),
  `HomeStatValueText` 27 (compteur), `SubtitleText` 12,5 (ligne de synthèse), `CaptionText` 11,5
  (légende serveur, étiquette de périmètre), `EmptyStateTitleText` / `EmptyStateHintText`,
  `LabelText` (libellé du sélecteur d'unité, relié par `AutomationLabels.LinkPrecedingLabel`).
- **Pastilles** : `StatusRejectedBackground/Foreground` (point et compteur de « En retard »,
  pastille « Indisponible »), `StatusSubmittedBackground/Foreground` (« Aujourd'hui »),
  `StatusDraftBackground/Foreground` (« À surveiller », pastille « Suivi »),
  `MaturityBadge.Planned` (badge du domaine 01 sur la carte produit), `ModuleCardLockIcon`.
- **Boutons** : `SecondaryButton` (action de carte, « Actualiser »), `GhostButton` (« Voir », Mon
  profil / Mes préférences / Ma sécurité), `FilterChip` (sélecteur de section, derniers écrans),
  `SearchClearButton` (inchangé dans le catalogue).
- **Icônes** : `ModuleGroupIcon.<Clé>` du domaine cible en tête de carte (16 px, trait 1.4,
  couleur du texte), `ModuleGroupIcon.MonEspace` sur la carte produit ; pictogrammes d'état vide
  au trait (32 px, 1.3).
- **Mouvement** : `HomeRevealStyle` / `Delayed` / `DelayedMore` sur bandeau, bande 1, bandes 2-3 ;
  aucune animation sur les chiffres.
- **Deux ressources non colorées nouvelles**, en `DynamicResource` à côté de `GridRowHeight` :
  `WorkCardPadding` (`16,14` confortable / `12,10` compact) et `WorkCardMinHeight` (104 / 88).
  Compact retire de l'air, pas de texte (§ 3.14). Aucun brush nouveau : `ThemePalette.Sombre`
  reste à 82/82.
- **Thème sombre** : toutes les couleurs sont des clés existantes, déjà couvertes ; les accents de
  bande se posent sur `SurfaceBrush` (carte), jamais sur `AppBackgroundBrush`.

## 8. Découpage WPF envisagé

### 8.1 Application (testable sans WPF)

```text
src/RaqmiSystem.Application/Workbench/
  WorkBand.cs             enum Overdue | Today | Watch
  WorkScope.cs            enum Me | Unit | Group | System
  WorkMode.cs             enum Act | Watch
  WorkSource.cs           enum des appels (FrontDesk, BusinessDate, HousekeepingBoard, PendingApprovals, DecCockpit, …)
  WorkQueueDefinition.cs  record (Id, Label, Band, Scope, ReadKey, ActKey?, Source, TargetTabIndex, TargetReadKey, ActVerb, WatchVerb)
  WorkQueueCatalog.cs     le registre § 3 (statique, une seule fois, comme FunctionalArchitectureCatalog)
  WorkbenchComposer.cs    Compose(Func<string,bool> has, bool hasUnit) → WorkbenchPlan (Sources ordonnées, Slots)
  WorkbenchProjection.cs  Project(plan, WorkSourceResults) → IReadOnlyList<WorkCard> (bande finale, Count, Amount, Legend, State)
  WorkCard.cs             record de présentation (aucune dépendance WPF)
```

`WorkSourceResults` reçoit les *records de réponse existants* (`FrontDeskResponse`,
`DecCockpitResponse`, `BusinessDateResponse`, `RoomBoardResponse`, `AgingBalanceResponse`,
`BackupStatusResponse`…) : la projection est une fonction pure des réponses, donc testable avec des
records construits à la main.

Tests (`tests/RaqmiSystem.Tests`, qui ne référence pas Desktop) :

- `WorkbenchComposerTests` : par jeu de clés `Only(...)` (une clé de lecture → une file ; action
  absente → mode Watch ; cible sans permission → `TargetLocked`) et **par rôle réel** via
  `SecuritySeeder` sur SQLite in-memory (`CreateSeededContextAsync` de `SecuritySeederTests`) : le
  tableau § 6 devient sept tests.
- `WorkbenchProjectionTests` : `BackupStatusResponse(IsOverdue: true)` → bande Overdue ;
  `FrontDeskResponse` à 2 `OverdueArrivals` → carte 2 ; 0 → masquée hors « Aujourd'hui » ; source
  absente → `Unavailable` pour toutes ses cartes ; sans unité → `NoUnit`.
- Un test d'intégration `RaqmiApiFactory` par rôle sur `/approvals/instances/pending` (200 pour les
  quatre décideurs, 403 sinon) fixe le contrat que la carte `approvals` consomme.

### 8.2 Desktop

```text
src/RaqmiSystem.Desktop/Views/
  HomeView.xaml(.cs)           hôte de l'onglet 0 : sélecteur de section + ContentControl ; expose
                               OpenSession(displayName), ResetState(), FocusCatalogSearch(), RecordVisit(tab)
  WorkbenchView.xaml(.cs)      contrat de vue § 2.1 : Initialize(context) sans réseau, LoadAsync()
                               (compose, puis une RunAsync par source, séquentielles), ResetState() ;
                               événement NavigateRequested(int tabIndex) comme DecCockpitView
  ModuleCatalogView.xaml(.cs)  extraction à l'identique des blocs 2-4 de l'accueil actuel
                               (~330 lignes XAML, ~180 lignes C#) ; reçoit IReadOnlyList<ModuleTile>
                               et Action<int> naviguer ; garde HomeSearchTextBox et ses handlers
src/RaqmiSystem.Desktop/
  WorkCardModel.cs             INotifyPropertyChanged sur WorkCard (Count, Amount, Legend, State, IsTargetLocked, ToolTipText, AutomationName)
  DesktopSettings.cs           + StationUnitCode (string?), + RecentTabs (int[] ≤ 6) — même load-modify-write qu'Apparence
  Themes/RaqmiTheme.xaml       + WorkCardPadding / WorkCardMinHeight (DynamicResource), + WarningBanner promu, + DataTemplate WorkCardTemplate
  ThemeManager.cs              AppliquerDensite pose aussi les deux clés WorkCard*
```

Points de contact avec `MainWindow` (les seuls) :

| Fichier | Aujourd'hui | Demain |
|---|---|---|
| `MainWindow.xaml` 611-944 | contenu de l'onglet 0 en dur | `<views:HomeView x:Name="HomeView" NavigateRequested="HomeView_NavigateRequested"/>` — la balise `<TabItem Header="Accueil">` reste la première, sans `x:Name` |
| `MainWindow.xaml.cs` 238-242 (connexion) | `HomeGreetingTextBlock.Text = …` | `HomeView.OpenSession(displayName)` ; après `ApplyModulePermissions()` : `HomeView.Workbench.Initialize(context)` puis `await HomeView.Workbench.LoadAsync()` (remplace le préchargement Units/Revenue/Dashboard qui ne sert que les onglets 1-3, conservé si besoin) |
| `MainWindow.xaml.cs` 484-490 (déconnexion) | salutation remise, permissions null | `HomeView.ResetState()` |
| `MainWindow.Navigation.cs` `NavigateToModule` | `MainTabs.SelectedIndex = tab` | + `HomeView.RecordVisit(tab)` (derniers écrans, par poste) |
| `MainWindow.Navigation.cs` 411-419 | `DecCockpitView_NavigateRequested` | même gabarit pour `HomeView_NavigateRequested` : garde `CanOpenModule`, puis `NavigateToModule` |
| `MainWindow.Navigation.cs` 615-794 | logique du catalogue | déplacée dans `ModuleCatalogView` ; `ApplyModulePermissions` continue de poser `IsLocked` sur les 50 `ModuleTile` (partagés avec la barre latérale) |
| `MainWindow.Shortcuts.cs` Ctrl+K | `HomeSearchTextBox.Focus()` | `HomeView.FocusCatalogSearch()` (bascule la section puis focus) |
| `EnsureMaturityBadgeStyles` + `MaturityBadgeFallback.*` | repli mort | supprimés avec l'extraction |

Ce qui **ne change pas** : les 31 `<TabItem>` et leur ordre, les 30 `x:Name`, les 30 appels
littéraux `ApplyModuleAccess(PermissionCatalog.X, XTabItem)`, `ModuleCatalog.cs`, `ModuleTile`,
`ModuleNavigationGroup`, la barre latérale, le fil d'Ariane, `SyncSidebarToTab` (barre repliée sur
l'onglet 0), `DocShots` (capture l'onglet 0 : il montrera le Poste de travail chargé — la vue est
stable une fois ses appels terminés, aucune fenêtre modale).

### 8.3 Clavier et accessibilité

- Ordre de tabulation dans l'onglet 0 : sélecteur de section → sélecteur d'unité → Actualiser →
  Mon profil / Mes préférences / Ma sécurité → boutons des cartes, bande par bande → derniers
  écrans → « Ouvrir le catalogue ».
- Titres de bande : `AutomationProperties.HeadingLevel=Level2`, nom « En retard, 3 files ».
- Carte : `AutomationProperties.Name` posé sur le `Border` racine (« Arrivées en retard, 2,
  ALG-CEN, à faire ») ; le bouton porte un `Content` textuel (« Traiter les arrivées ») — jamais
  un bouton à template sans nom comme `ModuleCatalogCard` aujourd'hui.
- Ligne de synthèse : `LiveSetting=Polite`, annoncée à chaque fin de chargement.
- Raccourcis : `F5` → `RefreshWorkQueuesButton` ; `Ctrl+K`/`Ctrl+F` → recherche du catalogue ;
  `Alt+Origine` → Poste de travail ; `Ctrl+PageDown` → premier onglet de l'ordre de l'arbre
  (inchangé) ; `Ctrl+N`/`Ctrl+S` → « Cet écran n'a rien à créer / enregistrer » (inchangé).
- Couleur jamais seule : le point de bande est doublé du mot, la carte *Suivi* porte la pastille
  « Suivi », le verrou porte le cadenas et l'info-bulle, l'indisponibilité porte le mot.

## 9. Rupture maîtrisée : ce que l'utilisateur perd, ce qu'il gagne

| Perd | Gagne |
|---|---|
| Le catalogue comme première image (23 puces de domaine, bandeau d'avancement, 50 cartes) : il est à un clic (section « Catalogue des modules », carte « Où en est le produit ? », `Ctrl+K`). | Un premier écran **utile sans défiler** à 1240 × 760, là où aucune carte de module n'était visible. |
| La réponse détaillée « où en est le produit ? » en tête de page : réduite à une ligne de chiffres statiques et un bouton. | Une réponse à « que dois-je faire ? » : compteurs serveur, verbe d'action, écran cible en un clic, `F5` pour rafraîchir. |
| Un accueil instantané (aucun appel) : le Poste de travail lance de 2 (RH) à 17 (administrateur) appels séquentiels à la connexion ; `MainTabs` est gelé pendant chacun. | Une composition par permission : la réception ne voit ni finance ni RH, le lecteur voit tout en *Suivi*, l'administrateur voit tout en *À faire*. Rien de faux, rien de grisé « bientôt ». |
| Le rôle de « plan du site » pour l'administrateur : la barre latérale le tient (22 domaines), le catalogue aussi. | Une unité de poste et des écrans récents **par poste**, cohérents avec la doctrine thème/densité. |
| Rien côté sécurité : le masquage reste un confort, `CanOpenModule` et les politiques serveur restent les gardes. | Une logique d'accueil testable sans WPF (composeur + projection dans Application), alignée sur `SecuritySeeder`. |

## 10. Compromis et questions ouvertes

1. **Urgence sans règle métier** : la bande est fixée par le registre ou par un booléen serveur.
   Conséquence assumée : une recette « à valider depuis 4 jours » reste dans *Aujourd'hui* avec
   « J-4 » en légende (`OldestAgeDays`) ; la faire monter dans *En retard* demanderait un seuil,
   donc une règle — à porter côté serveur si on la veut (champ `IsOverdue` sur
   `DecPendingValidationUnit`).
2. **Créances** : `Over90` seul, pas de total « échu » (calcul). Si la direction veut « échu à
   partir de J+1 », c'est un champ à ajouter à `AgingBucketsResponse` (`Overdue` = somme serveur).
3. **Commandes à réceptionner** : `status=Approved` en v1 ; `PartiallyReceived` demanderait un
   second appel ou un filtre multi-statut côté API.
4. **Cockpit DEC** : appel moyen-lourd, lancé en dernier ; pour un directeur d'unité il est
   groupe-entier (étiquette « Groupe »). Un paramètre `hotelUnitCode` sur `/pilotage/dec-cockpit`
   serait l'amélioration serveur la plus rentable de ce concept.
5. **Gel de `MainTabs` pendant le chargement** : accepté en v1 (cartes remplies au fil de l'eau,
   barre latérale active). Une surcharge `RunAsync(action, freezeTabs: false)` réservée aux
   lectures d'accueil est une évolution possible du contrat, pas une condition.
6. **Unité de poste sans `units.read`** (caissier) : saisie du code dans Paramétrage global ›
   Poste de travail (réglage par poste, comme l'URL API). Le serveur ne valide pas ce code avant le
   premier appel ; un code faux donne des cartes « Indisponible » avec le message du serveur.
7. **`Mon profil` / `Mes préférences` / `Ma sécurité`** ouvrent des écrans existants (onglet 9 et
   la boîte de changement de mot de passe) : ils n'annoncent pas un sous-module 01 livré. Le
   domaine 01 garde sa maturité `Planned` ; l'accueil n'en change pas le badge.
8. **Libellé du bouton racine** : « Accueil » ou « Mon Espace » (question 3 de
   `navigation-shell.md` § 11) — ce concept propose **« Poste de travail »** comme libellé de
   section et laisse le bouton racine hors de son périmètre.
9. **Derniers écrans par poste** : sur un poste partagé (réception de nuit), les récents sont ceux
   du poste, pas de la personne — assumé et dit dans le libellé « (ce poste) ».
10. **Compteurs de l'en-tête de bande** (« En retard · 3 ») comptent des **cartes**, pas des objets
    (2 arrivées + 1 départ + 1 journée = 3 cartes) : ce n'est pas un chiffre métier, il n'est jamais
    présenté comme tel.
