# Exploration 2 — « Cockpit » : l'accueil comme poste de pilotage

> Concept indépendant de refonte de l'onglet 0 (« Accueil ») du client WPF.
> Angle imposé : **pilotage**. KPI autorisés du jour et de la veille, files de décision,
> alertes du moteur KPI, comparaison d'unités ; chaque tuile ouvre l'écran qui porte la
> décision. Sobre, dense, pensé pour un directeur d'unité ou une direction — mais composé
> à partir des permissions, donc utilisable par tous les profils.
>
> Maquette : `docs/design/accueil/explorations/2-cockpit/maquette.html` (autonome, deux
> thèmes, sept profils, états visibles). Références : `docs/charte-ui-desktop.md` (loi du
> projet), `docs/design/navigation-shell.md` (shell), `docs/reorganisation/03-cartographie-cible.md`
> (domaine 01), `src/RaqmiSystem.Infrastructure/Security/SecuritySeeder.cs` (droits réels).
>
> Branche de référence : `reorg/phase-1` (HEAD `e7dcaad`). Aucun autre fichier du dépôt n'est
> modifié par cette exploration.

---

## 1. Intention

### 1.1 Le parti pris en une phrase

**L'accueil cesse d'être un sommaire du logiciel pour devenir le tableau de bord de la
journée : ce que valent J et J-1, ce qui attend une décision, ce qui sort des seuils, et
l'écran où l'on agit — en un clic.**

Aujourd'hui l'onglet 0 est un catalogue de 50 cartes précédé de 760 px de filtres : à
1240 × 760, aucune carte n'est visible sans défiler, et rien de ce qui s'affiche ne vient du
serveur. Un directeur qui ouvre Raqmi le matin veut savoir si les recettes d'hier sont
validées, quelle unité n'a pas clôturé, combien d'ordres de paiement attendent sa signature,
et si un indicateur a franchi un seuil. Toutes ces réponses existent déjà dans l'API
(`/pilotage/dec-cockpit`, `/revenue/daily/dashboard`, `/approvals/instances/pending`,
`/kpis/alerts`, `/lodging/front-desk`…) ; elles sont simplement dispersées dans quinze écrans.

Le Cockpit les rassemble sur une seule surface, sans en inventer aucune.

### 1.2 Ce que le Cockpit est — et n'est pas

| Le Cockpit est | Le Cockpit n'est pas |
|---|---|
| Une **synthèse à une valeur par instrument**, chaque instrument renvoyant à son écran source. | Un nouvel écran métier : aucune grille éditable, aucune action d'écriture depuis l'accueil. |
| Une **lecture J / J-1** côte à côte, chiffres du serveur, sans pourcentage calculé côté client. | Un moteur de calcul : toute variation, tendance ou verdict affiché vient d'une réponse API (`Trend`, `Health`, `PreviousVariancePercent`, `Variations`). |
| Un **composeur par permissions** : les instruments apparaissent parce qu'une clé de lecture les autorise, jamais parce qu'un rôle est codé en dur. | Une sécurité : le masquage reste un confort ; chaque route garde sa politique serveur. |
| Un accueil **utile pour tous** : un profil sans donnée d'exploitation garde ses files, sa carte Mon Espace et l'état du produit. | Une page qui promet : ce qui n'existe pas côté serveur (notifications, tâches transverses, agenda, favoris, messagerie) n'apparaît que comme nœud « Planifié » avec son badge. |
| Une **entrée** vers le Cockpit DEC (onglet 20), la Bibliothèque KPI (29), les Tableaux de bord (3, 19) — qui restent les écrans détaillés. | Un doublon du Cockpit DEC : le Cockpit d'accueil n'affiche qu'un compteur par file et ouvre l'onglet 20 pour le détail. |

### 1.3 Pour qui, en priorité

1. **Direction générale** et **Administrateur** : vue groupe, toutes unités, files de décision, alertes KPI, sauvegardes et postes.
2. **Directeur d'unité** : même cockpit, l'unité de travail sélectionnée en tête, instruments PMS et housekeeping de cette unité, files qu'il traite lui-même (clôture, rejets, réceptions, validations).
3. **Lecture seule** (contrôle, audit interne) : les mêmes instruments, sans verbe d'action.
4. **Réception, Caisse, RH** : un cockpit court — l'unité du poste, les compteurs de leur journée, leurs files — jamais vide.

---

## 2. Règles d'honnêteté du Cockpit

Ces règles sont la contrepartie de l'angle « pilotage » : un cockpit qui ment est pire qu'un
catalogue qui ne dit rien (charte § 4.3 : « un statut qui ment au dirigeant est un défaut grave »).

1. **Tout chiffre affiché est renvoyé par l'API** (charte § 3.10). Les montants (`GrandTotal`,
   `PendingValidationAmount`, `Total.Total`, `OutstandingBalance`…) et les compteurs serveur
   (`PendingValidationCount`, `UnitsMissing`, `TotalCount`, `AnswerCount`…) sont affichés tels
   quels, formatés `N2` / `N0` en culture courante.
2. **Compter des lignes est une présentation ; additionner des montants est interdit.**
   « 7 articles sous minimum » (= `LowStockRow[].Count`) ou « 4 absences en attente » sont
   acceptables : ce sont des lignes que le serveur a déjà sélectionnées. Aucune somme de
   `Amount` n'est faite côté client ; si le serveur ne renvoie pas le total, la tuile n'affiche
   que le nombre de lignes.
3. **Aucun pourcentage calculé côté client.** J et J-1 sont affichés côte à côte
   (« 3 412 600,00 · veille 4 060 900,00 »). Une flèche de tendance ou une variation en % n'apparaît
   que si le serveur la renvoie (`KpiMeasureResponse.Trend` / `PreviousVariancePercent`,
   `GroupDashboardResponse.Variations`). Le verdict de couleur ne vient que de `KpiHealth`
   ou de `KpiAlertSeverity`, jamais d'une comparaison locale.
4. **Le lourd est à la demande.** `/kpis/dashboard` et `/pilotage/group-dashboard` recalculent
   toute la bibliothèque ou toute la période : ils ne sont jamais appelés à l'ouverture.
   La bande « KPI moteur » s'affiche avec un bouton « Charger » et le coût annoncé.
5. **Une tuile n'existe que si sa route existe et si la clé de lecture est accordée.**
   Le registre des instruments (§ 4) est la seule source ; il cite pour chaque instrument la
   route, la permission et l'écran cible, et un test le verrouille.
6. **La fraîcheur est affichée.** « Cockpit calculé à 08:42 » dans le bandeau ; pas de
   rafraîchissement automatique (le client est monothread, aucun `Timer` : doctrine du
   battement de poste) ; F5 recharge.
7. **Ce qui n'est pas de moi est dit.** Un directeur d'unité voit les files de tout le groupe
   (aucune affectation utilisateur↔unité n'existe, décision 4 du README) : la tuile le dit
   (« toutes unités ») et l'unité de travail est surlignée, pas filtrée.

---

## 3. Structure

### 3.1 Wireframe (1240 × 760, barre latérale repliée comme aujourd'hui sur l'onglet 0)

```
┌──────────────────────────────────────────────────────────────────────────────────────────┐
│ EN-TÊTE 76 px  Raqmi System                        D. Hamdani · direction  [Thème] [F1]   │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ ┌ BANDEAU DE COMMANDE (CardBorder) ────────────────────────────────────────────────────┐ │
│ │ Bonjour, Djamel Hamdani                 Périmètre [Toutes les unités ▾] Journée [02/09]│ │
│ │ mardi 2 septembre 2026 · Raqmi Hôtels · DZD · direction        [Actualiser F5]         │ │
│ │ Cockpit calculé à 08:42 · HTL-01 date métier 02/09 · en retard : HTL-04 (30/08, 3 j)   │ │
│ └───────────────────────────────────────────────────────────────────────────────────────┘ │
│ INSTRUMENTS                                                                               │
│ ┌─────────┐┌─────────┐┌─────────┐┌─────────┐┌─────────┐┌─────────┐┌─────────┐            │
│ │Recettes ││Encaiss. ││Créances ││Stocks < ││Absences ││NPS 30 j ││Sauveg.  │  ← Button   │
│ │3 412 600││2 184 350││18 642 300│ 7 art.  ││   4     ││  +42    ││ 6 h     │    par tuile│
│ │veille   ││veille   ││> 90 j   ││3 magas. ││août     ││118 rép. ││1,2 Go   │            │
│ └─────────┘└─────────┘└─────────┘└─────────┘└─────────┘└─────────┘└─────────┘            │
│ À DÉCIDER                                                                                 │
│ [3 Mes validations →] [2 Recettes à valider · 1 216 300 →] [3 j Retard de clôture →]       │
│ [1 Recette rejetée →] [2 OP à approuver · 2 640 000 →] [1 Inventaire à valider →]         │
│ ┌ ALERTES ET SIGNAUX (5/12) ──────────┐ ┌ UNITÉS (7/12) ─────────────────────────────┐   │
│ │ ● Critique  Taux d'occupation HTL-04│ │ Unité      Recette J-1   Clôt. Occupation ● │   │
│ │   52 % < 60 % · unit.manager        │ │ HTL-01  1 215 300 Validée Oui  96/123 78 %  │   │
│ │ ● À surveiller  Food cost HTL-03    │ │ HTL-02    742 100 Soumise Oui  54/84  64 %  │   │
│ │ ● Signal  HTL-04 sans recette J-1   │ │ HTL-03  1 604 800 Validée Oui 141/155 91 %  │   │
│ │ [Ouvrir les alertes KPI]            │ │ HTL-04         —     —   Non   46/88  52 % ●│   │
│ └─────────────────────────────────────┘ └─────────────────────────────────────────────┘   │
│ KPI MOTEUR — mois en cours vs N-1        [Charger (calcule toute la bibliothèque, ~2 s)]   │
│ ┌ OÙ EN EST LE PRODUIT ? ───────────────┐ ┌ MON ESPACE ────────────────────────────────┐  │
│ │ 31 dispo · 0 API · 0 partiel · 19 plan.│ │ Djamel Hamdani · direction · d.hamdani@…    │  │
│ │ ▓▓▓▓▓▓▓▓▓▓▓▓░░░░░  [Catalogue (50)]    │ │ [Mon profil] [Mes préférences] [Ma sécurité]│  │
│ └────────────────────────────────────────┘ │ Mon activité → Journal d'audit              │  │
│                                            │ Notifications · Tâches · Agenda  ⟨Planifié⟩ │  │
│                                            └─────────────────────────────────────────────┘  │
│ ▸ Catalogue des modules · 50  (replié ; Ctrl+K l'ouvre et place le curseur dans la recherche) │
├──────────────────────────────────────────────────────────────────────────────────────────┤
│ Session ● Prêt. Connecté en tant que Djamel Hamdani.                                       │
└──────────────────────────────────────────────────────────────────────────────────────────┘
```

À 760 px de haut, le bandeau (≈ 96 px), la rangée d'instruments (≈ 108 px) et la rangée
« À décider » (≈ 64 px) tiennent dans le premier écran avec le haut des cartes Alertes /
Unités : **l'utilisateur voit des données avant de défiler**, là où il voyait des filtres.

### 3.2 Bandeau de commande

| | |
|---|---|
| **Contenu** | Salutation `HomeGreetingText` (« Bonjour, {DisplayName} », alimentée par la réponse de login comme aujourd'hui) ; date locale `HomeDateText` ; établissement et devise (`GET /api/v1/settings` → `CompanyName`, `CurrencyLabel`) ; rôles de la session (`LoginResponse.User.Roles`) ; fraîcheur « Cockpit calculé à HH:mm » (`LiveSetting=Polite`) ; date métier de l'unité de travail (`GET /lodging/business-date` → `BusinessDate`, `IsLate`, `PendingDays`) ; retard de clôture le plus ancien (`DecCockpitResponse.OldestClosingDelay`). |
| **Commandes** | `CockpitUnitComboBox` « Périmètre » : « Toutes les unités » + liste `GET /organization/hotel-units` (units.read) ; à défaut, l'**unité du poste** (§ 4.3). `CockpitDatePicker` « Journée » (défaut `DateTime.Today`, J-1 = veille de cette date ; passe explicitement `date=` aux routes qui défaut au jour UTC). `RefreshCockpitButton` « Actualiser » — nommé `Refresh*` pour que **F5** le trouve (`ShortcutRouter`). |
| **Permissions** | Salutation et date : aucune. Établissement : settings.read (tous les rôles seedés l'ont). Unités : units.read. Date métier : lodging.read + une unité. Retard : dashboard.read. Chaque fragment se retire silencieusement s'il n'est pas autorisé ; le bandeau ne dépend d'aucun appel. |
| **États** | Avant chargement : « Cockpit en cours de calcul… » ; erreur d'un fragment : le fragment disparaît, le message va au bandeau de session (`SetStatus`). |
| **Action** | Changer de périmètre ou de journée relance le chargement (même chemin que F5). |

### 3.3 Instruments

Tuiles `Button` (gabarit dérivé de `ModuleCatalogCard`, hauteur 100 px confortable / 84 px
compact) : icône du domaine `ModuleGroupIcon.<clé>` 16 px + libellé `MetricLabelText`,
valeur `HomeStatValueText` 27 (tri-valeurs en `MetricValueText` 18), légende `CaptionText`
(« veille … », « n/m unités saisies »), pastille de statut si le serveur en fournit un.
Le clic ouvre l'écran source par `NavigateToModule`. Info-bulle = route, permission, heure de calcul.

Registre des instruments (ordre d'affichage = ordre du registre, filtré par permission) :

| # | Instrument | Valeur affichée (serveur) | Route | Permission (clé historique acceptée) | Périmètre | Charge | Écran cible | Existe |
|---|---|---|---|---|---|---|---|---|
| 1 | **Recettes J / J-1** | `GrandTotal` ; légende `UnitsWithEntry`/`TotalUnits`, `UnitsPendingValidation` ; second appel `date=J-1` | `GET /revenue/daily/dashboard?date=` | dashboard.read | global | légère ×2 | 3 Tableau de bord | Oui |
| 1b | **Recettes J** (profil sans dashboard.read) | `Total`, `EntryCount`, `DraftCount`, `SubmittedCount` | `GET /revenue/daily/summary?from=J&to=J[&hotelUnitCode]` | revenue.read | global ou unité | légère | 2 Recettes journalières | Oui |
| 2 | **Encaissements J / J-1** | `GrandTotal` ; légende `ConfirmedCount`, `CashTotal`, `CardTotal` | `GET /treasury/receipts/summary?from&to[&hotelUnitCode]` | treasury.read | global ou unité | légère ×2 | 6 Trésorerie | Oui |
| 3 | **Créances** | `Total.Total` ; légende `Total.Over90`, `Customers.Count` clients | `GET /receivables/aging` | receivables.read | global | moyenne | 13 Créances | Oui |
| 4 | **Front office** (tri-valeur) | `Arrivals.Count` · `Departures.Count` · `InHouseCount` ; légende `OverdueArrivals.Count` en retard | `GET /lodging/front-desk?hotelUnitCode&date` | lodging.read | **unité** | moyenne | 30 PMS front office | Oui |
| 5 | **Occupation** | `Occupancy.OccupiedRooms`/`TotalActiveRooms`, `OccupancyRatePercent` (même réponse que 4) | idem | lodging.read | **unité** | — | 15 Hébergement & occupation | Oui |
| 6 | **Chambres** (tri-valeur) | `DirtyRooms` · `CleanRooms` · `OutOfOrderRooms` ; légende `PendingTasks`, `AwaitingInspectionTasks` | `GET /housekeeping/board?hotelUnitCode&date` | housekeeping.read | **unité** | moyenne | 21 Housekeeping | Oui |
| 7 | **Événements du jour** | lignes renvoyées ; légende premier `Title` · `FunctionSpaceLabel` | `GET /mice/events?from=J&to=J[&hotelUnitCode]` | mice.read | global ou unité | moyenne | 28 Groupes & MICE | Oui |
| 8 | **Stocks sous minimum** | lignes renvoyées | `GET /inventory/low-stock` | inventory.read | global | légère | 24 Stocks | Oui |
| 9 | **Absences à approuver** | lignes renvoyées (`status=Pending`) | `GET /hr/absences?status=Pending` | hr.read | global | légère | 22 RH & paie | Oui |
| 10 | **Paie** | dernière `PayrollPeriodResponse` : `Period`, `Status`, `DraftPayslipCount` | `GET /hr/payroll/periods` | hr.read | global | légère | 22 RH & paie | Oui |
| 11 | **NPS 30 jours** | `Nps`, `AnswerCount` | `GET /crm/satisfaction/nps?from=J-30&to=J[&hotelUnitCode]` | crm.read | global ou unité | légère | 23 CRM | Oui |
| 12 | **Journal 24 h** | `TotalCount` | `GET /audit?from=J-1&to=J&pageSize=1` | audit.read | global | légère | 4 Journal d'audit | Oui |
| 13 | **Sauvegarde** | `AgeHours`, `LastBackup.SizeBytes`, `IsOverdue` (pastille) | `GET /maintenance/backups/status` | maintenance.read | global | légère | 18 Sauvegarde | Oui |
| 14 | **Postes** | lignes `Workstations`, dont `Freshness` ≠ frais (compte de lignes) | `GET /sync/stations` | sync.read | global | légère | 27 Postes & erreurs | Oui |
| 15 | **Comptes** | lignes renvoyées | `GET /security/users` | users.read | global | légère | 10 Administration & utilisateurs | Oui |

Instruments **à la demande** (bande « KPI moteur », § 3.6) : `REVENUE_TOTAL`, `OCCUPANCY_RATE`,
`ADR`, `REVPAR`, `EBITDA`, `RECEIVABLES_TOTAL` via `GET /kpis/dashboard` (dashboard.read,
lourd) — avec `Trend`, `Health`, `PreviousVariancePercent`, `Quality`, `MissingData`,
`HiddenByPermission` renvoyés par le serveur.

**États d'une tuile** : *squelette* (chargement, `SurfaceSubtle`, `AutomationProperties.Name`
« Chargement de {libellé} ») ; *valeur* ; *sans unité* (« — », légende « Unité du poste non
définie · Paramétrage → Poste de travail ») ; *indisponible* (appel en erreur : pastille
`StatusRejected` « Indisponible », info-bulle avec le message, lien « Réessayer ») ; *non
autorisé* (403 inattendu : pastille « Non autorisé » — ne devrait jamais s'afficher puisque la
composition suit les permissions, mais le masquage n'est pas une sécurité) ; *zéro* (« 0 » est
une information, la tuile reste). Aucune tuile ne se cache parce qu'elle vaut zéro.

### 3.4 « À décider » — les files

Rangée de compteurs-boutons (valeur `MetricValueText` 18 + libellé + montant serveur + flèche),
chaque file ouvrant l'écran où l'on tranche. Les verbes sont ceux de l'écran cible ; pour un
profil qui ne peut pas agir (Lecture seule, ou directeur d'unité devant une recette à valider
par la DEC), le libellé devient un constat (« Recettes soumises, en attente DEC ») et la rangée
s'intitule « À suivre ».

| File | Compteur (serveur) | Route | Permission d'affichage | Écran cible |
|---|---|---|---|---|
| Mes validations | `instances.Count` | `GET /approvals/instances/pending` (filtre serveur par rôle) | approvals.decide | 16 Validations |
| Validations (lien sans chiffre) | — | aucun appel (403 sinon) | approvals.read sans decide | 16 Validations |
| Recettes à valider | `PendingValidationCount`, `PendingValidationAmount` | `GET /pilotage/dec-cockpit?date=` | dashboard.read | 20 Cockpit DEC |
| Retard de clôture | `ClosingBacklogDayCount`, `OldestClosingDelay` | idem | dashboard.read | 5 Clôture (closing.read) sinon 20 |
| Recettes rejetées | `RejectedCount` | idem | dashboard.read | 2 Recettes (revenue.read) sinon 20 |
| OP à approuver | `PendingPaymentOrderCount`, `PendingPaymentOrderAmount` | idem | dashboard.read | 6 Trésorerie (treasury.read) sinon 20 |
| OP à régler | lignes `status=Approved` | `GET /treasury/payment-orders?status=Approved` | treasury.read | 6 Trésorerie |
| Recettes en brouillon | `DraftCount` | `GET /revenue/daily/summary?from=J&to=J` | revenue.read (sans dashboard.read) | 2 Recettes |
| Commandes à réceptionner | lignes `CanReceive` | `GET /purchasing/orders?status=Approved` | purchasing.read | 25 Achats |
| Inventaires ouverts | lignes hors clôturé | `GET /inventory/counts?status=` | inventory.read | 24 Stocks |
| Absences en attente | lignes `status=Pending` | `GET /hr/absences?status=Pending` | hr.read | 22 RH & paie |
| Bulletins en brouillon | `DraftPayslipCount` | `GET /hr/payroll/periods` | hr.read | 22 RH & paie |
| Arrivées non assignées | `UnassignedCount` | `GET /lodging/arrivals?hotelUnitCode&date` | lodging.read + unité | 30 PMS front office |
| Chambres à préparer | `RoomsToPrepare`, `NotReadyCount` | idem | lodging.read + unité | 21 Housekeeping (housekeeping.read) sinon 30 |
| Départs avec solde | `PendingCount`, `OutstandingBalance` | `GET /lodging/departures?hotelUnitCode&date` | lodging.read + unité | 30 PMS front office |

**État vide** : quand toutes les files valent zéro, la rangée reste (les zéros sont visibles)
et une ligne `EmptyStateHintText` dit « Rien n'attend votre décision. » Quand aucune file n'est
autorisée, la rangée disparaît (pas de titre orphelin).

### 3.5 Alertes et signaux

Carte gauche (5/12). Liste `ListBox` de lignes : pastille de sévérité (`Critical` →
`DangerBrush`, `Watch` → `StatusSubmittedForeground`, signal → `TextMutedBrush`), nom du KPI,
unité, message serveur, `OwnerRole`, période. Chaque ligne est un bouton « Ouvrir » vers
29 Bibliothèque KPI (sous-onglet Alertes).

- **Alertes du moteur** : `GET /kpis/alerts?from=J-1&to=J[&unitId]` (dashboard.read ; **méthode
  cliente à ajouter**, la route existe et ne renvoie que les alertes — plus légère que `/dashboard`).
- **Signaux** (pas des alertes KPI, dits comme tels) : unité `NeedsAttention` du cockpit DEC ;
  `IsLate` de la date métier ; `IsOverdue` de la sauvegarde ; postes `Freshness` hors ligne.
  Chacun ouvre son écran source.
- **État vide** : « Aucune alerte sur la période » / « Les seuils se règlent dans Bibliothèque KPI
  → Paramétrage (kpi.admin). » Pictogramme au trait, `IsHitTestVisible=False`.
- **Sans dashboard.read** : la carte n'apparaît que si un signal est possible (lodging.read +
  unité → date métier ; maintenance.read → sauvegarde) ; sinon elle disparaît.

### 3.6 Unités — comparaison

Carte droite (7/12), `DataGrid` (donc sensible à `GridRowHeight` : la densité s'applique) :
Unité · Recette J-1 (`YesterdayRevenueTotal` + pastille `YesterdayRevenueStatus`, « provisoire »
si `YesterdayRevenueIsProvisional`) · Clôture J-1 (`YesterdayClosed`) · Occupation J
(`OccupiedRooms`/`ActiveRooms`, `OccupancyRatePercent`) · Attention (`NeedsAttention`, pastille +
nom accessible). Source : `DecCockpitResponse.UnitHealth` (dashboard.read). Tri : `NeedsAttention`
d'abord, puis ordre serveur — un tri de présentation, pas un calcul. L'unité de travail est
surlignée (`RowHover`), jamais filtrée (§ 2.7). Clic sur une ligne : « Ouvrir {unité} » →
3 Tableau de bord (la sélection d'unité n'est pas transmise à l'écran cible aujourd'hui ;
extension future de `NavigateRequested(tab, hotelUnitCode)`).
**État vide** : « Aucune unité active » / « Créez ou activez des unités hôtelières pour suivre
leur santé quotidienne ici. » (même texte que le Cockpit DEC : un seul vocabulaire).

### 3.7 KPI moteur — à la demande

Bande repliée sous Alertes/Unités : « KPI moteur — mois en cours vs N-1 » + bouton
`LoadKpiEngineButton` « Charger (calcule toute la bibliothèque, ~2 s) ». Au chargement :
six tuiles `KpiMeasureResponse` (`ShortName`, `Value` + `Unit`, `PreviousVariancePercent` avec
flèche `Trend`, pastille `Health`, badge « Partiel » si `Quality ≠ Valid` avec `MissingData` en
info-bulle) + mention « n indicateurs masqués par vos permissions » (`HiddenByPermission`) et
« calculé à » (`CalculatedAt`). Clic → 29 Bibliothèque KPI. Permission : dashboard.read.
Jamais appelé automatiquement ; une préférence de poste « Charger les KPI à l'ouverture » est
envisageable plus tard, pas dans la vague 1.

### 3.8 Pied : « Où en est le produit ? » et « Mon Espace »

- **Où en est le produit ?** (`CardBorder`, compact) : les 4 compteurs de statut du catalogue
  (`ModuleCatalog.CountOf`, `HomeStatDot` + `HomeStatLabelText` + `HomeStatValueText`), la barre
  segmentée `HomeProgressSegment`, et le bouton `SecondaryButton` « Catalogue des modules (50) »
  qui **déplie la section catalogue** en bas de l'onglet 0. Donnée statique client, comme
  aujourd'hui — annoncée comme telle (« état du produit, source : catalogue du client »).
- **Mon Espace** (`CardBorder`) : `DisplayName`, rôles, courriel (réponse de login et
  `GET /api/v1/me`) ; trois `GhostButton` « Mon profil » et « Mes préférences » (→ 9 Paramétrage,
  settings.read) et « Ma sécurité » (→ dialogue de changement de mot de passe, événement
  `SecurityRequested` traité par `MainWindow`) ; lien « Mon activité → Journal d'audit » si
  audit.read ; lien « Validations » si approvals.read. En dessous, **une seule ligne atténuée**
  « Notifications · Tâches · Agenda · Favoris · Messagerie » avec le badge `MaturityBadge.Planned`
  et l'info-bulle « Planifié — phase 4, aucun service serveur aujourd'hui » : les nœuds de
  l'arbre fonctionnel du domaine 01 sont nommés, jamais rendus cliquables.

### 3.9 Catalogue replié

Le bloc catalogue actuel (puces de domaine, bandeau d'avancement, recherche `HomeSearchTextBox`,
puces de statut et de priorité, `ModuleCatalogItemsControl` des 50 `ModuleCatalogCard`, état
vide) est **conservé tel quel** dans un `Expander` « Catalogue des modules · 50 » en pied de
l'onglet 0, replié par défaut. Les 50 cartes restent atteignables au même endroit ; **Ctrl+K
déplie la section et place le curseur dans `HomeSearchTextBox`** (la commande existante cible
déjà ce champ) ; Ctrl+F le trouve par nom (`ShortcutRouter`). La carte « Où en est le produit ? »
et le pied de la barre latérale y mènent aussi. Rien de la mécanique des filtres ne change.

---

## 4. Composition par permissions

### 4.1 Un composeur pur, dans `RaqmiSystem.Application`

```
namespace RaqmiSystem.Application.Pilotage.Cockpit;

public sealed record CockpitInstrument(
    string Id, string Label, string ReadPermissionKey, CockpitScope Scope,
    CockpitLoad Load, int TargetTabIndex, string SourceRoute, string DomainIconKey);

public sealed record CockpitLayout(
    IReadOnlyList<CockpitInstrument> Instruments,
    IReadOnlyList<CockpitQueue> Queues,
    bool ShowAlerts, bool ShowUnits, bool ShowKpiEngine,
    IReadOnlyList<CockpitLink> WorkspaceLinks);

public static class CockpitComposer
{
    public static CockpitLayout Compose(Func<string, bool> hasPermission, bool hasUnitScope);
}
```

- `CockpitRegistry` (statique) déclare les instruments, files et liens du § 3 avec leur clé de
  lecture **historique** (`PermissionCatalog.DashboardRead`, `LodgingRead`…) — les mêmes que
  `ApplyModuleAccess` — et la route citée en chaîne pour la documentation et les tests.
- `Compose` filtre par `hasPermission`, retire les instruments de périmètre *unité* quand aucune
  unité n'est connue (ils passent en état « sans unité » plutôt que de disparaître, pour que
  l'utilisateur sache quoi configurer), résout l'écran cible de repli (ex. « Retard de clôture »
  → 5 si closing.read, sinon 20), et calcule `ShowAlerts`/`ShowUnits`/`ShowKpiEngine`.
- **Aucun rôle codé en dur.** Un rôle personnalisé porteur de `lodging.read` obtient le même
  cockpit que la Réception de démonstration.
- `hasPermission` est celui de `ModuleViewContext.HasPermission` ; recommandation (déjà notée
  dans la cartographie) : le faire passer par `PermissionRegistry.AcceptedClaims` pour que les
  clés cibles (`lodging.front_office.read`) soient acceptées comme les historiques.

### 4.2 Tests (sans WPF)

`tests/RaqmiSystem.Tests` ne référence pas Desktop : le composeur vit dans Application et se
teste comme `NavigationTreeBuilder` :

- par jeu de clés : `Only("lodging.read")` → instruments 4-5 en état « sans unité », aucune file
  de décision, `ShowUnits=false` ; `Only("hr.read","approvals.read")` → instruments 9-10, files
  RH, lien Validations sans compteur ; `NoPermission` → cockpit réduit au pied (jamais vide) ;
- par rôle réel : `SecuritySeeder` sur SQLite in-memory (`SecuritySeederTests.CreateSeededContextAsync`)
  → un test par rôle système qui fige la liste d'instruments (un ajout de clé au seeder fait
  changer le test, c'est voulu) ;
- registre : chaque `SourceRoute` correspond à un `MapGet` existant portant
  `RequireAuthorization(<ReadPermissionKey ou cible couverte>)` — le même contrôle que
  `tools/check-module-readiness.ps1` fait pour les écrans, appliqué aux instruments.

### 4.3 L'unité de travail

Les routes `/lodging/*` et `/housekeeping/*` exigent `hotelUnitCode`. Trois sources, dans l'ordre :

1. **Sélection dans le bandeau** (`CockpitUnitComboBox`, liste `GET /organization/hotel-units`,
   units.read) — mémorisée en session ; par défaut « Toutes les unités » pour un profil groupe.
2. **Unité du poste** : nouvelle clé `UniteDuPoste` dans `DesktopSettings` (même schéma que
   `Apparence`/`Densite`, par poste, jamais par compte), saisie dans Paramétrage → Poste de
   travail. C'est ce qui rend le cockpit utile à un poste de Caisse (cashier n'a **pas**
   units.read d'après le seeder) ou de Réception. Confort de poste, pas périmètre de sécurité :
   la route vérifie toujours lodging.read.
3. **Aucune** : les instruments unité affichent « Unité du poste non définie » avec la marche à
   suivre. Ils ne disparaissent pas : l'utilisateur doit comprendre pourquoi son cockpit est court.

---

## 5. Variantes par profil

Droits = `SecuritySeeder` (pas les projections documentaires). Le nom du profil ne pilote rien :
seules les clés comptent.

| Profil (rôle) | Instruments | À décider / À suivre | Alertes & signaux | Unités | KPI moteur | Mon Espace |
|---|---|---|---|---|---|---|
| **Réception** (rôle personnalisé à créer : lodging.read + checkin/reserve/checkout/room_move, customers.read, crm.read, housekeeping.read, settings.read ; unité du poste HTL-01) | Front office, Occupation, Chambres, NPS 30 j (unité) | Arrivées non assignées, Chambres à préparer, Départs avec solde | Signal « date métier en retard » seulement | — | — | Profil, préférences, sécurité. Ni validations ni audit. |
| **Directeur d'unité** (unit.manager ; unité de travail choisie dans la liste) | Recettes J/J-1 (toutes unités, dit), Front office, Occupation, Chambres, Événements, Stocks < min., NPS | Mes validations (decide), Recettes soumises en attente DEC (constat), Retard de clôture (→ 5), Recettes rejetées (→ 2), Commandes à réceptionner, Inventaires ouverts | Alertes KPI `unitId` + signaux | Oui, unité surlignée | Oui, à la demande | Profil, préférences, sécurité, validations. Pas d'audit (audit.read absent). |
| **Direction générale** (direction) | Recettes, Encaissements, Créances, Stocks, Absences, Paie, NPS, Événements, Journal 24 h, Sauvegarde, Postes ; Front office/Occupation/Chambres dès qu'une unité est choisie | Mes validations, Recettes à valider, Retard de clôture, Rejets, OP à approuver, Inventaires à valider, Commandes à approuver, Absences en attente | Alertes KPI groupe + signaux (DEC, sauvegarde, postes) | Oui | Oui | Tout, y compris Mon activité. |
| **Caisse** (cashier ; sans units.read, unité du poste non définie dans la maquette) | Encaissements J/J-1, Recettes J (résumé), Front office et Chambres en état « unité du poste non définie » | OP à régler, Recettes en brouillon, lien Validations sans chiffre | — | — | — | Profil, préférences, sécurité. |
| **RH** (hr.manager) | Absences à approuver, Paie, Effectif (lignes `/hr/employees`) | Absences en attente (état vide « Rien à approuver » dans la maquette), Bulletins en brouillon, lien Validations | — | — | — | Profil, préférences, sécurité. |
| **Administrateur** (system.administrator) | Direction + Comptes | Direction | Direction | Oui | Oui | Tout. |
| **Lecture seule** (reader) | Recettes J/J-1, Créances, Stocks, NPS, Événements ; unité → Front office, Occupation, Chambres | Rangée « À suivre » : mêmes compteurs, verbes de constat, lien Validations sans chiffre | Alertes KPI (état vide « Aucune alerte » dans la maquette) | Oui | Oui, à la demande | Profil, préférences, sécurité. Pas de trésorerie, audit, RH. |

Un profil qui n'aurait que settings.read voit : bandeau, une ligne « Votre profil n'ouvre aucun
instrument de pilotage » (`EmptyStateTitleText`), la carte produit et la carte Mon Espace.
**Jamais une page blanche.**

---

## 6. États, chargement, erreurs

- **Chargement séquentiel par instrument** : `LoadAsync` enchaîne un `context.RunAsync` **par
  instrument** dans l'ordre du registre (bandeau → files → instruments → alertes → unités).
  `RunAsync` désactive `MainTabs` le temps de chaque appel ; le cockpit reste visible et se
  remplit au fil de l'eau, la barre latérale reste active. Une erreur n'interrompt pas la
  séquence : l'instrument passe en « Indisponible », `RunAsync` a déjà écrit le message dans le
  bandeau de session. Coût accepté : la barre de progression clignote entre deux appels.
- **Deux paliers** : palier 1 à l'ouverture de session (dec-cockpit, revenue dashboard J et J-1,
  approvals pending, kpis/alerts, business-date + front-desk de l'unité : ≤ 7 appels légers à
  moyens) ; palier 2 immédiatement après, tuiles en squelette pendant ce temps (receipts,
  aging, low-stock, absences, nps, events, backups, stations, audit) ; palier 3 à la demande
  (KPI moteur). Un profil court (Réception, RH) ne déclenche que 3 à 5 appels.
- **Quand** : à la connexion, après `ApplyModulePermissions` et `NavigateToModule(0)` ; à F5 ;
  au changement de périmètre ou de journée. Pas au retour sur l'onglet 0 (la fraîcheur est
  affichée, l'utilisateur décide).
- **Erreur globale** (API injoignable) : le bandeau de session porte l'erreur, chaque tuile est
  « Indisponible » avec « Réessayer », le pied (produit, Mon Espace) reste utilisable.
- **Hors session** : le cockpit n'est pas visible (`MainContentGrid` masqué) ; `ResetState()`
  vide toutes les valeurs à la déconnexion.
- **Message d'état plutôt que boîte de dialogue** (charte § 3.12) : aucune `MessageBox`.

---

## 7. Tokens et composants

Tout est `StaticResource` du thème ; **aucune nouvelle brush** n'est nécessaire (donc rien à
ajouter à `ThemePalette.Sombre`) — une seule ressource de taille est proposée.

| Usage | Ressource |
|---|---|
| Cartes | `CardBorder` (bandeau, alertes, unités, pied), `SubtleCardBorder` (bande KPI moteur repliée, ligne « Planifié ») |
| Textes | `HomeGreetingText`, `HomeDateText`, `SubtitleText`, `HomeSectionLabel` (titres de rangées, `HeadingLevel`), `MetricLabelText`, `HomeStatValueText` 27, `MetricValueText` 18, `CaptionText` 11,5, `EmptyStateTitleText` / `EmptyStateHintText` |
| Tuile instrument | dérivé de `ModuleCatalogCard` (survol : bordure `AccentBrush`, ombre 0,07 → 0,20, translation −2 px ; focus : anneau `FocusRingBrush` ; désactivé : `SurfaceSubtle`) ; icône `ModuleGroupIcon.<clé du domaine>` via `ModuleGroupIconConverter`, `ModuleCardIcon` |
| Pastilles | `StatusValidated*` (Validée, Favorable), `StatusSubmitted*` (Soumise, À surveiller, Sauvegarde en retard), `StatusRejected*` (Rejetée, Critique, Indisponible), `StatusDraft*` (Brouillon, Partiel) ; `MaturityBadge.Planned` pour les nœuds planifiés |
| Sévérité | pastille 8 px `DangerBrush` (Critical), `StatusSubmittedForeground` (Watch), `TextMutedBrush` (signal) — jamais de couleur seule : le mot est toujours à côté |
| Produit | `HomeStatDot`, `HomeStatLabelText`, `HomeStatValueText`, `HomeProgressSegment`, `ModuleProgress*`, `ModuleStatus*` |
| Boutons | `SecondaryButton` (Actualiser, Charger, Catalogue), `GhostButton` (Profil, Préférences, Sécurité, Réessayer, Ouvrir les alertes), `SearchClearButton` inchangé dans le catalogue |
| Champs | `ComboBox` implicite (Périmètre, nommé par `Tag`), `DatePicker` implicite (Journée) |
| Grille | `DataGrid` implicite (Unités : `GridRowHeight`, `RowAlt`, `RowHover`, `AmountCellText` à droite) |
| Avertissement | `WarningBanner` promu de `TreasuryView` vers le thème : « Vous voyez toutes les unités : aucune affectation utilisateur↔unité n'existe. » pour le directeur d'unité ; « Périmètre : unité du poste, définie localement. » pour la Caisse |
| Mouvement | `HomeRevealStyle` / `Delayed` / `DelayedMore` sur bandeau, instruments, reste ; fondu 150 ms inchangé au changement d'onglet |
| Densité | proposition : `CockpitTileHeight` en `DynamicResource` (100 / 84), réglée par `ThemeManager.AppliquerDensite` à côté de `GridRowHeight` ; la grille Unités suit déjà `GridRowHeight` |

Règles tenues : `AccentBrush` ne porte jamais de texte ; SemiBold, jamais Bold ; le vert reste
réservé à Validé / Prêt pour la production ; montants `N2` alignés à droite ; heures locales.

---

## 8. Accessibilité et clavier

- Chaque tuile est un `Button` **avec** `AutomationProperties.Name` = « {Libellé} : {valeur}
  ; veille {valeur}. Ouvrir {écran} » et `HelpText` = route et heure de calcul (correction de
  la dette de `ModuleCatalogCard` sans nom accessible).
- Titres de rangées : `AutomationProperties.HeadingLevel=Level2`.
- Fraîcheur du bandeau : `LiveSetting=Polite` (« Cockpit actualisé à 08:42 ») ; squelettes
  nommés « Chargement de … ».
- Files : boutons nommés « {n} {libellé}, ouvrir {écran} » ; alertes : lignes nommées
  « {sévérité}, {KPI}, {unité}, {message} » ; unités : lignes « Ouvrir {unité}, recette d'hier
  {statut}, occupation {taux} » ; pastilles d'attention nommées.
- Ordre de tabulation : bandeau (Périmètre, Journée, Actualiser) → instruments → files →
  alertes → unités → KPI moteur → pied → catalogue. Aucun `TabIndex` explicite : l'ordre du
  XAML suffit.
- Raccourcis inchangés : **F5** → `RefreshCockpitButton` ; **Ctrl+K** → déplie le catalogue et
  focalise `HomeSearchTextBox` ; **Ctrl+F** → idem par `ShortcutRouter` ; **Alt+Origine** →
  onglet 0 ; Ctrl+PageDown/PageUp inchangés ; **Ctrl+S / Ctrl+N** → « Cet écran n'a rien à
  enregistrer / créer. » (pas de bouton `Save*`/`New*` dans le cockpit, volontairement).
- Champs nommés par `Tag` (`AccessibleNameConverter`) ; libellés reliés par
  `AutomationLabels.LinkPrecedingLabel`.

---

## 9. Densité et thème

- **Sobre et dense** : bandeau 96 px, tuiles 100 px (84 compact), files 56 px, tout en
  `CaptionText`/`MetricLabelText` hors valeurs. À 1240 px : 6 instruments par rangée (176 px
  min) ; à 1920 px : 9. Barre latérale repliée sur l'onglet 0 comme aujourd'hui
  (`SyncSidebarToTab` inchangé) : chaque tuile est déjà un lien, 248 px valent une colonne
  d'instruments. La maquette offre le commutateur « déployée » pour comparaison.
- **Thème** : tout en `StaticResource`, palettes clair et sombre déjà couvertes ; un cockpit
  ouvert à la connexion est construit dans le thème appliqué au démarrage (`RedemarrageConseille`
  inchangé).

---

## 10. Découpage WPF

### 10.1 Fichiers

| Fichier | Rôle |
|---|---|
| `src/RaqmiSystem.Application/Pilotage/Cockpit/CockpitRegistry.cs`, `CockpitComposer.cs`, `CockpitLayout.cs` | Registre, composeur pur, modèles (§ 4). |
| `src/RaqmiSystem.Desktop/Views/CockpitView.xaml(.cs)` | `UserControl` du contrat de vue (charte § 2.1) : `Initialize(ModuleViewContext)` (compose la mise en page, sans réseau), `LoadAsync()` (sort si contexte absent ou `!ApiClient.IsAuthenticated` ; séquence § 6), `ResetState()`. Événements : `NavigateRequested(int tabIndex)` (motif `DecCockpitView`), `SecurityRequested()`, `CatalogRequested()`. Contrôles nommés : `RefreshCockpitButton`, `CockpitUnitComboBox`, `CockpitDatePicker`, `LoadKpiEngineButton`, `CockpitAlertsListBox`, `CockpitUnitsDataGrid`. Méthode `SetSession(displayName, roles, email)` appelée par `MainWindow` à la connexion. |
| `src/RaqmiSystem.Desktop/Views/CockpitTile.cs` | Modèle d'affichage d'une tuile (`INotifyPropertyChanged` : `State`, `Value`, `Caption`, `Pill`, `TabIndex`, `AccessibleName`) — comme `ModuleTile`, sans dépendance WPF. |
| `src/RaqmiSystem.Desktop/MainWindow.xaml` (onglet 0 seulement) | Contenu du `TabItem` 0 = `ScrollViewer` → `StackPanel` [ `views:CockpitView x:Name="CockpitView"` ; `Expander x:Name="CatalogExpander"` contenant **le XAML actuel** des blocs 2-4 ]. `HomeGreetingTextBlock`/`HomeDateTextBlock` passent dans la vue. Le `TabItem` 0 reste la première balise, sans `x:Name` ; aucune balise `<TabItem>` ajoutée. |
| `src/RaqmiSystem.Desktop/MainWindow.xaml.cs` | Trois points de contact : `LoginButton_Click` (→ `CockpitView.SetSession(...)` puis `await CockpitView.LoadAsync()` après `NavigateToModule(0)`), `LogoutButton_Click` (→ `CockpitView.ResetState()`), `InitializeModuleViews` (→ `CockpitView.Initialize(context)`, abonnement aux événements). `RefreshHomeDate` migre dans la vue. |
| `src/RaqmiSystem.Desktop/MainWindow.Navigation.cs` | `CockpitView_NavigateRequested` = garde `CanOpenModule` + `NavigateToModule` (copie de `DecCockpitView_NavigateRequested`) ; `CatalogRequested` → `CatalogExpander.IsExpanded = true` + focus recherche. Rien d'autre ne bouge : `ApplyModulePermissions` et ses 28 `ApplyModuleAccess` restent textuels. |
| `src/RaqmiSystem.Desktop/MainWindow.Shortcuts.cs` | Ctrl+K sur l'onglet 0 : déplier `CatalogExpander` avant `HomeSearchTextBox.Focus()`. |
| `src/RaqmiSystem.Desktop/Api/RaqmiApiClient.Kpi.cs` | **Ajout** `GetKpiAlertsAsync(from, to, unitId)` → `IReadOnlyCollection<KpiAlertResponse>` (route existante). |
| `src/RaqmiSystem.Desktop/Api/RaqmiApiClient.cs` | **Ajout** du paramètre `userId` optionnel à `GetAudit...` n'est pas requis par le cockpit (le compteur 24 h est global) ; utile pour « Mon activité » plus tard. |
| `src/RaqmiSystem.Desktop/DesktopSettings.cs` | **Ajout** `UniteDuPoste` (chaîne, par poste) + `LoadUniteDuPoste/SaveUniteDuPoste` ; saisie dans `SettingsView` (onglet Poste de travail). |
| `src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` | Style `CockpitTile` (dérivé de `ModuleCatalogCard`), `CockpitQueueButton`, `CockpitTileHeight` ; promotion de `WarningBanner`. Aucune brush. |
| `tests/RaqmiSystem.Tests/CockpitComposerTests.cs` | § 4.2. |
| `tools/RaqmiSystem.DocShots/CaptureTarget.cs` | Libellé de la cible 0 : « Accueil - cockpit » (et « 50 modules » à la place de 49). Le cockpit reste capturable : contenu stabilisé après `LoadAsync`, aucune fenêtre modale ; DocShots attend déjà l'inactivité du dispatcher. |

### 10.2 Ce qui ne change pas (contraintes tenues)

- L'ordre des 31 `TabItem`, leurs `x:Name`, les appels `ApplyModuleAccess(PermissionCatalog.X, XTabItem)` :
  `tools/check-module-readiness.ps1` passe sans modification.
- `ModuleCatalog.cs` : intact ; `ExpectedTotal = 50`, `ExpectedAvailable = 31`.
- Les 50 cartes : même `ModuleCatalogItemsControl`, même filtres, même onglet 0.
- Un seul chemin de navigation (`NavigateToModule`), un seul vocabulaire (libellés de l'arbre pour
  les écrans cibles).
- Aucune règle métier : le cockpit affiche, trie pour lire, et ouvre.

### 10.3 Vagues

1. **Vague A (sans serveur)** : composeur + tests ; `CockpitView` avec bandeau, instruments et
   files sur les méthodes clientes existantes ; catalogue replié ; unité du poste.
2. **Vague B** : `GetKpiAlertsAsync`, carte Alertes & signaux, carte Unités, bande KPI moteur.
3. **Vague C** : `WarningBanner` dans le thème, `CockpitTileHeight` densité, « Mon activité »
   avec `userId`, transmission de l'unité à l'écran cible.

---

## 11. Ce que perd et gagne l'utilisateur de l'accueil actuel

| Perd | Gagne |
|---|---|
| Le catalogue en première position : les 23 puces de domaine, le bandeau d'avancement à 4 compteurs et les 50 cartes descendent dans une section repliée. | Des données serveur dès l'ouverture, avant tout défilement — là où il ne voyait que des filtres. |
| Le parcours « je découvre le produit par ses domaines » comme geste par défaut. | Le même parcours en un clic ou Ctrl+K, inchangé dans sa mécanique. |
| Un accueil instantané (aucun appel) : le cockpit fait 3 à 12 appels légers à la connexion. | Un accueil qui répond à « où en sommes-nous ? » et mène directement à l'écran de décision. |
| La cascade d'apparition sur du contenu statique. | La même cascade, sur des instruments qui se remplissent au fil de l'eau. |
| Rien pour Réception, Caisse, RH (le catalogue leur montrait surtout des cadenas). | Un cockpit court avec leurs compteurs du jour et leurs files. |

Pour l'administrateur, le catalogue reste à deux gestes (carte produit ou Ctrl+K) ; la maquette
du shell prévoit de toute façon qu'il « devienne un écran, jamais supprimé ».

---

## 12. Compromis, risques, questions ouvertes

1. **Temps d'ouverture de session.** Jusqu'à 12 appels pour une direction (dec-cockpit et aging
   sont moyens). Mitigation : paliers, squelettes, aucun appel lourd, profils courts = peu
   d'appels. À mesurer sur PostgreSQL réel avant de fixer la liste du palier 1.
2. **`RunAsync` gèle `MainTabs`** pendant chaque appel : le cockpit n'est pas cliquable pendant
   son remplissage (quelques secondes). Alternative écartée : un `RunAsync` unique qui masquerait
   les erreurs partielles.
3. **Recouvrement avec le Cockpit DEC (onglet 20)** : assumé — l'accueil n'affiche qu'un compteur
   par file et ouvre l'onglet 20 ; les libellés sont les mêmes (« Recettes à valider », « Santé du
   jour par unité ») pour un seul vocabulaire.
4. **Périmètre d'unité** : aucune affectation utilisateur↔unité côté serveur (décision 4) ; le
   cockpit le dit et surligne. L'unité du poste est un réglage local — à valider comme confort de
   poste (question ouverte Q1).
5. **Barre latérale repliée** sur le cockpit (comme aujourd'hui) : parti pris pour la densité ;
   Q2 — la déployer à partir de 1450 px ? La maquette montre les deux.
6. **Alertes KPI sans seuils** : la carte sera souvent vide tant que `kpi.admin` n'a pas réglé de
   seuils ; l'état vide le dit et pointe l'écran de paramétrage.
7. **Salutation et date** migrent de `MainWindow` vers la vue : trois lignes de `MainWindow.xaml.cs`
   à toucher ; aucune référence dans DocShots ni les tests.
8. **Instruments « compte de lignes »** (stocks, absences, événements, comptes) : présentation
   assumée, jamais un montant. Q3 — la direction souhaite-t-elle des totaux serveur pour ces
   files (nouvelles routes `/summary`) ? Hors périmètre de cette exploration.
9. **`HasModulePermission` compare les clés brutes** : un rôle personnalisé n'ayant que des clés
   cibles verrait un cockpit vide alors que l'API l'accepterait ; recommandation d'adopter
   `PermissionRegistry.AcceptedClaims` côté client (déjà notée dans la cartographie).
10. **Aucune notification, tâche, agenda, favori** : nommés une fois, badge « Planifié », jamais
    cliquables. Le jour où `/notifications` existera, ce sera un instrument de plus dans le
    registre — pas une refonte.
