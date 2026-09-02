# Exploration 4-hybride — accueil « Catalogue vivant »

> **Statut** : exploration de design, version du 02/09/2026, branche `reorg/phase-1` (HEAD `e7dcaad`).
> Maquette de validation : `docs/design/accueil/explorations/4-hybride/maquette.html` (autonome, deux thèmes,
> sept profils, quatre simulations de données). Rien n'est codé ; aucun autre fichier du dépôt n'est modifié.
>
> Angle imposé : **évolution de l'accueil-catalogue existant**. On garde le repère des cartes par domaine, on les
> rend vivantes, on ajoute une zone « À traiter » en tête et une recherche universelle, avec le minimum de rupture
> pour les utilisateurs actuels.

---

## 1. Intention

L'accueil actuel répond à une seule question : *où en est le produit ?* — 50 cartes, quatre compteurs de statut,
une barre segmentée. C'est honnête et c'est utile au dirigeant, mais pour la réceptionniste de 7 h du matin c'est
un sommaire qu'il faut faire défiler (à 1240 × 760, la première carte est à ~760 px du haut de l'onglet : hors
écran). Elle y cherche une chose : *qu'est-ce qui m'attend ?*

Le parti pris est de **ne pas remplacer le catalogue par un portail**, mais de le faire répondre aux deux
questions à la fois :

1. **Chaque carte porte un signe vital** — un compteur ou un état lu sur le serveur, pour l'écran qu'elle ouvre :
   « 12 arrivées · 3 en retard » sur *PMS front office*, « 2 en attente de votre décision » sur *Workflows &
   validations*, « Sauvegarde en retard · dernière il y a 31 h » sur *Sauvegarde & restauration*.
2. **La zone « À traiter » n'est qu'une projection de ces mêmes signes** : les cartes dont le compteur est
   non nul remontent en tête, les urgentes d'abord. Une seule source, une seule lecture serveur, deux lectures
   à l'écran.
3. **Le badge d'urgence n'est jamais un jugement du client** : il n'apparaît que sur un drapeau que le serveur
   qualifie lui-même (`IsLate`, `IsOverdue`, `Freshness`, `IsCompliant = false`, `NeedsAttention`, listes
   « overdue » du front-desk). Aucun seuil, aucune règle métier dans le WPF.
4. **La recherche devient universelle pour la navigation** : modules, sous-modules et écrans de l'arbre
   `Domaine → Module → Sous-module → Écran`, y compris les nœuds planifiés (badge, jamais ouvrables). Elle ne
   cherche pas de données : il n'existe aucune route de recherche transverse côté serveur, et la maquette le dit.
5. **Ce qui n'existe pas n'apparaît pas comme une fonction** : ni notification, ni messagerie, ni tâche, ni
   agenda, ni favori. Les sous-modules planifiés de `01 Mon Espace` restent des nœuds de l'arbre — visibles dans la
   recherche avec le badge *Planifié*, et sur la carte *Alertes & notifications* avec son statut *Planifié*.

Ce que l'utilisateur actuel retrouve tel quel : la salutation, la date, les 50 cartes au même gabarit (224 × 176),
le groupement par domaine `01…22`, les puces de statut et de priorité, `Ctrl+K`, le clic sur une carte qui ouvre
le même onglet par `NavigateToModule`. Ce qu'il gagne : les cartes visibles sans défiler, ce qui l'attend en haut,
la maturité par domaine (spécification du shell § 5.1 enfin livrée), un nom accessible sur chaque carte.

---

## 2. Ce qui ne change pas, ce qui change

| Aujourd'hui | Catalogue vivant | Pourquoi |
|---|---|---|
| Salutation « Bonjour, {DisplayName} » + date | **inchangé**, complété par l'unité du poste et la date métier quand elles existent | même `HomeGreetingText` / `HomeDateText` |
| Carte « Architecture fonctionnelle » : 23 puces texte (~285 px) | **Rail des domaines** : 23 puces compactes avec l'icône du domaine, sur une ligne (~30 px) | libère ~250 px ; les 22 icônes existent (`ModuleGroupIcon.*`) |
| Carte « Avancement des modules » (~180 px) | **Ligne compacte** « Où en est le produit ? » : 31/50, mini-barre, deux lectures (modules / domaines), bouton *Détail* qui déplie l'ancien bandeau tel quel | l'information reste, elle n'est plus le premier écran |
| Recherche 420 px sous le bandeau | **Recherche universelle** dans le bandeau, à droite de la salutation ; résultats « sous-modules et écrans » au-dessus des cartes | même `HomeSearchTextBox`, même `Ctrl+K`, même `Ctrl+F` |
| Puces statut (5) + priorité (4) | **inchangées**, en compact, plus la rangée **Maturité** (spéc. § 5.1) | rangée prévue, jamais livrée |
| Groupes « Domaine → Module » (~35 en-têtes) | **Un en-tête par domaine** (22 max) avec icône, compteur, **badge de maturité** ; le module devient un libellé discret en bas à droite de la carte | moins d'en-têtes, badge à sa place (§ 6.3 : jamais sur la carte) |
| Carte : icône, priorité, cadenas, titre, description, pastille de statut + badge de maturité superposé | Carte : **même gabarit**, badge de maturité retiré (porté par l'en-tête), **ligne de signal** au-dessus de la pastille, `AutomationProperties.Name` posé | conforme à la spec § 5.1 ; corrige le nom accessible vide |
| Aucun appel API sur l'accueil | **≤ 10 appels légers, séquentiels, dans un seul `RunAsync`**, uniquement pour les cartes ouvrables par le profil | charte § 3.1, § 3.10 |
| Aucune zone de travail | **« À traiter »** : projection des signaux non nuls, urgents d'abord ; états chargement / vide / erreur partielle explicites | charte § 3.5 |

---

## 3. Structure

### 3.1 Wireframe (1240 × 760, barre latérale repliée comme aujourd'hui)

```text
┌ Accueil (TabItem 0 · ScrollViewer) ───────────────────────────────────────────────────────────────┐
│ ┌ Bandeau (CardBorder) ─────────────────────────────────────────────────────────────────────────┐ │
│ │ Bonjour, Nadia B.                                  [🔍 Rechercher un module, un écran… Ctrl+K] │ │
│ │ lundi 1er septembre 2026 · HTL-01 · date métier 01/09 [2 jours non clôturés]                   │ │
│ │ Mon profil  Mes préférences  Ma sécurité            Navigation seulement — pas les données.     │ │
│ └───────────────────────────────────────────────────────────────────────────────────────────────┘ │
│ ┌ À TRAITER  [3 compteurs serveur]                                  actualisé à 08:12 [Actualiser] │
│ │ [▌12 arrivées · 8 départs · 3 en retard · Front Office (en retard)] [9 chambres à nettoyer · 4 à │
│ │  inspecter · Housekeeping] [▌2 jours non clôturés · Contrôle (en retard)]                        │
│ └───────────────────────────────────────────────────────────────────────────────────────────────┘ │
│ Domaines  (Tous 50) (◈01 2) (◈02 3) (◈03 6) (◈04 4) (◈05 1) (◈06 3) (◈07 1) … (◈22 3)             │
│ Statut  Tous·Disponibles·API prête·Partiels·Planifiés   Priorité  Toutes·P0·P1·P2   Maturité  …   │
│ ▏Où en est le produit ?  31 disponibles sur 50 ▮▮▮▮▮▮░░░  0 API prête · 0 partiels · 19 planifiés  │
│  | ● 11 fonctionnels ● 5 aperçus ● 6 planifiés · 0 prêt pour la production          Détail ▸      │
│                                                                                                   │
│ ◈ 01 Mon Espace  (2 modules)  [Planifié] ─────────────────────────────────────────────────────── │
│ ┌────────────┐ ┌────────────┐                                                                     │
│ │ ◈       P1 │ │ ◈       P2 │      Carte 224×176 : icône du domaine, priorité, cadenas si         │
│ │ Workflows… │ │ Alertes &… │      verrouillée ; titre ; description (2 lignes) ; LIGNE DE SIGNAL  │
│ │ Circuits…  │ │ Règles…    │      (● texte serveur / ▌urgent / « Rien en attente » / squelette / │
│ │ ● 2 en att…│ │            │      « Indisponible · F5 » / « Unité du poste non définie ») ;       │
│ │ [Dispo] Mon│ │ [Planifié] │      pastille de statut + libellé du module.                        │
│ └────────────┘ └────────────┘                                                                     │
│ ◈ 06 PMS / Hébergement  (3 modules)  [Fonctionnel] ─────────────────────────────────────────────  │
│ …                                                                                                 │
└───────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Budget vertical estimé avant la première carte : bandeau ~110 px, « À traiter » ~90, rail 30, filtres 30,
ligne produit 40, en-tête de groupe 34, marges ~60 → **≈ 395 px**, contre ≈ 760 aujourd'hui. À 760 px de haut,
une rangée entière de cartes est visible ; à 1080 px, trois.

### 3.2 Les sections

| # | Section | Contenu | Source de données | Permission | État vide / dégradé | Action |
|---|---|---|---|---|---|---|
| A | **Bandeau** | Salutation, date calendaire, unité du poste, date métier, trois boutons fantômes *Mon profil / Mes préférences / Ma sécurité*, recherche universelle | `LoginResponse.User.DisplayName` ; `DateTime.Today` ; `DesktopSettings.UniteDuPoste` (à créer, par poste) ; `GET /lodging/business-date?hotelUnitCode` (`BusinessDate`, `IsLate`, `PendingDays`) | aucune pour la salutation ; `lodging.read` + unité du poste pour la date métier | sans unité : la date métier est omise (pas de texte « non définie » dans le bandeau) ; sans réponse : « date métier indisponible » | *Mon profil* → Paramétrage global, onglet Santé/Session (`GET /api/v1/me`) ; *Mes préférences* → Paramétrage → Poste de travail ; *Ma sécurité* → `ShowChangePasswordDialog` |
| B | **À traiter** | Puces-boutons, une par signal « travail » non nul, urgentes d'abord (filet ambre + pastille *en retard*), puis par poids ; compteur « n compteurs serveur » ; horodatage ; bouton `RefreshHomeSignalsButton` | projection du registre des signaux (§ 4) ; aucune donnée propre | celles des signaux | **aucun compteur pour le profil** : « Aucun compteur pour votre profil — le catalogue ci-dessous reste votre sommaire » ; **tout à zéro** : « Rien à traiter — aucun signal en attente sur vos écrans » ; **chargement** : trois squelettes ; **erreur partielle** : bandeau d'avertissement « 2 compteurs n'ont pas répondu (…) · Réessayer (F5) », les autres puces restent | clic = `NavigateToModule(tab)` de la carte porteuse ; `F5` relit |
| C | **Rail des domaines** | 23 puces `RadioButton` compactes : *Tous 50*, puis `01…22` avec icône, numéro, effectif ; la puce cochée déploie son nom | `FunctionalArchitectureCatalog.Domains` + `ModuleTile.FunctionalDomainId` | aucune | — (les 22 domaines apparaissent toujours, cadenas ou planifiés compris) | filtre le catalogue (même `functionalDomainFilter` qu'aujourd'hui) |
| D | **Filtres + produit** | Puces Statut (5), Priorité (4), **Maturité (4, nouveau)** ; ligne compacte « Où en est le produit ? » à deux lectures et bouton *Détail* qui déplie l'ancien bandeau intact | `ModuleCatalog.CountOf`, `FunctionalArchitectureCatalog.Domains` (statique client, comme aujourd'hui : ce ne sont pas des chiffres métier) | aucune | — | filtres croisés (`MatchesModuleFilters` + maturité) |
| E | **Résultats de recherche** | Quand la saisie n'est pas vide : liste « Sous-modules et écrans (n) » — chemin `Domaine › Module › Sous-module`, pastille *Écran X* si ouvrable, cadenas si non autorisé, badge *Planifié* si nœud sans écran ; les cartes se filtrent en dessous comme aujourd'hui | `NavigationTreeBuilder.Build(Tree, GrantedPermissionKeys(), NavigationFilter.Home with SearchText)` | par nœud : `ScreenNode.ReadPermissionKey` | « Aucun module ne correspond à « … ». Échap efface la recherche. » (existant) | Entrée ouvre le premier résultat ouvrable ; Échap efface ; ligne cliquable = `NavigateToModule` |
| F | **Catalogue vivant** | 50 cartes groupées par domaine `01…22`, en-tête = icône + `NN Nom` + compteur + badge de maturité + filet ; domaine 19 affiché même sans module (« c'est la promesse du catalogue ») ; carte = gabarit actuel + ligne de signal ; badge de maturité retiré de la carte | `ModuleCatalog.Entries` via `ModuleTile` ; signal via § 4 | `ModuleTile.IsLocked` (carte) ; permission de la route (signal) | carte verrouillée : cadenas, fond `SurfaceSubtle`, texte lisible, **aucun appel** ; carte planifiée : pastille *Planifié*, désactivée ; signal : « Rien en attente… » / squelette / « Indisponible · réessayer (F5) » / « Unité du poste non définie » | `ModuleTileNavigate_Click` inchangé |

---

## 4. Registre des signaux (`HomeSignalCatalog`)

Un signal = **une carte** + **une route légère** + **la permission de cette route** + **une portée** (globale ou
unité) + un rendu de libellé. Le composeur (§ 8) décide, pour chaque profil, lequel est *prêt*, *caché*,
*verrouillé* ou *sans unité*. Coûts d'après la cartographie des données (`RaqmiApiClient`, endpoints).

| Signal | Carte (onglet) | Route | Permission de la route | Portée | Libellé — le verbe n'est impératif que si le profil détient le droit d'agir | Urgence (drapeau serveur) | Existe aujourd'hui |
|---|---|---|---|---|---|---|---|
| Validations | 22.2 *Workflows & validations* (16) | `GET /approvals/instances/pending` | `approvals.decide` (filtre serveur par rôle) | globale | « 2 en attente de votre décision » / « Rien en attente de votre décision » | — | oui (`GetPendingApprovalInstancesAsync`) |
| Cockpit | 24.4 *Cockpit DEC* (20) | `GET /pilotage/dec-cockpit` | `dashboard.read` | globale | « 1 unité à surveiller · 2 j de clôture en retard · 1 recette rejetée » | `UnitHealth.NeedsAttention` | oui (`GetDecCockpitAsync`) — coût moyen, un seul appel |
| Recettes du jour | 24 *Tableaux de bord directionnels* (3) | `GET /revenue/daily/dashboard` | `dashboard.read` | globale | « 1 unité sans saisie · 2 à valider » (`revenue.validate`) ou « … en attente de validation » | — | oui (`GetUnitDashboardAsync`) |
| Front-desk | 10.1 *PMS front office* (30) | `GET /lodging/front-desk?hotelUnitCode&date` | `lodging.read` | unité | « 12 arrivées · 8 départs · 3 en retard » | `OverdueArrivals` / `OverdueDepartures` non vides | oui (`GetFrontDeskAsync`) |
| Date métier | 4.5 *Clôture journalière* (5) + bandeau | `GET /lodging/business-date?hotelUnitCode` | `lodging.read` (la carte exige `closing.read`) | unité | « 2 jours à clôturer » (`closing.close`) ou « 2 jours non clôturés » | `IsLate` | oui (`GetBusinessDateAsync`) |
| Housekeeping | 10.2 *Housekeeping & chambres* (21) | `GET /housekeeping/board?hotelUnitCode&date` | `housekeeping.read` | unité | « 9 chambres à nettoyer · 4 à inspecter » (`DirtyRooms`, `AwaitingInspectionTasks`) | — | oui (`GetHousekeepingBoardAsync`) — coût moyen |
| Trésorerie | 5 *Encaissements & trésorerie* (6) | variante par droit : `payment-orders?status=Draft` (`treasury.approve`), `?status=Approved` (`treasury.write`), sinon `receipts/summary` du jour | `treasury.read` | globale | « 3 ordres de paiement à approuver » / « 2 ordres approuvés à régler » / « Encaissé aujourd'hui : 84 500,00 DA (12 reçus) » | — | oui (`GetPaymentOrdersAsync`, `GetCashReceiptSummaryAsync`) |
| Créances | 9 *Créances & recouvrement* (13) | `GET /receivables/aging` | `receivables.read` | globale | « Plus de 90 jours : 412 300,00 DA » (`Total.Over90`, champ serveur, pas une soustraction client) | — | oui (`GetAgingBalanceAsync`) — coût moyen |
| Stocks | 11 *Stocks & consommations* (24) | `GET /inventory/low-stock` | `inventory.read` | globale | « 5 articles sous le minimum » | — | oui (`GetLowStockAsync`) |
| Achats | 12 *Achats & approvisionnements* (25) | `orders?status=Draft` (`purchasing.approve`) ou `?status=Approved` (`purchasing.receive`) ou `Draft` neutre | `purchasing.read` | globale | « 2 commandes à approuver » / « 1 commande à réceptionner » / « 2 commandes en brouillon » | — | oui (`GetPurchaseOrdersAsync`) |
| RH | 21 *RH & paie* (22) | `GET /hr/absences?status=Pending` | `hr.read` | globale | « 4 absences à approuver » (`hr.write`) ou « 4 absences en attente » | — | oui (`GetHrAbsencesAsync`) |
| Cuisine | 11.5 *Cuisine, production & qualité* (26) | `GET /kitchen/readings?nonCompliantOnly=true&from=today` | `kitchen.read` | globale | « 2 relevés HACCP non conformes aujourd'hui » | `IsCompliant = false` | oui (`GetTemperatureReadingsAsync`) |
| MICE | 10.6 *Groupes & MICE* (28) | `GET /mice/events?from=today&to=today` | `mice.read` | globale | « 1 événement aujourd'hui » | — | oui (`GetEventsAsync`) |
| Sauvegarde | 28 *Sauvegarde & restauration* (18) | `GET /maintenance/backups/status` | `maintenance.read` | globale | « Dernière sauvegarde il y a 6 h » / « Sauvegarde en retard · dernière il y a 31 h » | `IsOverdue` | oui (`GetBackupStatusAsync`) |
| Postes | 29 *Registre des postes* (27) | `GET /sync/stations` | `sync.read` | globale | « 1 poste hors ligne sur 7 » / « 7 postes en contact » | `Freshness` | oui (`GetWorkstationsAsync`) |
| Mon activité | 30 *Journalisation & traçabilité* (4) | `GET /audit?userId={moi}&action=auth.login.success&pageSize=1` | `audit.read` | globale | « Votre dernière connexion : 01/09 à 08:12 » (informatif, jamais « à traiter ») | — | **partiel** : route et `IAuditQueryService` acceptent `userId`, `RaqmiApiClient.GetAuditLogAsync` ne l'expose pas |

Ce qui **n'est pas** dans le registre, et pourquoi : notifications, messagerie, tâches, agenda, favoris,
délégations, demandes (aucune table, aucune route) ; « mes absences / mes pointages » (aucun lien `User ↔ Employee`)
; tâches housekeeping « à moi » (`AssignedTo` est une chaîne libre) ; KPI (`/kpis/dashboard` est lourd, et
`/kpis/alerts` n'a pas de méthode cliente — candidat de phase suivante) ; NPS et exécutions de rapports (informatifs,
pas une file de travail).

**Règles d'honnêteté du registre**

1. Un signal n'est lu que si **la carte est ouvrable** par le profil (`ModuleTile.IsClickable`) **et** que la
   route est ouverte (`HasPermission`, résolue avec `PermissionRegistry.AcceptedClaims` pour accepter clés
   historiques et cibles). Un 403 est traité comme « Indisponible », jamais comme une valeur.
2. Le chiffre est **celui du serveur** : compteur d'une liste renvoyée, champ agrégé (`Over90`, `PendingDays`,
   `DirtyRooms`) — jamais une soustraction ni un seuil client.
3. Le **verbe** n'est impératif (« à approuver », « à valider », « à clôturer ») que si le profil détient le droit
   d'agir ; sinon le libellé est neutre (« en attente d'approbation », « non clôturés »). Une lectrice en lecture
   seule ne se voit pas commander de valider.
4. L'**urgence** n'est posée que sur un drapeau serveur (colonne « Urgence »). Sans drapeau, un compteur élevé reste
   un compteur.
5. Une portée **unité** sans unité du poste n'est ni devinée ni masquée : la carte dit « Unité du poste non
   définie » (info-bulle : *Paramétrage global → Poste de travail*), et le signal n'entre pas dans « À traiter ».

---

## 5. États

| Situation | Où | Rendu |
|---|---|---|
| Chargement des signaux | « À traiter » + ligne de signal des cartes | squelettes (barre `SurfaceSubtle` pulsée, 12 px) ; `BusyProgressBar` du bandeau de session active ; les cartes restent cliquables |
| Tous les signaux à zéro | « À traiter » | pictogramme coche + `EmptyStateTitleText` « Rien à traiter » + `EmptyStateHintText` « Aucun signal en attente sur vos écrans. Les compteurs se relisent à l'ouverture de l'accueil ou avec F5. » ; `IsHitTestVisible="False"` |
| Profil sans aucun signal prêt | « À traiter » | « Aucun compteur pour votre profil — Vos écrans n'exposent pas de file de travail. Le catalogue ci-dessous reste votre sommaire ; les cartes verrouillées disent ce qui manque. » ; bouton *Actualiser* grisé |
| Une route échoue (403, réseau, 5xx) | carte + bandeau d'avertissement sous « À traiter » | carte : « Indisponible · réessayer (F5) » en `TextMuted`, info-bulle avec la route ; bandeau `WarningBanner` (à promouvoir de `TreasuryView` vers le thème) : « 2 compteurs n'ont pas répondu (PMS front office, Cockpit DEC). Les autres sont à jour. Réessayer (F5) ». Message d'état `SetStatus(…, isError: true)` ; jamais de `MessageBox` |
| Portée unité sans unité du poste | carte | « Unité du poste non définie » (`TextMuted`), info-bulle vers le réglage ; absente de « À traiter » ; date métier omise du bandeau |
| Carte verrouillée | carte | inchangé : cadenas, `SurfaceSubtle`, texte lisible, info-bulle « Accès non autorisé pour votre profil » ; **aucun appel** |
| Carte planifiée | carte | inchangé : pastille *Planifié*, désactivée, sans ligne de signal |
| Recherche sans résultat | catalogue | inchangé (« Aucun module ne correspond à « … ». Échap efface la recherche. ») |
| Domaine sans module (19) | catalogue | en-tête affiché + « Aucun module rattaché : c'est la promesse du catalogue » — masqué dès qu'un filtre est actif |
| Thème changé en session | bandeau de session | inchangé (`RedemarrageConseille`) ; l'accueil est construit au démarrage dans le thème appliqué |

Rafraîchissement : à l'ouverture de session (après `ApplyModulePermissions`), à chaque retour sur l'onglet 0
si la dernière lecture date de plus de 5 minutes (aligné sur le battement de poste, sans `Timer`), et sur `F5`
(`RefreshHomeSignalsButton`, trouvé par `ShortcutRouter`). Toutes les lectures d'une passe sont **séquentielles
dans un seul `context.RunAsync`** : le client est monothread par hypothèse, et `RunAsync` gèle `MainTabs` le
temps de l'appel — une passe complète tient en une à deux secondes sur des routes légères. Si ce gel est jugé
gênant, la seule alternative conforme est une variante `RunAsync(quiet: true)` du contrat de vue (même
traduction d'erreurs, sans `SetBusy`) : décision à prendre par le propriétaire de la charte, pas par l'accueil.

---

## 6. Variantes par profil

Composées **à partir des permissions**, jamais d'un nom de rôle. Les listes ci-dessous sont celles de
`SecuritySeeder` (branche `reorg/phase-1`) ; les documents `03-cartographie-cible.md` § 3.5 et
`navigation-shell.md` § 9 sur-promettent `unit.manager` (trésorerie, RH, audit) et `cashier` (clients, facturation)
et sont à corriger. L'**unité du poste** est un réglage par poste (`DesktopSettings`, à créer), pas un attribut du
compte : la maquette lui donne une valeur par défaut plausible par profil et un commutateur.

| Profil | Permissions déterminantes | « À traiter » (exemple) | Cartes ouvrables / verrouillées | Ce que l'accueil lui apporte |
|---|---|---|---|---|
| **Réception** (rôle personnalisé à créer, README décision 8 : `lodging.*`, `customers.read`, `crm.read`, `housekeeping.read`, `settings.read`) | pas d'`approvals.read`, pas de `closing.read`, pas de `dashboard.read` | Front-desk (urgent si retards), Housekeeping ; date métier dans le bandeau (`lodging.read` + unité HTL-01) | 5 ouvrables (10, 10.1, 10.2, 9.2, 10.4, 2) ; 25 verrouillées dont *Clôture* et *Validations* ; 19 planifiées | l'écran de 7 h : arrivées, retards, chambres à préparer, puis la carte *PMS front office* à un clic |
| **Directeur d'unité** (`unit.manager`) | `approvals.decide`, `dashboard.read`, `closing.close`, `purchasing.receive`, `kitchen.read`, `mice.read` ; **pas** `treasury.*`, `hr.read`, `audit.read`, `receivables.read`, `revenue.validate` | Validations, Cockpit (urgent si `NeedsAttention`), Front-desk, Clôture « à clôturer », Housekeeping, Recettes « en attente de validation » (pas de `revenue.validate`), Stocks, Achats « à réceptionner », Cuisine, MICE | 22 ouvrables ; verrouillées : 5, 5.2, 9, 21, 22/30, 1, 28, 29 | une file d'exploitation complète, sans un chiffre qu'il ne peut pas lire ailleurs |
| **Direction générale** (`direction`) | `approvals.decide`, `treasury.approve`, `purchasing.approve`, `hr.read` (sans `hr.write`), `audit.read`, `maintenance.read`, `sync.read` ; **pas** `users.read` | Sauvegarde (urgent si `IsOverdue`), Validations, Cockpit, Recettes « à valider » ? — non : **« en attente de validation »** (pas de `revenue.validate`), Trésorerie « à approuver », Créances > 90 j, Achats « à approuver », RH « en attente », Stocks, Postes ; *Mon activité* informatif | 29 ouvrables ; seule *Administration & utilisateurs* verrouillée ; unité du poste non définie → cartes PMS/HK « Unité du poste non définie » | le cockpit avant le cockpit : ce qui attend sa décision et ce qui cloche dans le système |
| **Administrateur** (`system.administrator`) | tout | Sauvegarde, Validations, Cockpit, Trésorerie, Créances, Achats, RH « à approuver », Stocks, Cuisine, Postes… | 31 ouvrables, aucun cadenas ; unité du poste non définie par défaut | la santé du système en tête (sauvegarde, postes), la densité compacte lui est destinée |
| **Lecture seule** (`reader`) | lectures d'exploitation/finance ; **pas** `treasury.read`, `hr.read`, `audit.read`, `approvals.decide`, `maintenance.read`, `sync.read` | Cockpit, Recettes « en attente de validation », Créances, Stocks, Achats « en brouillon », Cuisine, MICE — tous en libellé neutre | 23 ouvrables ; *Validations* ouvrable sans compteur (`approvals.read` seul : `/pending` renverrait 403, donc **pas d'appel**) | un observatoire honnête : elle voit ce qui attend, sans qu'on lui demande d'agir |
| **Caisse** (`cashier`) | `treasury.write`, `revenue.write`, `lodging.*`, `housekeeping.*`, `crm.*`, `approvals.read` ; **pas** `units.read`, `dashboard.read`, `customers.read`, `invoices.read` | Trésorerie « 2 ordres approuvés à régler », Front-desk, Housekeeping (unité du poste HTL-01 saisie dans Paramétrage, puisque `units.read` manque) | 8 ouvrables ; *Validations* sans compteur | la caisse du jour et les mouvements de l'hôtel ; rien qui prétende venir d'un tableau de bord qu'elle n'a pas |
| **RH** (`hr.manager`) | `hr.read/write/payroll`, `units.read`, `settings.read`, `approvals.read` | RH « 4 absences à approuver » — et c'est tout | 4 ouvrables (1, 2, 21, 22.2) | un seul compteur, mais le bon ; avec `sim = vide` la maquette montre « Rien à traiter » |

Un profil réduit à `settings.read` (rôle personnalisé mal doté) voit : le bandeau, « Aucun compteur pour votre
profil », le rail, et un catalogue presque entièrement cadenassé qui dit précisément ce qui manque — utile, non
vide, et sans mensonge.

---

## 7. Tokens et composants de la charte

| Élément | Styles / brushes existants | Nouveau (à ajouter au thème **et** à `ThemePalette.Sombre`) |
|---|---|---|
| Bandeau | `CardBorder`, `HomeGreetingText` 26, `HomeDateText` 13, `SubtitleText`, `GhostButton` ×3, `HomeRevealStyle` | — |
| Recherche universelle | style implicite `TextBox` (Tag = placeholder, `AccessibleNameConverter`), loupe `TextPlaceholderBrush`, `SearchClearButton` | — |
| « À traiter » | `CardBorder`, `HomeSectionLabel`, `SecondaryButton` (Actualiser), `EmptyStateTitleText` / `EmptyStateHintText`, `StatusSubmittedBackground/Foreground` (pastille *en retard*, filet d'urgence), `MaturityBadge.Functional` (« n compteurs serveur ») | `HomeSignalChip` (Button : `Surface` + `PanelBorder`, r 8, 40 px, survol bordure `AccentBrush`, focus `FocusRingBrush`) ; `HomeSignalChip.Urgent` (filet gauche 3 px `StatusSubmittedForegroundBrush`) ; `WarningBanner` promu de `TreasuryView` ; `HomeSkeleton` (Border `SurfaceSubtle` r 6, animation d'opacité 0,55 ↔ 1) — **aucune couleur nouvelle** |
| Rail des domaines | `FilterChipCompact` + `ModuleGroupIcon.*` 16 px (trait lié au `Foreground`), coché = `AccentAction` / `AccentActionForeground`, focus `FocusRingOnFilledBrush` | `HomeRailChip` (dérivé de `FilterChipCompact`, hauteur 30, icône + numéro + effectif) |
| Filtres, produit | `FilterChipCompact`, `FilterRowLabelText`, `SubtleCardBorder`, `HomeProgressSegment` (mini-barre 8 px), `MaturityDot.*`, `HomeStat*` (bandeau déplié) | — |
| Résultats | `SubtleCardBorder`/`CardBorder`, `ModuleNavSubButton` (34 px) pour les lignes, `MaturityBadge.Planned`, `ModuleCardLockIcon` | — |
| En-tête de groupe | `ModuleCatalogGroupHeaderTemplate` + icône (`ModuleGroupIconConverter`) + `MaturityBadge.<Niveau>` via la propriété attachée `MaturityBadge.Maturity` | mise à jour du gabarit, pas de style neuf |
| Carte | `ModuleCatalogCard` 224 × 176 (chip `AccentSoft` quand disponible — usage prescrit par la charte § 1.2 —, `SurfaceHover` sinon, `DisabledBackground` si verrouillée) ; pastille de statut `ModuleStatus*` ; badge de maturité **retiré** | `HomeSignalText` (11,5 SemiBold `TextSecondary`), `HomeSignalText.Urgent` (`StatusSubmittedForeground`), `HomeSignalText.Muted` (11,5 `TextMuted`) ; point 7 px `AccentBrush` / `StatusSubmittedForeground` / `BorderStrong` |
| Session | `SessionStatusBorder`, `SetStatus`, `FlashSessionStrip`, `BusyProgressBar` | — |

Contraste : les nouveaux textes n'utilisent que `TextSecondary` (7,1:1 sur blanc), `TextMuted` (4,6:1) et
`StatusSubmittedForeground` (5,0:1 sur blanc, 4,6:1 sur `SurfaceSubtle`) ; l'accent de marque ne porte que des
points, filets et bordures. Densité : les puces du rail et les lignes de résultats suivent la hauteur de
`ModuleNavSubButton` ; quand la densité de la barre latérale (spéc. § 2.4) sera branchée sur `ApparenceDensite`,
elles y seront liées par la même `DynamicResource`.

---

## 8. Découpage WPF envisagé

### 8.1 Composition pure, testable (`RaqmiSystem.Application/Home/`)

Les tests (`tests/RaqmiSystem.Tests`) ne référencent pas le projet Desktop : toute la décision « qui voit quoi »
vit donc dans `Application`, sur le modèle de `NavigationTreeBuilder`.

```csharp
// Registre statique : ce que le serveur sait rendre, carte par carte.
public sealed record HomeSignalDefinition(
    string Id, string LegacyOrder, string RoutePermissionKey, HomeSignalScope Scope, int Weight,
    IReadOnlyList<HomeSignalVariant> Variants);          // variante = droit d'agir → libellé impératif, sinon neutre

public sealed record HomeSlot(HomeSignalDefinition Signal, HomeSlotState State, HomeSignalVariant? Variant);
public enum HomeSlotState { Hidden, Locked, NeedsUnit, Ready }

public static class HomeComposer
{
    // Fonction pure : permissions accordées (résolues par PermissionRegistry.AcceptedClaims),
    // présence d'une unité de poste, arbre de navigation → un slot par signal.
    public static HomeComposition Compose(IReadOnlySet<string> grantedKeys, bool hasStationUnit, NavigationTree tree);
}

// Projection d'une réponse serveur en valeur affichable : texte, IsWork, IsUrgent (drapeau serveur uniquement).
public static class HomeSignalProjections
{
    public static HomeSignalValue FromFrontDesk(FrontDeskResponse r, bool canAct);
    public static HomeSignalValue FromBusinessDate(BusinessDateResponse r, bool canClose);
    // … une méthode par route ; les DTO vivent déjà dans Application.
}
```

Tests : par jeu de clés (`Only("lodging.read", "housekeeping.read")` → deux slots `Ready` de portée unité,
`NeedsUnit` sans unité ; `Only("approvals.read")` → validations `Hidden`, jamais d'appel) ; par rôle réel via
`SecuritySeederTests.CreateSeededContextAsync` (SQLite in-memory) pour figer les sept compositions ; projections
(`FromBackupStatus(IsOverdue = true)` → `IsUrgent`, `FromFrontDesk` avec listes overdue vides → non urgent) ;
libellés impératif/neutre selon le droit d'agir ; ordre de « À traiter » (urgents puis poids).

### 8.2 Vue autonome (`RaqmiSystem.Desktop/Views/HomeView`)

- `HomeView : UserControl`, contrat § 2.1 : `Initialize(ModuleViewContext)` sans réseau, `LoadAsync()` qui sort
  si le contexte est absent ou la session fermée, `ResetState()` à la déconnexion (retour à « Bonjour », signaux
  effacés, filtres remis). Placée **dans le contenu du TabItem 0** : aucune balise `<TabItem>` nouvelle, l'ordre
  des 31 onglets et le garde `tools/check-module-readiness.ps1` sont intacts ; `DocShots` capture toujours
  l'onglet 0.
- Entrées fournies par `MainWindow` à la construction : `IReadOnlyList<ModuleTile>` (les 50 tuiles existantes),
  `Action<int> navigate` (= `NavigateToModule` après `CanOpenModule`), `Func<IReadOnlySet<string>> grantedKeys`.
  Le XAML des blocs 2 à 4 de l'accueil actuel (~330 lignes) et la logique `InitializeModuleCatalog …
  RefreshModuleCatalogEmptyState` (~180 lignes de `MainWindow.Navigation.cs`) **déménagent** dans la vue sans
  changer de forme : mêmes `CollectionViewSource`, mêmes filtres, plus le filtre de maturité.
- `HomeSignalLoader` (Desktop) : pour chaque slot `Ready`, appelle la méthode `RaqmiApiClient` de la route,
  passe la réponse à `HomeSignalProjections`, écrit `ModuleTile.Signal` (nouvelle propriété `INotifyPropertyChanged`
  : `Loading / Value / Empty / Error / NeedsUnit / None`). Toutes les lectures d'une passe dans **un**
  `context.RunAsync`, séquentielles ; une exception par route est absorbée en `Error` sur la tuile et comptée pour
  le bandeau d'avertissement, sans interrompre la passe — les erreurs de session (401) remontent normalement.
- « À traiter » est un `ItemsControl` lié à une `CollectionViewSource` filtrée sur `Signal.IsWork`, triée
  `IsUrgent` puis `Weight` : pas de seconde collection.
- Points de contact `MainWindow` : `LoginButton_Click` (238-242) → `HomeView.OnSessionOpened(displayName)` puis
  `LoadAsync()` ; `LogoutButton_Click` (484-490) → `ResetState()` ; `MainTabs_SelectionChanged` vers l'onglet 0 →
  `HomeView.RefreshIfStaleAsync()` ; `MainWindow.Shortcuts.cs` `Ctrl+K` → `HomeView.FocusSearch()` (le champ
  garde le nom `HomeSearchTextBox` : `Ctrl+F` via `ShortcutRouter` le retrouve par balayage visuel) ; `F5` trouve
  `RefreshHomeSignalsButton`. `HomeGreetingTextBlock` / `HomeDateTextBlock` passent dans la vue ; les autres champs
  cités par `DocShots` (`MainTabs`, `MainContentGrid`, connexion) ne bougent pas.
- Gestionnaires câblés par XAML dans la vue : ils tolèrent d'être appelés avant l'initialisation (motif
  `moduleCatalogView is null`), comme aujourd'hui.

### 8.3 Autour de la vue

- `ModuleCatalogGroupHeaderTemplate` (thème) : icône + badge ; suppression du badge superposé sur la carte et du
  repli mort `MaturityBadgeFallback.*` / `EnsureMaturityBadgeStyles` ; `AutomationProperties.Name` et `HelpText`
  posés sur `ModuleCatalogCard` (« Ouvrir {Name}, {StatusLabel}, {Signal} » / « {Name}, accès non autorisé pour
  votre profil ») ; `AutomationProperties.HeadingLevel` sur les en-têtes de groupe.
- `DesktopSettings.UniteDuPoste` (chaîne, par poste, même schéma load-modify-write que `Apparence`) + champ dans
  `SettingsView` → *Poste de travail* (liste si `units.read`, saisie du code sinon). Confort, jamais une sécurité :
  la route reste gardée par sa politique.
- `RaqmiApiClient` : paramètre `userId` sur `GetAuditLogAsync` (pour *Mon activité*) ; rien d'autre n'est
  nécessaire aux seize signaux du registre.
- `HasModulePermission` / `ModuleViewContext.HasPermission` : résolution par `PermissionRegistry.AcceptedClaims`
  (le Domain est déjà référencé) pour qu'un rôle personnalisé porteur de clés cibles ne soit pas verrouillé à tort.
- Textes « 49 modules » → `ModuleCatalog.ExpectedTotal` ; `CaptureTarget` 0 renommé « Accueil - catalogue vivant ».
- `tools/RaqmiSystem.DocShots` : le délai fixe (2,5 s) suffit à une passe de signaux sur la base de démo ; option
  `--tabs 0` et `--users` multiples souhaitables pour capturer les sept accueils.

### 8.4 Lots de livraison

| Lot | Contenu | Réseau | Rupture |
|---|---|---|---|
| **A — Catalogue compact** | `HomeView` extraite, rail des domaines, ligne produit + détail, badge et icône dans l'en-tête de groupe, filtre de maturité, badge retiré de la carte, noms accessibles, textes « 49 » | aucun | visuelle seulement ; toutes les fonctions actuelles présentes |
| **B — Signaux** | `HomeComposer`, `HomeSignalProjections`, `HomeSignalLoader`, `ModuleTile.Signal`, ligne de signal, « À traiter », états, `RefreshHomeSignalsButton`, tests | ≤ 10 appels légers séquentiels par passe | aucune pour qui ignore la zone : le catalogue est en dessous |
| **C — Unité du poste et recherche universelle** | `DesktopSettings.UniteDuPoste` + réglage, signaux de portée unité, date métier du bandeau, résultats « sous-modules et écrans » branchés sur `NavigationTreeBuilder`, `userId` de l'audit | + 3 routes unitaires | — |

---

## 9. Accessibilité et clavier

- Ordre de tabulation : en-tête de fenêtre → (barre latérale repliée) → bandeau (liens compte, recherche) →
  « À traiter » (puces, Actualiser) → rail (groupe radio, flèches) → puces de filtre (groupes radio) → *Détail* →
  résultats → cartes dans l'ordre visuel → bandeau de session.
- Noms : cartes « Ouvrir {Name}, {StatusLabel}, {Signal} » ; puces « À traiter » « Ouvrir {Name} : {Signal}, en
  retard » ; rail « Domaine 06 PMS / Hébergement, 3 modules » ; en-têtes de groupe « Domaine …, n modules,
  {Maturité} » avec `HeadingLevel` ; squelettes « Chargement du compteur » ; le point d'urgence n'est jamais seul
  (texte + pastille *en retard*) ; `LiveSetting = Polite` sur la zone « À traiter » et le bandeau d'avertissement.
- Raccourcis inchangés : `Ctrl+K` recherche, `Échap` efface sans quitter le champ, `Entrée` ouvre le premier
  résultat ouvrable, `Alt+Origine` accueil, `Ctrl+PageUp/Down` inchangés, `F5` relit les signaux (message d'état
  « Cet écran n'a rien à actualiser » si aucun signal), `F1` liste.
- Focus clavier : anneau `FocusRingBrush` navy sur carte, puce et ligne ; `FocusRingOnFilledBrush` sur puce cochée ;
  la souris (bordure accent) et le clavier (navy) ne produisent jamais le même signal.
- Thème : aucune couleur nouvelle ; les pastilles d'urgence reprennent le couple `StatusSubmitted*` déjà renversé
  en sombre ; la maquette rend les deux thèmes.

---

## 10. Compromis et rupture maîtrisée

**Ce que perd l'utilisateur actuel**

- La grande carte « Architecture fonctionnelle » avec les noms des domaines en toutes lettres : remplacée par un
  rail d'icônes numérotées (nom en info-bulle et déployé sur la puce cochée). Coût d'apprentissage : les icônes
  des 22 domaines, déjà présentes dans la barre latérale.
- Le bandeau à quatre grands compteurs en haut de page : replié derrière *Détail*, résumé sur une ligne.
- Les en-têtes « Domaine → Module » : le module devient un libellé discret sur la carte.
- Le badge de maturité sur chaque carte (divergence avec la spec, corrigée).

**Ce qu'il gagne**

- Des cartes visibles sans défiler à la taille par défaut (≈ 395 px d'en-tête au lieu de ≈ 760).
- Ce qui l'attend, en tête, sans ouvrir dix écrans — et rien qu'il ne puisse déjà lire dans ces écrans.
- Une recherche qui trouve « Night audit » ou « Balance âgée » (sous-modules), pas seulement des titres de cartes.
- La maturité par domaine, le filtre de maturité, des noms accessibles sur les 50 cartes.

**Compromis assumés**

1. **Gel pendant la passe de signaux** : `RunAsync` désactive `MainTabs` ; une passe de 4 à 10 routes légères tient
   en une à deux secondes, la barre de progression le montre. Si c'est trop, la réponse est dans le contrat de vue
   (`quiet`), pas dans un `try/catch` maison.
2. **Coût serveur à l'ouverture de session** : `dec-cockpit`, `housekeeping/board` et `receivables/aging` sont
   « moyens ». Ils ne partent que pour les profils qui ouvrent ces cartes, une fois par ouverture d'accueil et au
   plus toutes les cinq minutes. Les routes lourdes (`kpis/dashboard`, `group-dashboard`, `tape-chart`) sont
   exclues du registre.
3. **Unité du poste** : sans elle, Réception et Caisse n'ont pas de signaux unitaires. C'est un réglage local à
   créer (lot C) ; en attendant, la carte le dit au lieu de deviner. Une affectation utilisateur ↔ unité côté
   serveur serait plus juste, mais elle n'existe pas (README décision 4).
4. **La zone « À traiter » n'est pas une boîte de réception** : elle ne liste que des compteurs de files existantes
   ; pas de tâches, pas de notifications. Le portail Mon Espace de la phase 4 pourra s'y greffer (une tuile par
   projection `MyWorkItem`), le registre des signaux étant déjà le bon contrat.
5. **`/pending` n'est appelé qu'avec `approvals.decide`** : cashier, hr.manager et reader ouvrent la carte
   *Validations* sans compteur, car la route leur renverrait 403. C'est la vérité des droits, pas une régression.
6. **Barre latérale toujours repliée sur l'accueil** (comportement de `SyncSidebarToTab`) : le rail et le catalogue
   sont le sommaire. La maquette la montre par défaut pour situer le shell, avec un commutateur vers le comportement
   actuel ; la recommandation est de garder le repli.

---

## 11. Questions ouvertes

1. **Gel de `MainTabs` pendant la passe** : accepter (une à deux secondes, barre de progression) ou ajouter une
   variante silencieuse à `ModuleViewContext.RunAsync` ?
2. **Cockpit DEC sur l'accueil** : signal « moyen-lourd » mais un seul appel et la meilleure file de travail pour
   direction / contrôle / unité — le garder dans le lot B ou le différer au clic ?
3. **Unité du poste** : réglage local seul (lot C) ou d'abord une affectation utilisateur ↔ unité serveur ?
4. **Libellé de la ligne de signal à zéro** : « Rien en attente de votre décision » (rassurant, une ligne de plus
   sur chaque carte) ou ligne vide (plus dense) ? La maquette montre la première option.
5. **Rail** : numéros seuls avec nom en info-bulle (option 6 de la spec du shell) ou nom court permanent sur les
   écrans larges ?
6. **Libellé « Accueil » → « Mon Espace »** sur `ShowHomeButton` (question 3 de la spec) : le catalogue vivant
   reste l'accueil ; le renommage n'en dépend pas.
