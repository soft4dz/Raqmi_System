# Exploration 3 — Portail Mon Espace

**Statut** : exploration indépendante, à trancher sur la maquette · **Angle imposé** : accueil orienté espace
personnel · **Maquette** : `maquette.html` (autonome, données fictives annotées) · **Référence** :
`reorg/phase-1` @ `e7dcaad` · **Rédaction** : 2 septembre 2026.

> **Parti pris.** L'accueil cesse d'être la vitrine du produit pour devenir la page de *la personne
> connectée* : ce qui l'attend, où en est sa journée, où elle va, ce qu'elle a fait — et, en dernier,
> où en est le produit. Chaque bloc n'existe que si une route serveur *existante* et une permission
> *détenue* le justifient. Rien n'est simulé, rien n'est « bientôt » ; le catalogue des 50 modules
> ne disparaît pas, il se replie.

---

## 1. Intention

L'accueil actuel répond à une seule question — « où en est votre ERP, module par module ? » — et la
pose à tout le monde, y compris à la réceptionniste de nuit qui n'ouvrira jamais que trois écrans. À
1240 × 760, la première carte de module est à ~760 px du haut : personne ne voit une carte sans
défiler, et personne n'y lit ce qui le concerne.

Le portail renverse l'ordre des questions, dans l'ordre où un utilisateur se les pose en arrivant :

| Ordre | Question de l'utilisateur | Section du portail | Ce qui la rend honnête |
|---|---|---|---|
| 1 | Qui suis-je ici, avec quels droits, quel jour ? | **Bandeau identitaire** | réponse de connexion, `GET /api/v1/me`, `GET /settings`, `GET /lodging/business-date` |
| 2 | Qu'est-ce qui attend **ma** décision ou **mon** geste ? | **À traiter** | files d'action = route de lecture existante **+** clé d'action détenue |
| 3 | Où en est ma journée ? | **Ma journée** | instantanés de lecture, un appel par tuile, chiffres serveur |
| 4 | Où vais-je ? | **Mes écrans** | l'arbre élagué de la barre latérale, rendu en puces + derniers ouverts (poste) |
| 5 | Qu'ai-je fait ? | **Mon activité** | `GET /audit?userId=moi` — seulement avec `audit.read` |
| 6 | Où en est le produit ? | **Où en est le produit ?** | bandeau d'avancement inchangé + catalogue replié |

Quatre règles fixent le parti pris et ne se négocient pas écran par écran :

1. **Composé par permissions, jamais par rôle.** Le portail lit `login.User.Permissions` (comme
   `ApplyModulePermissions`) et un *registre des capacités* : une tuile apparaît si toutes ses clés
   sont détenues (alias acceptés) et, pour les routes unitaires, si le poste connaît son unité.
2. **Une tuile = une route qui existe + un champ de sa réponse.** Aucun chiffre calculé côté client,
   aucune agrégation maison. Une tuile absente signifie « droit absent » ou « service inexistant »,
   jamais « fonctionnalité à venir ».
3. **Ce qui n'existe pas côté serveur n'a pas de tuile.** Tâches, notifications, messagerie, agenda,
   favoris serveur, demandes, délégations : une seule ligne de badge `Planifié` les nomme (ce sont des
   nœuds de l'arbre du domaine 01), sans compteur, sans bouton, sans tuile grisée.
4. **Le catalogue reste atteignable et complet** — 50 cartes, filtres, cadenas, badges — mais replié
   dans la dernière carte, état mémorisé par poste.

La barre latérale reste repliée sur le portail (`SyncSidebarToTab`) et le fil d'Ariane masqué : le
portail *est* le sommaire de la personne, il n'a pas besoin d'un second sommaire à côté.

---

## 2. Structure

### 2.1 Wireframe (profil Réception, 1240 px de large, thème clair)

```text
┌ En-tête 76 px ─────────────────────────────────────────────────────────────────────────────────┐
│ Q  Raqmi System                        ● Nadia Bouzid · n.bouzid · ALG-CEN   [Sombre] [Mot de passe] [F1] [Déconnexion] │
├───────────────────────────────────────────────────────────────────────────────────────────────┤
│ (barre latérale repliée à 0 px, fil d'Ariane masqué : onglet 0)                                │
│                                                                                                 │
│ ┌ CardBorder — Bandeau identitaire ─────────────────────────────────────────────────────────┐   │
│ │ Bonjour, Nadia Bouzid                              Établissement   El Bahdja Hôtels         │   │
│ │ Réception · reception · 10 droits                  Unité du poste  ALG-CEN · Hôtel Riadh…  │   │
│ │ mercredi 2 septembre 2026                          Date métier     02/09/2026  [à jour]     │   │
│ │ [Mon profil] [Mes préférences] [Ma sécurité]       Session         08:12 → 09:12            │   │
│ │                                                                            [Actualiser (F5)] │   │
│ └───────────────────────────────────────────────────────────────────────────────────────────┘   │
│                                                                                                 │
│ À TRAITER · ce que vos droits vous permettent de décider ou d'exécuter · compteurs du serveur    │
│ ┌ tuile ──────────────────────┐ ┌ tuile ──────────────────────┐                                  │
│ │ Arrivées à enregistrer      │ │ Départs à clôturer          │                                  │
│ │ 14 arrivées                 │ │ 9 départs                   │                                  │
│ │ attendues aujourd'hui       │ │ prévus aujourd'hui          │                                  │
│ │ [2 en retard]               │ │ [1 en retard]               │                                  │
│ └─────────────────────────────┘ └─────────────────────────────┘                                  │
│ [Planifié] Mes tâches · Notifications · Mes demandes · Mes délégations · Messagerie · Agenda      │
│            — aucun service serveur aujourd'hui (domaine 01, lot 4.3) ; rien n'est simulé.        │
│                                                                                                 │
│ MA JOURNÉE · unité ALG-CEN · instantanés de lecture, un appel par tuile                         │
│ ┌ Occupation  78 % ───────────┐ ┌ Chambres  12 à faire ───────┐                                  │
│ │ 96 clients présents · 42 ch.│ │ 5 tâches en cours · 1 HS    │                                  │
│ └─────────────────────────────┘ └─────────────────────────────┘                                  │
│                                                                                                 │
│ MES ÉCRANS · 6 écrans ouvrables pour votre profil                                              │
│ [ Rechercher un écran…  (Ctrl+K)                    ]                                            │
│ Derniers ouverts · ce poste   (PMS front office) (Folios · Hébergement & occupation) (CRM)       │
│ 02  Administration & Socle ERP    (Paramétrage global)                                           │
│ 04  Commercial, Clients & CRM     (Clients) (CRM & expérience client)                            │
│ 06  PMS / Hébergement             (Hébergement & occupation) (PMS front office)                  │
│ 08  Housekeeping                  (Housekeeping & chambres)                                      │
│                                                                                                 │
│ ┌ CardBorder — Mon activité ─┐   ← absente pour Réception (pas d'audit.read)                    │
│                                                                                                 │
│ ┌ CardBorder — Où en est le produit ? ──────────────────────────────────────────────────────┐   │
│ │ ● 31 disponibles  ● 0 API prête  ● 0 partiels  ● 19 planifiés       [Afficher les 50 modules] │   │
│ │ ██████████████████████████████░░░░░░░░░░░░░░░░                                              │   │
│ │ 62 % du périmètre déjà utilisable · domaines : ● 11 fonctionnels · 5 aperçus · 6 planifiés  │   │
│ │ ▸ catalogue replié : puces statut / priorité / domaine + 50 cartes groupées par domaine     │   │
│ └───────────────────────────────────────────────────────────────────────────────────────────┘   │
├ Bandeau de session ── ● Prêt. Connecté en tant que Nadia Bouzid (reception). ──────────────────┤
```

Budget vertical à 1240 × 760 (≈ 580 px utiles) : bandeau ≈ 190 px, titre de section ≈ 30 px, première
rangée de tuiles 124 px → **les files d'action sont visibles sans défiler**, là où l'accueil actuel
n'affichait aucune carte de module au-dessus du pli.

### 2.2 Bandeau identitaire

| | |
|---|---|
| **Contenu** | « Bonjour, {DisplayName} » ; libellé du rôle + identifiants de rôle + nombre de droits du jeton ; date du poste (`dddd d MMMM yyyy`) ; trois boutons fantômes **Mon profil / Mes préférences / Ma sécurité** ; à droite, une liste de définition : Établissement, Unité de ce poste, Date métier, Session (ouverte à / expire à) ; bouton `RefreshHomeButton` « Actualiser » (cible de `F5`). |
| **Sources** | `LoginResponse.User` (`DisplayName`, `UserName`, `Roles`, `Permissions.Count`, `MustChangePassword`) et `LoginResponse.ExpiresAt` (aujourd'hui ignoré par le client : on le conserve, on ne le rafraîchit pas) ; `GET /api/v1/me` pour le panneau Profil ; `GET /settings` → `CompanyName` ; `GET /lodging/business-date?hotelUnitCode=` → `BusinessDate`, `IsLate`, `PendingDays` ; `DesktopSettings` pour l'unité du poste, l'apparence, la densité. |
| **Permissions** | Établissement : `settings.read`. Date métier : `lodging.read` **et** une unité de poste. Le reste vient de la session elle-même. |
| **Absences explicites** | Sans `settings.read` : « — settings.read requis pour lire le nom de l'établissement ». Sans unité de poste : « aucune · poste sans unité, lecture au niveau du groupe » et date métier « — par unité, choisissez une unité sur ce poste ». Sans `lodging.read` : date métier « — lodging.read requis pour la lire ». Ce que le serveur ne sait pas (photo, périmètre, sessions ouvertes) n'est pas affiché. |
| **Actions** | **Mon profil** déplie un panneau dans la carte (identifiant, courriel, rôles, droits, état du mot de passe) — pas de boîte de dialogue. **Mes préférences** déplie apparence / densité / unité du poste / état du catalogue, avec le lien « Paramétrage global → Poste de travail » si `settings.read`. **Ma sécurité** ouvre la boîte « Changer mon mot de passe » existante (`ChangePasswordButton_Click`) via un événement de la vue, comme `DecCockpitView.NavigateRequested`. **Actualiser** relance le chargement de toutes les tuiles. |
| **Date métier** | Pastille `StatusValidated` « à jour » si `!IsLate`, `StatusSubmitted` « en retard · n jours » sinon. La date métier d'une unité n'est jamais déduite de l'horloge du poste. |

### 2.3 À traiter — files d'action

| | |
|---|---|
| **Contenu** | Une tuile par *file d'action* que le profil peut réellement traiter : titre, compteur (27 px), unité de compte, légende, pastille d'alerte éventuelle (`StatusSubmitted`), badge de maturité si la projection est partielle (« Mes validations » : `Aperçu technique`, un seul sujet `PaymentOrder`). |
| **Règle d'apparition** | clé de **lecture** de la route **+** clé d'**action** qui rend la file « mienne » (approuver, valider, clôturer, enregistrer, inspecter, réceptionner, régler) ; unité du poste connue si la route exige `hotelUnitCode`. Détail : registre § 3. |
| **Source** | un appel par route, dédoublonné : `front-desk` sert Arrivées + Départs + Occupation, `housekeeping/board` sert Inspections + Chambres, `dec-cockpit` sert Recettes à valider + Exploitation du groupe. |
| **État vide** | Si **aucune** file ne s'applique : tuile pleine largeur « **Rien à traiter** » avec l'indice adapté : *« Votre profil ne porte aucun droit de décision ou d'exécution : aucune file d'action ne vous est adressée. Vos écrans de lecture restent ci-dessous. »* (Lecture seule) ; *« Vos files d'action dépendent d'une unité : fixez l'unité de ce poste dans Paramétrage → Poste de travail. »* (unité manquante) ; sinon *« Vos validations et vos files d'action apparaîtront ici dès qu'un élément attendra votre décision. »*. Une file **à zéro** reste affichée (« 0 · aucun inventaire en cours à valider ») : l'utilisateur sait qu'elle est surveillée. |
| **Chargement** | squelette `SurfaceSubtle` à la place de la valeur et de la légende, `aria-busy`. |
| **Erreur** | la tuile passe en `DangerSoft` / `DangerBorder`, valeur « Indisponible », légende « Le serveur n'a pas répondu (détail dans le bandeau de session) · Réessayer (F5) » ; les **autres** tuiles restent chargées (un `RunAsync` par route). |
| **Action** | clic / Entrée → `NavigateToModule(tab)` vers l'écran **et le sous-onglet** de la file (Arrivées → onglet 30, sous-onglet Arrivées). Une tuile dont l'écran n'est pas ouvrable reste lisible mais inerte (`SurfaceSubtle`, info-bulle). |
| **Sous la grille** | une ligne `MaturityBadge.Planned` + « Mes tâches · Notifications · Mes demandes · Mes délégations · Messagerie · Agenda — aucun service serveur aujourd'hui (domaine 01, lot 4.3) ; rien n'est simulé. » |

### 2.4 Ma journée — instantanés

| | |
|---|---|
| **Contenu** | tuiles de lecture sans droit d'action : Occupation, Chambres, Recettes du jour, Encaissements du jour, Exploitation du groupe, Créances, Stocks sous le minimum, Sauvegardes, Postes de travail. Le titre de section précise le périmètre : « unité ALG-CEN » ou « lecture au niveau du groupe ». |
| **Règle** | clé de lecture détenue ; unité du poste si la route l'exige. Montants en `N2` culture courante, suffixe de devise venant de `GET /settings` → `CurrencyLabel`. |
| **État vide** | la section **disparaît** si aucune tuile ne s'applique (profil RH) : pas de carte « rien à voir ». |
| **Chargement / erreur / action** | identiques à § 2.3. |

### 2.5 Mes écrans — raccourcis

| | |
|---|---|
| **Contenu** | champ `HomeSearchTextBox` (Ctrl+K, Ctrl+F, Échap efface, croix `SearchClearButton`) ; rangée « Derniers ouverts · ce poste » (6 puces maximum, sous-onglet nommé quand il diffère de l'écran) ; puis un groupe par domaine ouvrable : numéro, icône `ModuleGroupIcon.<clé>`, nom, point `MaturityDot.TechnicalPreview` si le domaine est en aperçu, et une puce `FilterChip` par écran ouvrable. |
| **Source** | **le même arbre élagué que la barre latérale** : `NavigationTreeBuilder.Build(Tree, GrantedPermissionKeys(), NavigationFilter.Sidebar)` — un seul vocabulaire, un seul élagage. Derniers ouverts : `DesktopSettings.DerniersEcrans` (index d'onglet + sous-onglet), écrit par `NavigateToModule`, filtré à l'affichage par `CanOpenModule`. |
| **Permission** | par écran, sa clé de lecture (`ScreenNode.ReadPermissionKey`) ; un écran verrouillé **n'apparaît pas** ici (il reste visible, cadenassé, dans le catalogue). |
| **État vide** | recherche sans résultat : « Aucun écran ne correspond à « … » » / « Échap efface la recherche. Les 50 modules restent listés dans « Où en est le produit ? ». » Derniers ouverts : la rangée est masquée tant que le poste n'a rien ouvert (la maquette montre un exemple annoté). |
| **Action** | puce → `NavigateToModule(tab)` + sous-onglet. |

### 2.6 Mon activité

| | |
|---|---|
| **Contenu** | carte avec les 5 dernières traces du compte (quand, action lisible + code `module.ressource.verbe`, élément, poste), badge `Aperçu technique` (pas de vue personnelle côté serveur, journal générique filtré), pied « Tout mon journal » → onglet 4. |
| **Source** | `GET /audit?userId={currentUserId}&pageSize=5` — la route et `IAuditQueryService.SearchAsync` acceptent déjà `userId` ; il manque le paramètre dans `RaqmiApiClient.GetAuditLogAsync` / `BuildAuditQuery` (une ligne). Horodatages UTC → heure locale (`UtcToLocalTimeConverter`). |
| **Permission** | `audit.read` (system.administrator, direction, exploitation.control). **Sans elle, la carte n'existe pas** — pas de version dégradée : la seule trace « personnelle » disponible aux autres profils est la rangée « Derniers ouverts » de § 2.5, qui est locale et le dit. |
| **État vide** | « Aucune trace pour votre compte » / « Vos connexions et vos actions apparaîtront ici. » |

### 2.7 Où en est le produit ?

| | |
|---|---|
| **Contenu** | titre `SectionTitleText`, quatre compteurs inline (`HomeStatDot` + valeur 16 px + libellé) Disponible / API prête / Partiel / Planifié, barre segmentée 10 px, légende « n % du périmètre déjà utilisable · 31 modules disponibles sur 50, 19 planifiés », seconde lecture « domaines : 11 fonctionnels · 5 aperçus techniques · 6 planifiés · 0 prêt pour la production — les deux échelles ne s'additionnent pas » (`MaturityDot.*`), bouton `SecondaryButton` « Afficher les 50 modules » / « Replier le catalogue ». |
| **Catalogue déplié** | **le catalogue actuel, extrait tel quel** (puces de domaine, statut, priorité, recherche propre, `ModuleCatalogItemsControl` groupé par domaine avec icône + badge de maturité dans l'en-tête, 50 `ModuleCatalogCard`, cadenas, état vide de filtre). Déplié ou replié par poste (`DesktopSettings.CatalogueDeplie`). |
| **Source** | `ModuleCatalog` (statique, inchangé) et `FunctionalArchitectureCatalog` ; aucun appel réseau. Les compteurs ne prétendent pas venir du serveur : ce sont des faits du build, comme aujourd'hui. |
| **Permission** | aucune ; les cartes verrouillées sont cadenassées avec info-bulle `ModuleTile.ToolTipText`. |
| **Action** | carte → `ModuleTileNavigate_Click` → `NavigateToModule`, inchangé. |

---

## 3. Registre des capacités — ce que le serveur sait projeter aujourd'hui

Le registre est une **liste statique** (dans `RaqmiSystem.Application`, testable) ; chaque entrée cite la
route réelle, le champ de la réponse dont vient le chiffre et les clés requises (« / » = alias accepté,
« + » = toutes requises). Les profils cités sont les rôles seedés par `SecuritySeeder` qui détiennent
toutes les clés ; « (unité) » signale une route qui exige en plus l'unité du poste.

### 3.1 Files d'action (section « À traiter »)

| Tuile | Route → champ | Clés | Ouvre | Profils seedés |
|---|---|---|---|---|
| Mes validations | `GET /approvals/instances/pending` → `Count` | `approvals.decide` / `workflow.request.decide` | 16 Validations | admin, direction, exploitation.control, unit.manager |
| Ordres de paiement à approuver | `GET /treasury/payment-orders?status=Draft` → `Count` | `treasury.read` + `treasury.approve` | 6 Trésorerie › Ordres de paiement | admin, direction, exploitation.control |
| Ordres de paiement à régler | `…?status=Approved` → `Count` | `treasury.read` + `treasury.write` | 6 › Ordres de paiement | admin, cashier |
| Recettes à valider | `GET /pilotage/dec-cockpit` → `PendingValidationCount` · `PendingValidationAmount` | `dashboard.read` + `revenue.validate` | 2 Recettes journalières | admin, exploitation.control |
| Jours à clôturer (unité) | `GET /lodging/business-date` → `PendingDays` · `IsLate` | `lodging.read` + `closing.close` | 5 Clôture journalière | unit.manager ; exploitation.control, admin avec unité |
| Arrivées à enregistrer (unité) | `GET /lodging/front-desk` → `Arrivals.Count` · `OverdueArrivals.Count` | `lodging.read` + `lodging.checkin` | 30 Front office › Arrivées | Réception, cashier, unit.manager ; exploitation.control, admin avec unité |
| Départs à clôturer (unité) | même appel → `Departures.Count` · `OverdueDepartures.Count` | `lodging.read` + `lodging.checkout` | 30 › Départs | idem |
| Chambres à inspecter (unité) | `GET /housekeeping/board` → `AwaitingInspectionTasks` | `housekeeping.read` + `housekeeping.inspect` | 21 Housekeeping | unit.manager ; exploitation.control, admin avec unité |
| Commandes à approuver | `GET /purchasing/orders?status=Draft` → `Count` | `purchasing.read` + `purchasing.approve` | 25 Achats › Commandes | admin, direction, exploitation.control |
| Commandes à réceptionner | `GET /purchasing/orders` → `Count(CanReceive)` | `purchasing.read` + `purchasing.receive` | 25 › Réception | admin, exploitation.control, unit.manager |
| Inventaires à valider | `GET /inventory/counts?status=Draft` → `Count` | `inventory.read` + `inventory.validate` | 24 Stocks › Inventaires | admin, direction, exploitation.control |
| Absences à approuver | `GET /hr/absences?status=Pending` → `Count` | `hr.read` + `hr.write` | 22 RH › Temps et absences | admin, hr.manager |
| Bulletins en brouillon | `GET /hr/payroll/periods` → `DraftPayslipCount` | `hr.read` + `hr.payroll` | 22 › Paie | admin, hr.manager |

`Count(CanReceive)` est un comptage d'un drapeau **renvoyé par le serveur** ligne par ligne (comme
`PurchasingView` le fait déjà), pas une règle métier recalculée.

### 3.2 Instantanés (section « Ma journée »)

| Tuile | Route → champ | Clé | Ouvre |
|---|---|---|---|
| Occupation (unité) | `GET /lodging/front-desk` → `Occupancy.OccupancyRatePercent` · `InHouseCount` · `TotalActiveRooms` | `lodging.read` | 30 › Clients présents |
| Chambres (unité) | `GET /housekeeping/board` → `DirtyRooms` · `PendingTasks` · `OutOfOrderRooms` | `housekeeping.read` | 21 |
| Recettes du jour (unité) | `GET /revenue/daily/summary?from=to=aujourd'hui&hotelUnitCode=` → `Total` · statut | `revenue.read` | 2 |
| Encaissements du jour | `GET /treasury/receipts/summary?from=to=aujourd'hui` → `GrandTotal` · `ConfirmedCount` · `DraftCount` | `treasury.read` | 6 › Encaissements |
| Exploitation du groupe | `GET /pilotage/dec-cockpit` → `UnitHealth.Count(NeedsAttention)` · `ClosingBacklogDayCount` · `RejectedCount` | `dashboard.read` | 20 Cockpit DEC |
| Créances | `GET /receivables/aging` → `Total.Total` · `Total.Over90` | `receivables.read` | 13 |
| Stocks sous le minimum | `GET /inventory/low-stock` → `Count` | `inventory.read` | 24 |
| Sauvegardes | `GET /maintenance/backups/status` → `AgeHours` · `IsOverdue` · seuil | `maintenance.read` | 18 |
| Postes de travail | `GET /sync/stations` → `Workstations.Count` · `Freshness` | `sync.read` | 27 |

### 3.3 Bandeau et activité

| Élément | Route → champ | Clé |
|---|---|---|
| Établissement, devise | `GET /settings` → `CompanyName`, `CurrencyLabel` | `settings.read` |
| Date métier (unité) | `GET /lodging/business-date` → `BusinessDate`, `IsLate`, `PendingDays` | `lodging.read` |
| Mon activité | `GET /audit?userId=&pageSize=5` → `Items` | `audit.read` |
| Profil, session | réponse de connexion, `GET /api/v1/me` | — |

### 3.4 Ce qui n'entre pas dans le registre, et pourquoi

| Sous-module de 01 | Raison | Ce que le portail en montre |
|---|---|---|
| Tableau de bord personnel, Mes tâches, Notifications, Messagerie, Agenda, Mes documents, Mes favoris, Mes délégations | aucune entité, aucune route (`03-cartographie-cible` § 01) | une ligne de badge `Planifié`, sans tuile |
| Mes demandes | un filtre client `CreatedBy == UserName` sur `/approvals/instances` serait une projection, mais la cartographie la classe *Absent* et aucun profil ne l'a demandée | rien — à instruire en lot 4.3 |
| « Mes tâches housekeeping » | `HousekeepingTask.AssignedTo` est une chaîne libre, pas un compte | rien |
| « Mes absences / mes bulletins » | aucun lien `User` ↔ `Employee` | rien |
| Sessions ouvertes | `RefreshToken` sans route de lecture | la ligne Session n'affiche que ce que la connexion a renvoyé |

---

## 4. Composition par permissions

```text
Compose(clésDétenues, unitéDuPoste?, arbreOuvrable) :
  pour chaque capacité du registre :
      retenue si  ∀ groupe de clés requis : ∃ clé du groupe ∈ clésDétenues        (alias)
               et (¬capacité.ExigeUnité ∨ unitéDuPoste ≠ null)
  files      = capacités retenues de genre « file »       → section À traiter (ou état vide typé)
  instantanés = capacités retenues de genre « instantané » → section Ma journée (ou section absente)
  écrans     = arbreOuvrable (déjà élagué par clé de lecture)
  activité   = « audit.read » ∈ clésDétenues
  dateMétier = unitéDuPoste ≠ null ∧ « lodging.read » ∈ clésDétenues
  raisonVide = aucune file détenue → NoActionRights ; files détenues mais unité manquante → UnitMissing ; sinon None
```

- **Clés exactes du jeton**, comme `HasModulePermission`, avec les alias du registre
  (`PermissionRegistry.AcceptedClaims`) pour qu'un rôle personnalisé porteur de clés cibles seules ne
  soit pas masqué à tort — le même correctif profiterait à `HasModulePermission`.
- **Hors session** (`currentUserPermissions == null`) le portail ne compose rien et n'appelle rien ;
  `MainContentGrid` est de toute façon masqué.
- **L'unité du poste** est un réglage local (`DesktopSettings.UniteDuPoste`, code d'unité), posé dans
  « Paramétrage global → Poste de travail » par la personne qui configure le poste. C'est un
  **confort** : les routes restent gardées par leurs politiques, et un code d'unité qu'un profil n'a
  pas le droit de lire renvoie 403 → tuile en erreur explicite, pas un chiffre inventé. Aucune
  affectation utilisateur ↔ unité n'existe côté serveur (décision 4 du README) ; le jour où elle
  existera, elle remplacera le réglage local sans toucher au composeur.

---

## 5. Ce que voient les profils

Permissions **réelles** de `SecuritySeeder.RolePermissions` (pas la projection de
`03-cartographie-cible` § 3.5, qui sur-promet `unit.manager` et `cashier`). Réception n'a pas de rôle
système : rôle personnalisé à créer (décision 8), clés proposées ci-dessous. Unité du poste supposée
renseignée pour les postes d'unité (Réception, Directeur d'unité, Caisse, Lecture seule) et absente
pour les postes de siège.

| Profil (rôle) | À traiter | Ma journée | Mes écrans | Activité | Bandeau |
|---|---|---|---|---|---|
| **Réception** (personnalisé : `lodging.read/checkin/reserve/checkout/room_move`, `customers.read`, `crm.read/write`, `housekeeping.read`, `settings.read`) | Arrivées à enregistrer · Départs à clôturer | Occupation · Chambres | 6 écrans : 02 (Paramétrage), 04 (Clients, CRM), 06 (Hébergement, Front office), 08 (Housekeeping) | — | établissement, unité, date métier |
| **Directeur d'unité** (`unit.manager`) | Mes validations · Jours à clôturer · Arrivées · Départs · Chambres à inspecter · Commandes à réceptionner | Occupation · Chambres · Recettes du jour · Exploitation du groupe · Stocks sous le minimum | 22 écrans (ni 4, 6, 10, 11, 13, 18, 22, 27) | — (pas d'`audit.read`) | complet |
| **Direction générale** (`direction`) | Mes validations · OP à approuver · Commandes à approuver · Inventaires à valider | Encaissements · Exploitation du groupe · Créances · Stocks · Sauvegardes · Postes | 29 écrans (tout sauf 10) | 5 dernières traces | pas d'unité → « lecture au niveau du groupe », date métier non affichée |
| **Contrôle d'exploitation** (`exploitation.control`) | Mes validations · OP à approuver · Recettes à valider · Commandes à approuver · Commandes à réceptionner · Inventaires à valider | Encaissements · Exploitation du groupe · Créances · Stocks | 26 écrans | 5 dernières traces | idem siège |
| **Caisse** (`cashier`) | OP à régler · Arrivées · Départs | Occupation · Chambres · Recettes du jour · Encaissements | 8 écrans (2, 6, 9, 15, 16, 21, 23, 30) | — | unité connue **par le réglage du poste** (pas d'`units.read`) |
| **RH** (`hr.manager`) | Absences à approuver · Bulletins en brouillon | *(section absente)* | 4 écrans (Unités, Paramétrage, Validations, RH & paie) | — | établissement seulement |
| **Administrateur** (`system.administrator`) | les 9 files non unitaires (13 avec une unité) | les 6 instantanés non unitaires (9 avec une unité) | 30 écrans | 5 dernières traces | complet |
| **Lecture seule** (`reader`) | **Rien à traiter** — « Votre profil ne porte aucun droit de décision ou d'exécution… » | Occupation · Chambres · Recettes du jour · Exploitation du groupe · Créances · Stocks | 23 écrans | — | complet |

Trois lectures de ce tableau :

- **Un profil sans donnée d'exploitation garde un accueil utile.** RH n'a aucun instantané : son portail
  est un bandeau, deux files qui lui appartiennent vraiment (absences, paie), ses quatre écrans, la
  ligne des sous-modules planifiés et la carte produit. Pas une page blanche, pas une tuile grisée.
- **Lecture seule est le profil de l'état vide assumé** : il voit des chiffres, pas des files ; le texte
  lui dit pourquoi et où sont ses écrans.
- **« Mes validations » n'a un compteur que pour les quatre rôles porteurs d'`approvals.decide`** ; les
  autres (Caisse, RH, Lecture seule) n'ont que la puce « Validations » dans Mes écrans, sans chiffre et
  sans appel (`GET /pending` leur répond 403 ; `ApprovalsView` le sait déjà).

---

## 6. États

| État | Comportement |
|---|---|
| **Chargement** | ordre d'appel : bandeau (`settings`, `business-date`) → files → instantanés → activité ; chaque tuile passe en squelette puis se remplit à son tour ; `BusyProgressBar` du bandeau de session active pendant la séquence ; message « Chargement de Mon Espace… ». |
| **Erreur partielle** | la tuile fautive passe en erreur (§ 2.3), `SetStatus(isError)` porte le détail ; les autres tuiles sont intactes ; `F5` relance tout. |
| **Rien à traiter** | tuile pleine largeur, pictogramme au trait 32 px, `EmptyStateTitleText` « Rien à traiter », indice typé (§ 2.3), `IsHitTestVisible=False`. |
| **Section sans tuile** | Ma journée et Mon activité disparaissent ; À traiter ne disparaît jamais (c'est l'état vide qui la remplace). |
| **Unité manquante** | bandeau « aucune · poste sans unité » ; les tuiles unitaires ne sont pas composées ; si le profil en aurait eu, l'indice de l'état vide nomme le réglage à faire. |
| **Session expirée** | le jeton dure 60 min et le client ne le rafraîchit pas : la ligne Session affiche « expire à HH:MM » (`ExpiresAt`), et un 401 sur une tuile suit le chemin d'erreur ordinaire. Rien de plus n'est promis. |
| **Déconnexion** | `ResetState()` : salutation « Bonjour », tuiles vidées, panneaux repliés, recherche effacée ; les réglages de poste restent. |
| **Thème changé en session** | les couleurs sont en `StaticResource` : le portail ouvert garde l'ancien thème jusqu'au redémarrage, et Mes préférences le dit dans le bandeau de session (comportement existant, `RedemarrageConseille`). |

---

## 7. Charte : tokens, composants, accessibilité

**Aucun nouveau brush.** Tout le portail se construit avec les ressources existantes de
`RaqmiTheme.xaml` ; rien à ajouter à `ThemePalette.Sombre`, `VerifierCouverture` reste à 82/82.

| Élément | Style / brush |
|---|---|
| Cartes (bandeau, activité, produit) | `CardBorder` (Surface, PanelBorder, rayon 10) ; bandeau Padding 22,24 |
| Salutation, date, rôle | `HomeGreetingText` 26, `HomeDateText` 13, `SubtitleText` 12,5 |
| Liste de définition du bandeau | `MetricLabelText` 11 pour les termes, `BodyText` 13 pour les valeurs |
| Boutons Profil / Préférences / Sécurité | `GhostButton` (texte `SecondaryBrush`), état déplié = `ModuleActiveBackgroundBrush` |
| Actualiser, Afficher le catalogue | `SecondaryButton` |
| Titres de section | `HomeSectionLabel` (11 SemiBold `TextMuted`) + aide en `Normal` ; `AutomationProperties.HeadingLevel` |
| Tuile | `Button` dérivé de `ModuleCatalogCard` (mêmes états : ombre 0,07 → 0,20 et −2 px au survol, bordure `AccentBrush`, anneau de focus `FocusRingBrush` interne, désactivé = `SurfaceSubtle` sans ombre) ; largeur minimale 222 px, hauteur minimale 124 px, Padding 16,18 ; titre 13,5 SemiBold ; valeur `HomeStatValueText` 27 ; légende `HomeStatLabelText` 11,5 / `CaptionText` |
| Alerte de tuile (« 2 en retard ») | pastille `StatusSubmittedBackground/Foreground` |
| Date métier à jour / en retard | `StatusValidated` / `StatusSubmitted` |
| Maturité (Mes validations, Mon activité, domaines) | `MaturityBadge.TechnicalPreview`, `MaturityBadge.Planned`, `MaturityDot.*` |
| Squelette de chargement | `SurfaceSubtleBrush`, rayon 6 |
| Tuile en erreur | `DangerSoftBrush`, `DangerBorderBrush`, texte `DangerBrush` |
| Puces d'écran, derniers ouverts | `FilterChip` (pilule rayon 15) ; icône `ModuleGroupIcon.<clé>` 16 px en `TextMutedBrush` |
| Filtres du catalogue | `FilterChip` / `FilterChipCompact` existants |
| Recherche | gabarit existant : `TextBox` Tag = placeholder, loupe `TextPlaceholderBrush`, `SearchClearButton` |
| État vide | pictogramme au trait 1,3, `EmptyStateTitleText`, `EmptyStateHintText` |
| Activité | `DataGrid` implicite (hauteur `GridRowHeight` en `DynamicResource` : la densité s'y applique), `UtcToLocalTimeConverter` |
| Produit | `HomeStatDot` 8 px `ModuleProgress*`, `HomeProgressSegment` 10 px, `HomeProgressCaptionText`, `SectionTitleText` 16 |
| Apparition | `HomeRevealStyle` (bandeau), `HomeRevealDelayedStyle` (À traiter, Ma journée), `HomeRevealDelayedMoreStyle` (le reste) |
| Retour d'information | `SetStatus` → `SessionStatusBorder`, flash accent 0,9 s / danger 1,6 s ; jamais de `MessageBox` d'information |

Règles respectées et vérifiables à la revue : `AccentBrush` ne porte aucun texte (les puces cochées
utilisent `AccentAction` + `AccentActionForeground`) ; montants `N2` alignés à droite dans les tuiles,
devise venue du serveur ; horodatages en heure locale ; français accentué (« », espace avant `:`) ;
libellés de maturité et de statut à source unique (`FunctionalMaturityMapper.Label`,
`ModuleCatalog.StatusLabel`).

**Densité** : la charte réserve `ApparenceDensite` aux grilles ; le portail ne l'étend pas — seule la
grille de Mon activité réagit. Le catalogue replié par défaut et la barre latérale masquée sont ce qui
rend le portail dense pour l'administrateur.

**Clavier et lecteurs d'écran** :

- ordre de tabulation : en-tête → bandeau (boutons fantômes, Actualiser) → tuiles À traiter → tuiles
  Ma journée → recherche → puces → activité → produit → bandeau de session ; `Alt+Origine` revient au
  portail, `Ctrl+K` / `Ctrl+F` ciblent `HomeSearchTextBox` (nom conservé), `F5` cible
  `RefreshHomeButton` (`ShortcutRouter`), `Ctrl+PageDown` part du premier écran ouvrable comme
  aujourd'hui ;
- chaque tuile est un `Button` **avec** `AutomationProperties.Name` composé (« Arrivées à enregistrer,
  14 arrivées, attendues aujourd'hui, 2 en retard, ouvrir PMS front office ») et `HelpText` = route +
  clés ; `aria-busy` pendant le chargement, `LiveSetting=Polite` sur la valeur — ce que
  `ModuleCatalogCard` n'a pas aujourd'hui (nom accessible vide) ;
- puces nommées par leur `Content` (« Ouvrir Clients ») ; points de maturité nommés ; en-têtes de
  domaine « Domaine 06 PMS / Hébergement, 2 écrans » ; panneaux Profil / Préférences en
  `aria-expanded` sur leur bouton, jamais une fenêtre.

---

## 8. Découpage WPF

### 8.1 Ce qui ne bouge pas

- `MainWindow.xaml` garde ses **31 `TabItem`** dans l'ordre : l'onglet 0 reste la première balise, sans
  `x:Name` ; aucune balise `<TabItem>` n'est ajoutée (le catalogue replié est un `Expander`, pas un
  `TabControl`). `tools/check-module-readiness.ps1` ne voit aucune différence.
- `ModuleCatalog.cs`, `ModuleTile`, `FunctionalDomainOption`, `ModuleNavigationGroup`, les 30 appels
  littéraux `ApplyModuleAccess(PermissionCatalog.X, XTabItem)`, `CanOpenModule`, `NavigateToModule`,
  `SyncSidebarToTab`, `UpdateBreadcrumb`, `DocShots` (champs `MainTabs`, `MainContentGrid`, …) :
  inchangés.
- Les 50 cartes restent atteignables dans l'onglet 0, dans le catalogue replié, avec les mêmes
  gestionnaires.

### 8.2 Nouveaux fichiers

| Fichier | Rôle |
|---|---|
| `src/RaqmiSystem.Application/Workspace/MonEspaceCapabilities.cs` | le registre § 3 : `PortalCapability(Id, Title, Kind, RouteDescription, RequiredKeyGroups, RequiresUnit, TargetTabIndex, TargetSubTab, Maturity?)` |
| `src/RaqmiSystem.Application/Workspace/MonEspaceComposer.cs` | `Compose(IReadOnlySet<string> grantedKeys, bool hasWorkstationUnit, NavigationTree openableTree) → MonEspaceLayout` (§ 4) — **fonction pure, sans WPF, sans réseau** |
| `src/RaqmiSystem.Desktop/Views/MonEspaceView.xaml(.cs)` | le portail (contrat de vue § 2.1 de la charte) : `Initialize(ModuleViewContext)`, `LoadAsync()`, `ResetState()`, `SetSession(displayName, roles, permissionCount, expiresAt)` ; événements `NavigateRequested(int tab, int subTab)` et `ChangePasswordRequested` (précédent : `DecCockpitView.NavigateRequested`) |
| `src/RaqmiSystem.Desktop/Views/ModuleCatalogueView.xaml(.cs)` | les blocs 2-4 de l'accueil actuel extraits tels quels (≈ 330 lignes XAML + 180 C#), alimentés par `IReadOnlyList<ModuleTile>` et un `Action<int>` de navigation ; son champ de recherche s'appelle `CatalogueQueryTextBox` pour ne pas capter `Ctrl+F` |
| `src/RaqmiSystem.Desktop/Views/PortalTile.cs` | modèle de tuile (`INotifyPropertyChanged`) : `Capability`, `State` (Idle / Loading / Loaded / Error), `Value`, `Unit`, `Legend`, `Alert`, `IsOpenable`, `AccessibleName` |
| `tests/RaqmiSystem.Tests/MonEspaceComposerTests.cs` | § 8.5 |

### 8.3 Modifications

| Fichier | Changement |
|---|---|
| `MainWindow.xaml` | le contenu de l'onglet 0 (lignes 611-944) devient `<views:MonEspaceView x:Name="MonEspaceView"/>` ; `MaturityBadgeFallback.*` et `EnsureMaturityBadgeStyles` (repli mort) sont retirés |
| `MainWindow.Navigation.cs` | les champs et gestionnaires du catalogue partent dans `ModuleCatalogueView` ; `EnsureModuleTabLoadedAsync` gagne `case 0 → MonEspaceView.LoadAsync()` (une fois par session, puis `F5`, puis au retour sur l'onglet 0 si le dernier chargement date de plus de 5 minutes — même cadence que le battement de poste) ; `NavigateToModule` enregistre l'écran dans `DesktopSettings.DerniersEcrans` |
| `MainWindow.xaml.cs` | lignes 161 / 240 / 486 : `HomeGreetingTextBlock` / `HomeDateTextBlock` remplacés par `MonEspaceView.SetSession(...)` et `ResetState()` ; `InitializeModuleViews` appelle `Initialize(context)` ; abonnement aux deux événements |
| `MainWindow.Shortcuts.cs` | rien : `HomeSearchTextBox` garde son nom dans la vue ; `F5` trouve `RefreshHomeButton` par convention |
| `DesktopSettings.cs` | trois réglages **par poste** : `UniteDuPoste` (code), `DerniersEcrans` (6 couples onglet / sous-onglet), `CatalogueDeplie` (bool) — même schéma charge-modifie-écrit qu'`Apparence` |
| `SettingsView` (Poste de travail) | un champ « Unité de ce poste » (liste si `units.read`, sinon code saisi) |
| `RaqmiApiClient.cs` | paramètre `userId` sur `GetAuditLogAsync` / `BuildAuditQuery` |
| `MainWindow.xaml` (barre latérale) | `ShowHomeButton` : libellé « Mon Espace », `AutomationProperties.Name` « Mon Espace, accueil » (tranche la question 3 de `navigation-shell.md`) |
| `tools/RaqmiSystem.DocShots/CaptureTarget.cs` | cible 0 renommée « Mon Espace » (et « 49 » → 50) ; `--delay` suffisant pour la séquence d'appels, ou attente d'un drapeau `MonEspaceView.IsLoaded` |

### 8.4 Cycle de vie et appels réseau

```csharp
// MonEspaceView.LoadAsync — un RunAsync par route, en séquence ; l'échec d'une route n'arrête pas les autres.
private async Task LoadRouteAsync(IReadOnlyList<PortalTile> tiles, Func<Task> fetch)
{
    foreach (var tile in tiles) tile.State = TileState.Loading;
    var ok = false;
    await context.RunAsync(async () => { await fetch(); ok = true; });   // RunAsync traduit et affiche l'erreur
    foreach (var tile in tiles) tile.State = ok ? TileState.Loaded : TileState.Error;
}
```

- `Initialize` ne fait aucun appel ; `LoadAsync` sort si le contexte est absent ou la session fermée ;
  `ResetState` remet tout à zéro sans toucher aux réglages de poste.
- Les routes sont **dédoublonnées** : une même réponse (`front-desk`, `board`, `dec-cockpit`) alimente
  plusieurs tuiles. Selon le profil, l'ouverture coûte de 4 appels (Réception : `settings`,
  `business-date`, `front-desk`, `board`) à 12 (Direction : `settings`, `pending`, `payment-orders`,
  `purchasing/orders`, `inventory/counts`, `receipts/summary`, `dec-cockpit`, `aging`, `low-stock`,
  `backups/status`, `sync/stations`, `audit`). Aucune route lourde (`group-dashboard`,
  `kpis/dashboard`, `tape-chart`) n'est dans le registre.
- `RunApiActionAsync` désactive `MainTabs` pendant chaque appel : le portail est inerte le temps de la
  séquence (quelques centaines de millisecondes en réseau local). C'est le comportement de toutes les
  vues ; l'ordre files → instantanés fait apparaître l'essentiel en premier, et le squelette dit ce qui
  arrive. Une variante « silencieuse » de `RunAsync` (sans gel de `MainTabs`) serait une amélioration
  du contrat de vue, hors périmètre de cette exploration.
- Le client est monothread par hypothèse (battement de poste) : pas de `Timer`, pas d'appels
  concurrents ; le rafraîchissement est manuel (`F5`) ou déclenché par le retour sur l'onglet.

### 8.5 Tests (sans WPF)

`tests/RaqmiSystem.Tests` ne référence pas `Desktop` : toute la logique de composition vit dans
`Application`, sur le modèle de `NavigationTreeBuilderTests`.

- `MonEspaceComposerTests` : `Only("approvals.read")` → pas de tuile Validations ;
  `Only("approvals.read","approvals.decide")` → tuile ; `Only("workflow.request.decide")` → tuile
  (alias) ; tuiles unitaires absentes sans unité ; `reader` → zéro file, `EmptyReason.NoActionRights` ;
  `direction` sans unité → `EmptyReason.None` mais `UnitMissing` signalé pour l'indice ; ordre stable des
  tuiles ; aucune capacité ne cite une clé absente de `PermissionCatalog`.
- **Table de vérité par rôle seedé** : `SecuritySeeder` sur SQLite en mémoire
  (`SecuritySeederTests.CreateSeededContextAsync`) → clés effectives de chacun des 7 rôles → le tableau
  du § 5 devient un test paramétré ; toute dérive du seeder ou du registre casse le test.
- `MonEspaceRoutesByRoleTests` (`RaqmiApiFactory`) : `GET /approvals/instances/pending` répond 200 aux
  quatre décideurs et 403 à `cashier`, `hr.manager`, `reader` — le contrat que le portail consomme.
- Smoke WPF : protocole existant (`module-readiness.md`) avec administrateur + Caisse ; DocShots par
  compte de démonstration pour capturer les huit accueils.

### 8.6 Lots

| Lot | Contenu | Dépend de |
|---|---|---|
| A | registre + composeur + tests | — |
| B | `ModuleCatalogueView` extraite (comportement identique, onglet 0 inchangé visuellement) | — |
| C | `MonEspaceView` : bandeau, À traiter, Ma journée, Mes écrans, produit replié ; câblage MainWindow | A, B |
| D | `UniteDuPoste`, `DerniersEcrans`, `CatalogueDeplie` dans `DesktopSettings` + champ Poste de travail | C |
| E | Mon activité (paramètre `userId` client) ; libellé « Mon Espace » ; DocShots | C |

---

## 9. Compromis et risques

1. **Le portail va plus loin que `navigation-shell.md` § 5.2**, qui limite les tuiles aux sous-modules de
   `01` au moins `Functional` : appliquée à la lettre, cette règle donnerait un portail vide à sept
   profils sur huit pendant toute la phase 4. Le portail projette donc des files d'action **d'autres
   domaines**, mais sous une contrainte plus stricte que la spec : route existante + clé de décision +
   champ de réponse cité. Risque : dériver vers un mini-cockpit. Garde-fous : pas d'agrégation, pas de
   route lourde, une tuile ouvre toujours l'écran qui la traite, et le registre est une liste
   revue — pas un espace où chaque module ajoute sa vitrine.
2. **L'unité du poste est un réglage local**, pas un périmètre. La maquette du shell montrait
   « Unité HTL-01 » comme si le serveur la connaissait ; il ne la connaît pas. Un poste mal réglé
   afficherait la journée d'une autre unité : l'unité est donc écrite en clair dans le bandeau et dans
   Mes préférences, et ne se change que dans Paramétrage. Le jour où une affectation serveur existera,
   elle prendra la place sans toucher au composeur.
3. **Gel de `MainTabs` pendant le chargement** (§ 8.4) : accepté, borné par le nombre et le poids des
   routes ; à revoir si le contrat de vue gagne un mode silencieux.
4. **Le catalogue est une section repliée, pas un écran dédié** : ajouter un `TabItem` 31 passerait le
   garde, mais `CanOpenModule` exige une tuile de `ModuleCatalog`, qui ne change pas — l'écran ne
   serait pas ouvrable. La section repliée garde les 50 cartes atteignables sans toucher au catalogue ;
   l'écran dédié reste possible au lot 1.1 (catalogue hiérarchique).
5. **Mon activité dépend d'`audit.read`** : trois rôles seulement. Une route « mon journal » filtrée sur
   le jeton (comme `/account/change-password`) l'ouvrirait à tous ; ce serait un ajout serveur, hors
   exploration.
6. **La ligne Session affiche `ExpiresAt` sans rafraîchir le jeton** : c'est une information, pas une
   promesse de reconnexion. Le libellé le dit (« jeton 60 min »).
7. **Le client compare les clés brutes** : un rôle personnalisé porteur de clés cibles seules serait
   masqué par le WPF alors que l'API l'accepte. Le registre accepte les alias ; `HasModulePermission`
   devrait suivre (`PermissionRegistry.AcceptedClaims`, `Domain` déjà référencé).
8. **Rôle Réception** : il n'existe pas ; les clés proposées (§ 5) sont une recommandation pour la
   décision 8, dont `settings.read` parce que l'unité du poste se règle dans Paramétrage.

---

## 10. Rupture maîtrisée : ce que perd et gagne un utilisateur de l'accueil actuel

| Il perd | Il gagne |
|---|---|
| Les 50 cartes en pleine page dès l'ouverture — elles sont à un clic, repliées, et le poste se souvient du choix | Ce qui l'attend, lisible en une seconde, sans défiler, avec des chiffres qui viennent du serveur |
| Les 23 puces de domaine en tête de page — elles vivent dans le catalogue replié | Ses écrans à lui, groupés par domaine, sans ouvrir la barre latérale ; ses derniers écrans ouverts |
| Le message « où en est votre ERP » en première position — il devient la dernière carte, avec les mêmes compteurs et la seconde lecture par maturité que la spec attendait | Un `F5` qui a un sens sur l'accueil, un état de chargement et d'erreur explicites là où il n'y avait rien à charger |
| La vue d'ensemble des 22 domaines, verrouillés compris, au premier regard — Mes écrans ne liste que l'ouvrable ; les cadenas sont dans le catalogue | La transparence de ses droits : rôle, nombre de droits, ce qui lui est ouvert et pourquoi le reste ne l'est pas |
| Les quatre grands compteurs animés — ils passent en ligne, à 16 px | Une identité de poste (établissement, unité, date métier) que l'accueil ne connaissait pas |
| Rien d'autre : `Ctrl+K`, `Alt+Origine`, `Ctrl+PageDown`, la barre latérale, le fil d'Ariane, les 30 écrans et leurs raccourcis sont inchangés | La liste honnête de ce qui n'existe pas encore, nommée une fois, sans fausse promesse |

Pour l'administrateur, seul profil à embrasser les 22 domaines, la perte est la plus sensible : le
catalogue déplié par défaut sur son poste (`CatalogueDeplie = true`, mémorisé) lui rend l'accueil
d'aujourd'hui sous le bandeau et ses files.

---

## 11. À trancher sur la maquette

1. **Ma journée pour Lecture seule** : six instantanés (proposé — c'est le profil qui consulte) ou
   réservé aux profils opérationnels ?
2. **Unité du poste** : réglage local d'abord (proposé, lot D) ou attendre l'affectation
   utilisateur ↔ unité côté serveur ?
3. **Rafraîchissement au retour sur l'accueil** : automatique au-delà de 5 minutes (proposé) ou
   manuel seulement ?
4. **Catalogue** : replié par défaut partout (proposé) ou déplié par défaut pour l'administrateur ?
5. **Numéro de domaine** visible dans Mes écrans (proposé : oui, comme sur l'accueil actuel) — question
   6 de `navigation-shell.md`.
6. **Libellé « Mon Espace »** sur la première ligne de la barre latérale : le portail tranche pour oui
   (question 3 de la spec) ; à confirmer.
7. **Files à zéro** : garder la tuile (« 0 · aucun … », proposé) ou la retirer et ne montrer que ce
   qui attend ?

---

## Annexe — divergences documentaires relevées en chemin

- `03-cartographie-cible.md` § 3.5 attribue `hr.read` et `revenue.validate` à `unit.manager` et
  `invoices.*` / `customers.read` à `cashier` : faux d'après `SecuritySeeder` ; `cashier` n'a pas non
  plus `units.read`. `navigation-shell.md` § 9 annonce « pas de 02 ni de 22 » pour la direction, qui
  détient `units.read`, `settings.read`, `audit.read`, `maintenance.read`, `sync.read`.
- « Mes validations » ne couvre que les ordres de paiement (`ApprovalSubjectType.PaymentOrder` unique) :
  la tuile porte le badge `Aperçu technique` pour cette raison.
- `/treasury/summary` n'existe pas ; la synthèse est `/treasury/receipts/summary`.
- Textes « 49 modules » (info-bulles de l'accueil, `CaptureTarget`) à passer à 50 lors de l'extraction
  du catalogue.
