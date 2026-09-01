# Shell de navigation — spécification de design

> **Statut** : livrable DESIGN de la vague 1 (réorganisation fonctionnelle), version du 01/09/2026,
> **en attente de validation humaine** sur la maquette `docs/design/maquette-shell.html`.
>
> Ce document dit **comment le shell se présente et se comporte** ; il ne code rien. L'agent NAV
> l'intègre dans `MainWindow` avec les ressources livrées dans `Themes/RaqmiTheme.xaml` (icônes des
> 22 domaines, badges de maturité). Il étend la charte `docs/charte-ui-desktop.md` — sa section
> « Navigation à quatre niveaux » en est le résumé normatif — et ne la contredit sur aucun point.
>
> Références : `docs/reorganisation/03-cartographie-cible.md` (§ 3.4 navigation, § 3.5 profils,
> § 3.6 maturité), `04-mapping-existant-vers-cible.md` (§ 4.F onglets → chemins), le catalogue
> `src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.cs` et le code du shell
> actuel (`MainWindow.xaml`, `ModuleNavigationGroup.cs`, `ModuleTile.cs`).

---

## 0. Ce qui change, en une page

| Aujourd'hui (lot 0) | Cible vague 1 | Cible phase 4 |
|---|---|---|
| Barre latérale : sections = domaines cibles ayant au moins un écran, ordre `01`…`22`, 22 Administration Système épinglée en pied | **inchangé** ; en-tête de domaine avec **son icône propre** (22 clés) et un point de maturité ; état replié **mémorisé par poste** ; recherche étendue aux sous-modules | idem |
| Pas de fil d'Ariane ; le titre de l'écran est le seul repère | **Fil d'Ariane** `Domaine → Module → Sous-module → Écran` au-dessus de chaque vue, synchronisé avec les sous-onglets | idem |
| Sous-onglets nommés au gré de chaque vue | **Règle unique** : sous-onglets = sous-modules cibles quand la vue en couvre plusieurs ; écrans sinon | vues scindées quand un onglet change de domaine |
| Accueil = catalogue des 50 modules, filtre de domaine, bandeau à 4 statuts de module | Accueil = catalogue **groupé par domaine** (`01`…`22`), badge de maturité par domaine, bandeau à 4 statuts de module **et** 4 niveaux de maturité | Accueil = **portail Mon Espace** ; le catalogue devient un écran « Où en est le produit ? » |
| Pastille `Disponible / API prête / Partiel / Planifié` par module | idem **plus** badge `Planifié / Aperçu technique / Fonctionnel / Prêt pour la production` par domaine (calculé) | badge par écran (`ScreenNode`) |

Décision déjà validée et appliquée : **la barre latérale ne liste que les écrans ouvrables** par le
profil ; le sommaire complet, cadenas et modules planifiés compris, reste l'accueil.

---

## 1. Anatomie du shell

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│ En-tête (76 px, StructureBrush) : marque · session · thème · raccourcis (F1)  │
├──────────────┬────────────────────────────────────────────────────────────────┤
│ Barre        │ Fil d'Ariane (32 px)  Domaine › Module › Sous-module › Écran   │
│ latérale     ├────────────────────────────────────────────────────────────────┤
│ 248 px       │ Vue du module (UserControl) : en-tête d'écran, sous-onglets,    │
│ (CardBorder) │ cartes, grilles, états vides — inchangée                        │
│              │                                                                │
│ 01 Mon Espace│                                                                │
│ Recherche    │                                                                │
│ ▸ 03 Finance │                                                                │
│ ▾ 06 PMS     │                                                                │
│   · Écran    │                                                                │
│ …            │                                                                │
│ ───────────  │                                                                │
│ ▸ 22 Système ├────────────────────────────────────────────────────────────────┤
│ Session ●    │                                                                │
└──────────────┴────────────────────────────────────────────────────────────────┘
```

Quatre niveaux, une seule surface de navigation (la barre latérale, charte § 2.4), un seul repère
de position (le fil d'Ariane), un seul chemin de navigation (`NavigateToModule`, charte § 2.2).

---

## 2. Barre latérale

### 2.1 Contenu et ordre

- **Une section par domaine** de `FunctionalArchitectureCatalog`, dans l'ordre des identifiants
  `01`…`22`. L'ordre n'est plus éditorial (`SidebarLayout`, obsolète) : il est celui de la taxonomie,
  le même sur l'accueil, dans le fil d'Ariane et dans la documentation.
- **Seuls les écrans ouvrables** figurent dans une section : un écran existe (`TabIndex`) **et** le
  profil a sa permission de lecture. Un domaine sans écran ouvrable pour le profil **n'apparaît pas**
  (aucun en-tête vide, aucun cadenas). Le cadenas et le statut « Planifié » vivent sur l'accueil,
  qui répond à « où en est le produit ? » ; la barre latérale répond à « où est-ce que je travaille ? ».
- **Un écran, une ligne** : deux modules servis par le même onglet (Audit & contrôle interne /
  Journalisation, onglet 4) ne donnent qu'une ligne, dans le premier domaine de l'ordre (`15`).
- **Épinglé en pied**, sous un filet : `22 Administration Système`. On l'ouvre rarement, jamais
  dans le flux de la journée, mais toujours en un clic. `02 Administration & Socle ERP` reste dans
  la liste : c'est le socle fonctionnel (organisation, utilisateurs, référentiels), pas l'exploitation
  du serveur.
- **`01 Mon Espace` occupe la première ligne**, à la place du bouton « Accueil » actuel, avec son
  icône et le même comportement (`ShowHomeButton`, `Alt+Origine`). Aujourd'hui il ouvre l'accueil-
  catalogue ; en phase 4 il ouvre le portail (§ 5). Le libellé change de « Accueil » à « Mon Espace »
  dès la vague 1 pour que le vocabulaire soit stable avant le contenu.

### 2.2 En-tête de domaine

`ModuleNavGroupHeaderTemplate`, inchangé dans sa mécanique (ToggleButton pleine ligne, chevron,
compteur), enrichi de :

| Élément | Règle |
|---|---|
| Icône | `Path` 16 × 16, `ModuleGroupIcon.<IconKey>` du domaine, trait `TextMutedBrush` 1.4 (voir `icones-domaines.md`) |
| Libellé | nom du domaine **sans son numéro** (« PMS / Hébergement ») : le numéro sert au tri et à la documentation, pas à la lecture. L'info-bulle donne « 06 · PMS / Hébergement » |
| Compteur | nombre d'écrans ouvrables ; pendant une recherche, nombre de résultats |
| Point de maturité | `MaturityDot.<Niveau>` (7 px) à gauche du compteur, **seulement si le domaine n'est pas Fonctionnel** (Aperçu technique aujourd'hui : 07, 10, 12, 15, 21). Info-bulle : « Aperçu technique — noyau technique ou parcours incomplet ». Un domaine Fonctionnel ne porte rien : le silence est la norme, le point est l'exception |

### 2.3 Écran (ligne de section)

`ModuleNavSubButton`, inchangé : 34 px, retrait de 8 px, libellé du module historique (celui de la
carte de l'accueil, un seul vocabulaire). État actif `Tag="Active"` (filet accent 3 px, fond
`ModuleActiveBackgroundBrush`, texte semi-gras). Info-bulle = description du module.

### 2.4 Densité

| | Confortable | Compact |
|---|---|---|
| En-tête de domaine | 36 px | 32 px |
| Ligne d'écran | 34 px | 30 px |
| Marge entre sections | 2 px | 2 px |

La densité suit le réglage **Poste de travail → Densité** déjà en place pour les grilles (charte
§ 3.14) : mêmes deux valeurs, même portée (par poste), même mécanique (`{DynamicResource}` pour les
deux hauteurs, à ajouter à côté de `GridRowHeight`). Compact ne réduit pas le texte (12,5 px) : il
retire de l'air. Motif : avec 16 domaines ouvrables pour un administrateur aujourd'hui et 22 demain,
une barre latérale entièrement repliée mesure 16 × 38 = 608 px en confortable, 22 × 34 = 748 px en
compact — la seconde tient sans défiler sur un écran de 1080 lignes une fois l'en-tête, les marges et
le bandeau de session retirés, la première non.

### 2.5 État replié et mémorisation

Règles observées aujourd'hui, conservées :

1. Ouvrir un écran **déroule son domaine sans replier les autres** : la barre suit la navigation,
   elle ne défait pas ce que l'utilisateur a ouvert.
2. Pendant une recherche, les domaines qui ont un résultat sont déroulés, les autres disparaissent ;
   effacer la recherche **rend l'état antérieur**, plus le domaine de l'écran affiché.

Règle nouvelle :

3. L'ensemble des domaines déroulés est **mémorisé par poste** (`DesktopSettings`, à côté de
   `Apparence` et `Densite`), écrit à chaque changement, relu à la connexion. Par poste et non par
   compte, pour la même raison que le thème : une réception de nuit garde sa barre latérale quand
   l'équipe change. Valeur : la liste des identifiants de domaine (`"06","08"`). Absente ⇒ tout
   replié sauf le domaine de l'écran affiché.
4. À la première connexion d'un poste, **rien n'est déroulé** : un profil Réception voit quatre
   en-têtes, pas trente lignes.

### 2.6 Recherche

- Même champ, même moteur que l'accueil : `ModuleTile.SearchText` normalisé (sans accent ni casse),
  `Ctrl+K` y amène le focus, `Échap` efface, croix d'effacement `SearchClearButton`.
- **Étendue aux sous-modules et aux écrans** dès que le catalogue hiérarchique existe (lot 1.1) :
  « arrivées » doit trouver `06 → Front Office → Arrivées` et ouvrir `PmsView` **sur ce sous-onglet**.
  Tant que la navigation vers un sous-onglet n'existe pas, la ligne de résultat garde le libellé du
  module et une seconde ligne en `CaptionText` donne le chemin (« Front Office › Arrivées »).
- Sans résultat : message `SidebarSearchEmptyTextBlock` inchangé (« Aucun écran ne correspond.
  L'accueil liste les 50 modules du catalogue. » — corriger 49 → 50).
- Sans résultat **parce que le seul écran trouvé est verrouillé** : le message le dit
  (« “Paie” existe mais n'est pas ouvert à votre profil ») plutôt que de laisser croire que
  l'écran n'existe pas. C'est l'unique cas où un écran non ouvrable est nommé dans la barre latérale,
  et il l'est en texte, pas en ligne cliquable.

---

## 3. Fil d'Ariane

### 3.1 Placement et forme

- Une ligne de **32 px** en haut de la colonne de contenu, au-dessus du `UserControl` de la vue,
  hors de la vue (posée par `MainWindow`, jamais par les vues : elles ignorent leur position dans
  l'arbre, charte § 2.1). Masquée sur l'accueil / Mon Espace, qui est la racine.
- Séquence : `[icône du domaine 16 px] Domaine › Module › Sous-module › Écran`.
  Texte 12,5 px ; ancêtres en `TextSecondaryBrush` SemiBold et cliquables ; **dernier segment en
  `TextPrimaryBrush`**, non cliquable. Séparateur : chevron `M0.6,1.6 L4.5,5.5 L8.4,1.6` (celui de
  l'en-tête de domaine) tourné à −90°, `TextMutedBrush`, 9 × 7.
- Les segments absents sont omis, jamais remplacés par un tiret : `03 Finance › Budget › Plan
  budgétaire` a trois segments ; `20 Pilotage › Dashboards › Groupe` aussi.
- À droite, aligné sur la même ligne : le **badge de maturité du domaine** quand il est sous
  Fonctionnel (`MaturityBadge.TechnicalPreview`, libellé « Aperçu technique »). Rien sinon.

### 3.2 Comportement

| Segment | Clic | Clavier |
|---|---|---|
| Domaine | déroule le domaine dans la barre latérale et y met le focus ; en phase 4, ouvre la page du domaine | `Entrée` / `Espace` |
| Module | ouvre le premier écran ouvrable du module | idem |
| Sous-module | sélectionne le sous-onglet correspondant dans la vue | idem |
| Écran | — (segment courant) | non focusable |

Le fil d'Ariane **suit les sous-onglets** : changer d'onglet dans `AccountingView` réécrit les deux
derniers segments. Mécanique : la vue expose la sélection courante (`SelectedSection`, événement
`SectionChanged`) via `ModuleViewContext`, et la fenêtre projette le chemin depuis le catalogue ;
la vue ne connaît ni le fil d'Ariane ni son domaine.

Cas particulier des **onglets qui changent de domaine** (PmsView « Règles de vente » → `07 Revenue
→ Restrictions` ; ApprovalsView « Circuits » → `02 Administration → Paramétrage → Circuits`) : le fil
d'Ariane affiche le chemin **cible** — domaine, icône et badge compris — pendant que la barre
latérale garde en surbrillance l'écran par lequel on est entré. Le fil d'Ariane dit la vérité de la
taxonomie ; la barre latérale dit d'où l'on vient. La divergence est temporaire : ces onglets sont à
déplacer (§ 4.3).

### 3.3 Libellés

Les quatre segments viennent du catalogue hiérarchique (`DomainNode.Name`, `ModuleNode.Name`,
`SubmoduleNode.Name`, `ScreenNode.Name`), jamais du `Header` de l'onglet ni du titre de la vue.
Tant que le catalogue hiérarchique n'existe pas (lot 1.1), NAV peut alimenter le fil d'Ariane depuis
la table du § 4.4 de ce document, qui est le mapping validé.

---

## 4. Sous-navigation dans les vues

### 4.1 La règle unique

> **Quand un module a plusieurs sous-modules dans la même vue, les sous-onglets portent les noms
> des sous-modules cibles.** Quand la vue ne couvre qu'un sous-module, les sous-onglets portent les
> noms de ses écrans. Un onglet ne porte jamais un nom que la cartographie cible ne connaît pas.

Corollaires :

- **Deux niveaux d'onglets au plus** dans une vue : niveau 3 (sous-modules) en `SectionTabControl`
  (charte § 2.4, référence `TreasuryView`), niveau 4 (écrans) en `SectionTabControl` imbriqué
  (gabarit déjà en place dans `AccountingView`, onglet « Exercices, tiers et livres ») ou en puces
  `FilterChip` quand les écrans sont de simples filtres d'une même grille.
- L'**ordre** des onglets est l'ordre des sous-modules dans la cartographie cible, sauf quand
  l'ordre opératoire de la journée s'y oppose (Achats : Commandes → Réception → Fournisseurs) ; la
  dérogation est notée dans la vue.
- `Ctrl+Tab` / `Ctrl+Maj+Tab` circulent entre sous-onglets (réservé à cet usage, `MainWindow.xaml`),
  `Ctrl+Page haut/bas` entre modules.
- Le nom d'un sous-onglet est un **nom de sous-module**, pas une action : « Nouvelle réservation »
  n'est pas un onglet mais le bouton `New…Button` (Ctrl+N) de l'onglet « Réservations ».

### 4.2 Inventaire des sous-onglets actuels et correspondance

Relevé sur les 30 onglets de `MainTabs` (`MainWindow.xaml`) et les vues de `Views/` au 01/09/2026.
« Écrans » signifie que la vue ne couvre qu'un sous-module et que ses onglets sont de niveau 4.

| Vue (onglet) | Domaine → Module | Sous-onglets actuels | Sous-onglets cibles | Action |
|---|---|---|---|---|
| `AccountingView` (11) | 03 → Comptabilité | Plan comptable · Écritures · Balance générale · Exercices, tiers et livres ⟨Exercices / clôture · Tiers / balance auxiliaire · Grand livre · Lettrage⟩ | **Comptabilité générale** ⟨Plan comptable · Écritures⟩ · **Exercices** · **Comptabilité auxiliaire** ⟨Tiers · Lettrage · Balance auxiliaire⟩ · **États comptables** ⟨Balance générale · Grand livre · Journaux⟩ | réorganiser : 4 sous-modules au niveau 3, écrans au niveau 4 |
| `ApprovalsView` (16) | 01 → Mes validations · 02 → Paramétrage | En attente de ma décision · Circuits · Historique | **Mes validations** ⟨En attente · Historique⟩ ; **Circuits** (chemin 02 → Paramétrage → Circuits dans le fil d'Ariane) | garder ; scinder au lot 4.1 |
| `BudgetView` (12) | 03 → Budget | Plan budgétaire · Réalisé et écarts | écrans, inchangés | — |
| `CrmView` (23) | 04 → CRM | Vue 360 · Segments · Fidélité · Campagnes · Satisfaction · Contacts | **Fiche client 360°** · **Contacts et historique** · **Segmentation** · **Fidélité** · **Campagnes** · **Satisfaction et NPS** | renommer et réordonner |
| `HousekeepingView` (21) | 08 → Housekeeping | Tableau des chambres · Planning des équipes · Minibar | **États des chambres** · **Planning et affectations** · **Minibar** | renommer (inspections = États des chambres) |
| `HumanResourcesView` (22) | 13 → RH & paie | Collaborateurs · Temps & absences · Paie du mois · Référentiel | **Personnel** · **Temps et absences** · **Paie** · **Référentiel RH** | renommer ; « Congés » devient un onglet propre quand les soldes existent |
| `InventoryView` (24) | 11 → Stocks | Stock courant · Mouvements · Articles & magasins · Inventaires | inchangés (déjà les sous-modules cibles) | — |
| `KitchenView` (26) | 10 → Cuisine | Fiches techniques · Relevés HACCP · Points de contrôle | **Fiches techniques** · **Hygiène** ⟨Relevés HACCP · Points de contrôle⟩ | regrouper l'hygiène |
| `KpiView` (29) | 20 → KPI Engine / Analyse | Bibliothèque · Comparatif inter-unités · Alertes · Paramétrage | **Bibliothèque KPI** · **Analyse inter-unités** · **Alertes** · **Paramétrage** (`kpi.admin`) | renommer |
| `LodgingView` (15) | 06 → Réservations & folios | Réception · Nouvelle réservation · Planning · Folio · Chambres & occupation | **Réservations** (« Nouvelle réservation » = action Ctrl+N) · **Folios** · **Inventaire** · **Planning** | renommer ; doublon « Planning » avec `PmsView` à trancher (§ 9) |
| `PmsView` (30) | 06 → Front Office | Planning · Arrivées · Départs · Clients présents · Prévisionnel · Hors service · Règles de vente · Night audit | **Arrivées** · **Départs** · **Clients présents** · **Planning** · **Prévisionnel** · **Hors service** · **Night audit** ; **Règles de vente** → chemin `07 → Restrictions` | réordonner ; « Règles de vente » change de domaine au lot 2.5 |
| `PurchasingView` (25) | 12 → Achats | Bons de commande · Réception · Fournisseurs | **Commandes** · **Réception** · **Fournisseurs** | renommer |
| `ReceivablesView` (13) | 03 → Créances | Balance âgée · Relances · Risque client | écrans, inchangés | — |
| `TreasuryView` (6) | 03 → Trésorerie | Encaissements · Ordres de paiement · Comptes bancaires | écrans, inchangés | référence de la charte § 2.4 |

Vues **sans sous-onglets** (sections empilées ou écran unique) et leur chemin :

| Vue (onglet) | Chemin | Remarque |
|---|---|---|
| `MiceView` (28) | 09 → Groupes & événements | sections Salles · Événements · Devis · BEO · Allotements & rooming lists : candidate à trois sous-onglets **Groupes** (allotements, rooming lists) · **Salles** · **Événements** ⟨Devis · BEO⟩ quand la vue sera retouchée |
| `TariffsView` (14) | 07 → Tarification | Plans tarifaires · Périodes · Conventions · Test de résolution : un seul sous-module, sections conservées |
| `UsersView` (10) | 02 → Utilisateurs → Comptes | rôles dans le formulaire |
| `SettingsView` (9) | 02 → Paramétrage → Paramètres globaux | sections Établissement · Poste de travail · Santé du système |
| Unités (1) | 02 → Organisation → Unités | onglet historique de `MainWindow` |
| Recettes (2) | 03 → Recettes → CA journalier | idem |
| `CustomersView` (7) | 04 → Clients → Fichier clients | — |
| `InvoicesView` (8) | 05 → Factures → Factures | paiements dans le détail |
| `ClosingView` (5) | 06 → Contrôle → Clôture journalière | — |
| Tableau de bord (3) | 20 → Dashboards → Unité | — |
| `GroupDashboardView` (19) | 20 → Dashboards → Groupe | — |
| `DecCockpitView` (20) | 20 → Dashboards → Exploitation | — |
| `ReportsView` (17) | 20 → BI → Rapports | — |
| Journal d'audit (4) | 15 → Audit → Journal d'audit (alias 22 → Maintenance → Journal d'audit) | une ligne, dans 15 |
| `BackupView` (18) | 22 → Maintenance → Sauvegarde | — |
| `SyncView` (27) | 22 → Diagnostic → Postes | — |

### 4.3 Onglets à cheval sur deux domaines

Deux cas, tous deux transitoires, tous deux signalés par le fil d'Ariane (§ 3.2) :

- `PmsView` « Règles de vente » (restrictions, `lodging.*`) relève de `07 Revenue Management`.
- `ApprovalsView` « Circuits » relève de `02 Administration → Paramétrage`.

Aucun renommage ni déplacement d'onglet ne se fait dans la vague 1 : la règle est d'abord affichée
(fil d'Ariane), les vues sont retouchées lot par lot avec leurs tests.

### 4.4 Arbre de navigation transitoire (phase 1)

Tableau prêt à l'emploi pour le fil d'Ariane et la barre latérale tant que le catalogue
hiérarchique (lot 1.1) n'existe pas. Domaines sans écran : `14`, `16`, `17`, `18`, `19`, `21` —
absents de la barre latérale, présents sur l'accueil avec leur badge.

| Domaine | Module | Sous-module(s) | Écran (onglet) |
|---|---|---|---|
| 01 Mon Espace | Mes validations | En attente · Historique | Validations (16) |
| 02 Administration & Socle ERP | Organisation | Unités | Unités hôtelières (1) |
| | Utilisateurs | Comptes | Administration & utilisateurs (10) |
| | Paramétrage | Paramètres globaux · Circuits | Paramétrage global (9) · Validations (16, onglet Circuits) |
| 03 Finance & Comptabilité | Recettes | CA journalier | Recettes journalières (2) |
| | Trésorerie | Encaissements · Ordres de paiement · Comptes bancaires | Trésorerie (6) |
| | Comptabilité | Comptabilité générale · Exercices · Comptabilité auxiliaire · États comptables | Comptabilité SCF (11) |
| | Budget | Plan budgétaire · Réalisé et écarts | Budget & prévisions (12) |
| | Créances | Balance âgée · Relances · Risque client | Créances & recouvrement (13) |
| 04 Commercial, Clients & CRM | Clients | Fichier clients | Clients (7) |
| | CRM | Fiche 360° · Contacts · Segmentation · Fidélité · Campagnes · Satisfaction | CRM & expérience client (23) |
| 05 Facturation & Ventes | Factures | Factures | Facturation (8) |
| 06 PMS / Hébergement | Réservations & folios | Réservations · Folios · Inventaire · Planning | Hébergement & occupation (15) |
| | Front Office | Arrivées · Départs · Clients présents · Planning · Prévisionnel · Hors service · Night audit | PMS front office (30) |
| | Contrôle | Clôture journalière | Clôture journalière (5) |
| 07 Revenue Management & Distribution | Tarification | Plans tarifaires · Périodes · Conventions | Tarifs & conventions (14) |
| | Restrictions | Règles de vente | PMS front office (30, onglet Règles de vente) |
| 08 Housekeeping | Housekeeping | États des chambres · Planning et affectations · Minibar | Housekeeping & chambres (21) |
| 09 Groupes, MICE & Événementiel | Groupes & événements | Salles · Événements · Groupes | Groupes & MICE (28) |
| 10 F&B / Restauration | Cuisine | Fiches techniques · Hygiène | Cuisine & HACCP (26) |
| 11 Stocks & Économat | Stocks | Stock courant · Mouvements · Articles & magasins · Inventaires | Stocks & consommations (24) |
| 12 Achats & Fournisseurs | Achats | Commandes · Réception · Fournisseurs | Achats & approvisionnements (25) |
| 13 Ressources Humaines & Paie | RH & paie | Personnel · Temps et absences · Paie · Référentiel RH | RH & paie (22) |
| 15 Qualité, Audit & Contrôle interne | Audit | Journal d'audit | Journal d'audit (4) |
| 20 Pilotage, KPI & BI | Dashboards | Unité · Groupe · Exploitation | Tableau de bord (3) · Tableau de bord PDG (19) · Cockpit DEC (20) |
| | KPI Engine | Bibliothèque KPI · Analyse inter-unités · Alertes · Paramétrage | Bibliothèque KPI (29) |
| | BI | Rapports | Rapports (17) |
| 22 Administration Système | Maintenance | Sauvegarde | Sauvegarde (18) |
| | Diagnostic | Postes | Postes & erreurs (27) |

---

## 5. Accueil : catalogue aujourd'hui, portail demain

### 5.1 Accueil-catalogue (vague 1)

L'accueil reste « la réponse directe à “où en est le produit ?” » (charte § 4.3) et garde sa
structure : salutation, date, filtre de domaine (lot 0), bandeau d'avancement, recherche, puces de
statut et de priorité, cartes. Trois changements :

1. **Groupement par domaine** (`01`…`22`, `ModuleCatalogGroupHeaderTemplate`) à la place des onze
   familles historiques. L'en-tête de groupe reçoit l'icône du domaine et son **badge de maturité**
   (`MaturityBadge.<Niveau>`), à droite du compteur. Les domaines sans module (`19`) et les domaines
   dont tous les modules sont planifiés apparaissent quand même : c'est la promesse du catalogue.
2. **Bandeau d'avancement à deux lectures** : la barre segmentée et ses quatre compteurs de modules
   (Disponible / API prête / Partiel / Planifié) restent ; une seconde ligne, sous la légende, donne
   les **quatre compteurs de domaines par maturité** avec `MaturityDot.<Niveau>` : « 11 Fonctionnels ·
   5 Aperçus techniques · 6 Planifiés · 0 Prêt pour la production ». Les deux lectures ne s'additionnent
   pas et ne se remplacent pas : l'une compte des écrans, l'autre des engagements.
3. **Filtre de maturité** : une rangée de puces `FilterChipCompact` « Maturité : Toutes · Fonctionnel ·
   Aperçu technique · Planifié » se croise avec les puces de statut existantes.

La carte module (`ModuleCatalogCard`) ne change pas : sa pastille reste le **statut du module**.
Le badge de maturité est porté par le domaine, une seule fois, dans l'en-tête de groupe — deux
pastilles par carte diraient deux fois la même chose ou, pire, deux choses différentes.

### 5.2 Accueil-portail Mon Espace (phase 4)

Cible du lot 4.3 (`07-plan-migration.md`) : `01 Mon Espace` devient l'écran d'ouverture. Il
n'agrège que des projections autorisées et ne possède aucune donnée métier.

```text
┌ Bandeau ──────────────────────────────────────────────────────────────┐
│ Bonjour, Nadia                     Unité HTL-01 · Date métier 01/09  │
│ [Mon profil] [Mes préférences] [Ma sécurité]                          │
├ Tuiles (grille fluide, cartes CardBorder) ────────────────────────────┤
│ Mes tâches 4 │ Mes validations 2 │ Notifications 7 │ Mon activité     │
│ Mes demandes │ Mes délégations   │ Mes favoris      │ Mes documents    │
├ Raccourcis ──────────────────────────────────────────────────────────┤
│ Derniers écrans ouverts : Arrivées · Folios · Fiche client 360°       │
├ Produit ─────────────────────────────────────────────────────────────┤
│ « Où en est le produit ? » → catalogue des modules (écran dédié)       │
└───────────────────────────────────────────────────────────────────────┘
```

- **Bandeau** : salutation (`HomeGreetingText`), unité et date métier à droite (`HomeDateText`),
  trois boutons fantômes vers les sous-modules « Mon profil / Mes préférences / Ma sécurité » (les
  trois seuls partiellement couverts aujourd'hui).
- **Tuiles** : une par sous-module de `01` ; chaque tuile porte son compteur (serveur, charte § 3.10)
  et n'apparaît que si le sous-module est au moins `Functional` — une tuile « Mes tâches » sans
  service derrière serait un statut qui ment. Tant que rien n'est livré, le portail se réduit au
  bandeau, aux raccourcis et à la carte « Où en est le produit ? » : c'est **l'état vide du portail**,
  assumé (§ 7).
- **Le catalogue ne disparaît pas** : il devient un écran (`Catalogue des modules`) atteignable
  depuis la carte « Où en est le produit ? », depuis le pied de la barre latérale et par `Alt+Origine`
  suivi de `Ctrl+K`. La barre latérale reste masquée sur le portail comme sur l'accueil actuel
  (`SyncSidebarToTab`) : le portail est la racine, il n'a pas besoin de sommaire à côté.

---

## 6. Badges de maturité

### 6.1 Ressources livrées (`RaqmiTheme.xaml`)

| Niveau `FunctionalMaturity` | Libellé (source unique) | Style de badge | Style de point | Teinte | Contraste clair / sombre |
|---|---|---|---|---|---|
| `Planned` | Planifié | `MaturityBadge.Planned` | `MaturityDot.Planned` | ardoise (non engagé) | 5,52 / 6,98 |
| `TechnicalPreview` | Aperçu technique | `MaturityBadge.TechnicalPreview` | `MaturityDot.TechnicalPreview` | ambre (attention) | 5,02 / 7,96 |
| `Functional` | Fonctionnel | `MaturityBadge.Functional` | `MaturityDot.Functional` | teal de marque (en service) | 5,38 / 7,45 |
| `ProductionReady` | Prêt pour la production | `MaturityBadge.ProductionReady` | `MaturityDot.ProductionReady` | vert (accompli) | 5,21 / 7,57 |

Douze brushes (`Maturity<Niveau>Background/Foreground/AccentBrush`), définies en clair dans le thème
et en sombre dans `ThemePalette.Sombre` (fond sombre teinté, texte clair : le renversement de la
charte § 1.2). Le badge est un `Border` qui porte fond, coins, marges **et** la couleur du texte par
`TextElement.Foreground` : le `TextBlock` enfant hérite, aucun style à lui poser.

```xml
<Border Style="{StaticResource MaturityBadge.Functional}">
    <TextBlock Text="Fonctionnel" />
</Border>
```

Dans un gabarit piloté par les données, un `DataTrigger` sur la maturité change le `Style` du
`Border` nommé (`<Setter TargetName="badge" Property="Style" Value="{StaticResource
MaturityBadge.Functional}" />`), exactement comme la carte module bascule ses brushes sur
`ModuleStatus`.

### 6.2 Choix de teinte, et pourquoi

Le vert est **réservé au dernier niveau**. `Functional` prend le teal de marque : « livré, en
service », sans promettre l'homologation. Conséquence assumée : tant qu'aucun domaine n'est
`ProductionReady` (c'est le cas au 01/09/2026), le bandeau d'avancement ne montre **aucun vert** —
c'est exact, et c'est le but (charte § 4.3, un statut qui ment au dirigeant est un défaut grave).
L'ambre garde son sens d'attention (« Aperçu technique » : noyau technique, parcours incomplet),
l'ardoise son sens neutre (« Planifié »). Les quatre familles de teinte sont celles de la carte
d'avancement des modules ; les clés sont distinctes pour que les deux échelles puissent diverger.

### 6.3 Où le badge apparaît

| Surface | Forme | Condition |
|---|---|---|
| En-tête de domaine de l'accueil | badge complet | toujours |
| Bandeau d'avancement | point + compteur | toujours |
| En-tête de domaine de la barre latérale | point, info-bulle | seulement sous `Functional` |
| Fil d'Ariane | badge complet à droite | seulement sous `Functional` |
| Carte module | — | jamais (la carte porte le statut du module) |

### 6.4 Libellés

Une seule source, en C#, sur le modèle de `ModuleCatalog.StatusLabel` et de
`DailyRevenueStatusDisplay` (charte § 1.5, § 3.6) : `FunctionalMaturityDisplay.Label(maturity)` →
« Planifié », « Aperçu technique », « Fonctionnel », « Prêt pour la production ». Le même mot à
l'écran, dans l'info-bulle, dans le CSV du catalogue. À créer par NAV (hors périmètre DESIGN).

---

## 7. États vides

Le gabarit ne change pas (charte § 3.5) : pictogramme au trait + `EmptyStateTitleText` +
`EmptyStateHintText` qui **nomme l'action** qui remplira l'écran. Cinq situations propres au shell :

| Situation | Titre | Indice |
|---|---|---|
| Recherche sans résultat (barre latérale) | « Aucun écran ne correspond » | « L'accueil liste les 50 modules du catalogue. » |
| Recherche dont le seul résultat est verrouillé | « “Paie” n'est pas ouvert à votre profil » | « Demandez le droit `hr.read` à votre administrateur. » |
| Catalogue filtré sans résultat (accueil) | « Aucun module ne correspond à ce filtre » | « Élargissez le statut, la priorité ou le domaine. » |
| Domaine sans écran ouvrable (accueil, groupe entier verrouillé ou planifié) | l'en-tête de groupe reste, les cartes sont non ouvrables | info-bulle de la carte : `ModuleTile.ToolTipText` (« Accès non autorisé pour votre profil » ou note de statut) |
| Portail Mon Espace sans tuile (phase 4, rien de livré ou rien à traiter) | « Rien à traiter » | « Vos validations, tâches et notifications apparaîtront ici. » — et la carte « Où en est le produit ? » |

Un état vide n'est **jamais** une page blanche ni une boîte de dialogue ; il reste dans la surface
qu'il remplace, `IsHitTestVisible="False"`.

---

## 8. Accessibilité

Ce que le shell fait déjà et qui doit survivre à l'intégration, puis ce qu'il ajoute.

### 8.1 Acquis à préserver

- **`AutomationLabels.LinkPrecedingLabel`** : tout champ de saisie (recherche comprise) est nommé par
  le libellé qui le précède, ou par son `Tag` / info-bulle via `AccessibleNameConverter`
  (`Converters.cs`). La recherche de la barre latérale s'annonce « Rechercher un module ».
- **Raccourcis déclarés une fois** sur la fenêtre (`Window.InputBindings`), listés par `F1`
  (`ShortcutsWindow`) : `Ctrl+K` recherche, `Alt+Origine` accueil, `Ctrl+Page haut/bas` module
  précédent/suivant (les modules interdits sont sautés), `Échap` efface la recherche ; sur l'écran
  affiché : `F5`, `Ctrl+F`, `Ctrl+N`, `Ctrl+S` routés par `ShortcutRouter` sur le **nom** des
  contrôles (charte § 3.13).
- **En-tête de domaine = `ToggleButton`** : `Espace` et `Entrée` ouvrent et ferment, l'état est
  exposé par `IsChecked` (motif ExpandCollapse), le chevron ne fait que le refléter.
- **Anneau de focus** : `FocusRingBrush` sur surface claire, `FocusRingOnFilledBrush` sur puce
  active, `FocusRingOnDarkBrush` sur l'en-tête ; la souris (bordure accent) et le clavier (navy) ne
  produisent jamais le même signal.
- **La couleur n'est jamais seule** : le badge porte un mot, le point porte une info-bulle, l'état
  actif de la barre latérale porte un filet **et** un fond **et** une graisse.

### 8.2 Ajouts du shell à quatre niveaux

| Élément | `AutomationProperties.Name` | Notes |
|---|---|---|
| En-tête de domaine | « Domaine PMS / Hébergement, 3 écrans » (compteur inclus) | `HelpText` = maturité quand elle est sous `Functional` |
| Ligne d'écran | « Ouvrir Arrivées » | l'écran actif ajoute « , écran affiché » |
| Segment de fil d'Ariane | « Remonter à Front Office » | le segment courant est un `TextBlock`, pas un bouton |
| Badge de maturité | libellé du badge | `HelpText` = définition du niveau (`07-plan-migration.md`, modèle de readiness) |
| Point de maturité | « Aperçu technique » | l'`Ellipse` n'a pas de nom par défaut : le poser |
| Bouton `01 Mon Espace` | « Mon Espace, accueil » | `AccessKey` inchangée (`Alt+Origine`) |

Ordre de tabulation : en-tête de fenêtre → barre latérale (Mon Espace, recherche, en-têtes et
écrans dans l'ordre visuel, section épinglée) → fil d'Ariane → vue → bandeau de session. Les
en-têtes repliés ne donnent pas accès à leurs écrans au clavier (ils sont `Collapsed`), ce qui est
attendu : `Espace` les déroule.

Contraste : toutes les paires nouvelles sont ≥ 4,5:1 pour du texte (badges : 5,0 à 8,0:1) et ≥ 3:1
pour les points sur `SurfaceBrush` ; les points ne se posent pas directement sur `AppBackgroundBrush`
(2,8:1 en clair), comme les `ModuleProgress…` existants.

---

## 9. Ce que voient quatre profils (projection § 3.5 sur l'existant)

Domaines ouvrables = domaines ayant au moins un écran dont le profil a la permission de lecture.
Les autres n'apparaissent pas dans la barre latérale ; ils restent sur l'accueil, verrouillés ou
planifiés selon le cas.

| Profil (rôle actuel) | Barre latérale (ordre `01`…`22`) | Écran d'ouverture proposé | Ce que l'accueil montre en plus | Remarques |
|---|---|---|---|---|
| **Réception** (aucun rôle système ; `cashier` partiel — à créer en phase 2) | 01 Mon Espace · 04 Commercial, Clients & CRM (Clients, CRM) · 06 PMS / Hébergement (Réservations & folios, Front Office) · 08 Housekeeping (si `housekeeping.read`) · 05 Facturation (si `invoices.read`) | `06 → Front Office → Arrivées` | 03, 20 verrouillés ; 14, 16, 17, 18, 19 planifiés | 06 → Contrôle (clôture) absent sans `closing.read` ; `01` n'a rien à montrer sans `approvals.read` : la ligne reste, c'est l'accueil |
| **Directeur d'unité** (`unit.manager`) | 01 · 03 Finance (Recettes, Trésorerie, Budget — Comptabilité selon `accounting.read`) · 06 PMS (les trois modules) · 08 · 09 · 10 F&B · 11 Stocks · 12 Achats · 13 RH (lecture) · 15 Qualité (Journal d'audit) · 20 Pilotage (Unité, Exploitation, KPI, Rapports) · 22 (épinglée, si `maintenance.read`) | `20 → Dashboards → Unité` | 14 Maintenance planifié (son domaine, il attend l'écran) | `approvals.decide` alimente 01 → Mes validations |
| **Direction générale** (`direction`) | 01 · 03 Finance (tout, lecture + approbations) · 06 PMS (lecture) · 10 · 11 (lecture) · 13 RH · 15 Qualité · 20 Pilotage (Groupe, Unité, Exploitation, KPI, Rapports) | `20 → Dashboards → Groupe` | 16 Juridique planifié, mis en avant par le filtre de domaine | Pas de 02 ni de 22 : la DG ne paramètre pas |
| **Administrateur** (`system.administrator`) | tous les domaines ayant un écran : 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 15, 20 · 22 épinglée | `01 Mon Espace` (accueil-catalogue) | 14, 16, 17, 18, 19, 21 planifiés / aperçu technique, aucun cadenas | Seul profil pour qui la barre latérale approche les 22 : la densité compacte (§ 2.4) lui est destinée |

Le masquage est un confort d'interface, jamais une sécurité (charte § 3.9) : chaque route reste
protégée par sa politique, et un profil qui perd un droit en cours de session voit la ligne
disparaître au prochain `ApplyModulePermissions`.

---

## 10. Intégration par NAV — liste de contrôle

Ressources livrées dans le thème, prêtes à référencer :

- `ModuleGroupIcon.MonEspace`, `.Administration`, `.Commercial`, `.Facturation`, `.Hebergement`,
  `.Revenue`, `.Housekeeping`, `.Evenementiel`, `.Restauration`, `.Stocks`, `.Maintenance`,
  `.Qualite`, `.Marina`, `.Parking`, `.Integrations` (nouvelles) ; `.Finance`, `.Achats`,
  `.RessourcesHumaines`, `.Juridique`, `.Documentaire`, `.Pilotage`, `.Systeme` (existantes) —
  à poser dans `FunctionalArchitectureCatalog.IconKey`.
- `MaturityBadge`, `MaturityBadge.Planned`, `.TechnicalPreview`, `.Functional`, `.ProductionReady`
  (`Border`) ; `MaturityDot` et ses quatre variantes (`Ellipse`) ; douze brushes `Maturity…`.

Reste à coder (hors périmètre DESIGN) :

1. `FunctionalMaturityDisplay.Label` (libellés, § 6.4).
2. En-tête de domaine : icône du domaine, point de maturité conditionnel, `AutomationProperties`
   (§ 2.2, § 8.2).
3. Persistance des domaines déroulés dans `DesktopSettings` (§ 2.5) ; densité de la barre latérale
   sur `ApparenceDensite` (§ 2.4).
4. Contrôle de fil d'Ariane dans `MainWindow`, alimenté par le tableau § 4.4 puis par le catalogue
   hiérarchique ; exposition de la section courante par les vues à sous-onglets (§ 3.2).
5. Accueil : groupement par domaine, badge dans `ModuleCatalogGroupHeaderTemplate`, seconde ligne
   du bandeau, puces de maturité (§ 5.1) ; libellé « Accueil » → « Mon Espace » (§ 2.1).
6. Message de recherche 49 → 50 modules (§ 2.6).

---

## 11. Questions ouvertes (à trancher sur la maquette)

1. **Teal pour Fonctionnel, vert réservé à Prêt pour la production** (§ 6.2) — ou vert dès
   Fonctionnel, au risque d'un bandeau qui ne distingue plus les deux derniers niveaux ?
2. **Point de maturité dans la barre latérale seulement sous Fonctionnel** (§ 2.2) — ou sur tous
   les domaines, au prix de seize points identiques ?
3. **« Mon Espace » en première ligne dès la vague 1**, alors que le portail n'arrive qu'en phase 4
   (§ 2.1) — ou garder « Accueil » jusqu'à la livraison du portail ?
4. **Doublon « Planning »** entre `LodgingView` et `PmsView` (§ 4.2) : lequel disparaît ?
5. **`02 Administration & Socle ERP` dans la liste, `22` seule épinglée** (§ 2.1) — ou les deux en
   pied, comme la section « Administration » de l'ancien `SidebarLayout` ?
6. **Numéro de domaine masqué dans la barre latérale** (§ 2.2), visible dans l'info-bulle et sur
   l'accueil — ou affiché partout ?
