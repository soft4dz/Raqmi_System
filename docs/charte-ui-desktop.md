# Charte UI — Raqmi System Desktop

> **Statut** : document de référence du gardien du design. Version du 30/08/2026, révisée le
> 01/09/2026 (navigation à quatre niveaux, icônes des 22 domaines, badges de maturité — vague 1 de
> la réorganisation fonctionnelle ; spécification détaillée dans `docs/design/`).
>
> **Règle fondatrice** : cette charte **décrit l'existant** du client WPF (`src/RaqmiSystem.Desktop/`).
> Chaque règle est observée dans le code réel et citée avec sa référence. Quand deux écrans
> divergent, la divergence est notée et tranchée en faveur de la pratique majoritaire ou la plus
> récente — en le disant. Une trentaine de modules restent à construire : chacun doit paraître
> issu du même produit sans dépendre de la mémoire de qui le construit.

---

## 1. Identité

### 1.1 Marque

L'identité visuelle est définie dans `assets/brand/raqmi-system/README.md` : le symbole fusionne
le `Q` latin et le `ق` arabe ; signature « Un système. Toute votre entreprise. » (reprise en
sous-titre de l'en-tête, `MainWindow.xaml:39`). Version blanche du symbole sur fond sombre,
version couleur sur fond clair (règle de marque n° 3 — respectée par l'en-tête et la carte de
connexion, `MainWindow.xaml:29` et `MainWindow.xaml:149`). Icône d'application :
`Assets/RaqmiSystem.ico` (`MainWindow.xaml:6`).

### 1.2 Palette

**Toutes** les couleurs de l'interface proviennent des brushes de
`src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` (lignes 15-134). Aucun écran ne pose de couleur
en dur : un nouvel écran référence les clés ci-dessous par `{StaticResource …}`, rien d'autre.
Les variantes hover/pressed sont dérivées de façon cohérente : ~ +8 % de luminosité au survol,
~ −10 % à l'appui (commentaire d'en-tête du thème, `RaqmiTheme.xaml:5-10`).

**Règle de contraste** : toute paire texte/fond du produit atteint WCAG AA — 4,5:1 pour du
texte, 3:1 pour un trait, une bordure ou une barre porteuse d'information. Un nouveau token de
couleur se vérifie avant d'être ajouté. Deux conséquences structurantes suivent de cette règle,
détaillées ci-dessous : l'accent existe en deux rôles, et le focus clavier a ses propres tokens.

#### Structure et marque

| Clé | Hexa | Usage observé |
|---|---:|---|
| `StructureBrush` | `#071525` | Fonds sombres de marque : en-tête de fenêtre, info-bulles |
| `StructureShadowColor` | `#071525` | Couleur unique de toutes les ombres portées (`DropShadowEffect`) |
| `StructureElevatedBrush` | `#0C2135` | Surface surélevée sur fond structure |
| `LoginBackdropBrush` | dégradé `#071525 → #0A2440 → #071C31` | Fond plein écran de la connexion |
| `HeaderForegroundBrush` | `#FFFFFF` | Texte principal sur fonds structure |
| `HeaderMutedBrush` | `#9FC3DC` | Texte secondaire sur fonds structure |
| `PrimaryBrush` / Hover / Pressed | `#073B78` / `#0A4C97` / `#052B57` | Navy primaire : texte des boutons secondaires, état actif de navigation |
| `SecondaryBrush` / Hover / Pressed | `#145CAB` / `#1A6BC4` / `#0F4A8C` | Bleu secondaire : boutons fantômes |
| `AccentBrush` / Hover / Pressed | `#0AA3AD` / `#0DB6C1` / `#088790` | Teal de **marque**. Uniquement ce qui ne porte pas de texte : pastilles, traits, bordures de survol, filets de sélection. Il ne pèse que 3,06:1 sur blanc — assez pour un trait, pas pour du texte |
| `AccentActionBrush` / Hover / Pressed | `#07767D` / `#08838B` / `#066A70` | Teal d'**action** : toute surface pleine portant du texte ou un glyphe — bouton principal, filtre actif, case cochée, barre de progression. Même teinte (H 184), assombrie pour atteindre 5,39 / 4,54 / 6,36:1 sur texte blanc |
| `AccentSoftBrush` | `#E1F4F5` | Fond doux teinté accent (pastille d'icône d'une carte module disponible) |
| `AccentSelectionBrush` | `#2E0AA3AD` | Sélection translucide (18 % accent) : lignes de grille, sélection de texte |

#### Danger

| Clé | Hexa | Usage |
|---|---:|---|
| `DangerBrush` / Hover / Pressed | `#C0392B` / `#D24A3C` / `#992E22` | Actions destructrices, messages d'erreur |
| `DangerSoftBrush` | `#FBECEA` | Fond doux d'un encart d'erreur |
| `DangerBorderBrush` | `#EFC9C4` | Bordure d'un encart ou bouton danger |

#### Surfaces, bordures, textes

| Clé | Hexa | Usage |
|---|---:|---|
| `AppBackgroundBrush` | `#F4F7FA` | Fond de la fenêtre (« Cloud » de la marque) |
| `SurfaceBrush` | `#FFFFFF` | Cartes, champs, grilles |
| `SurfaceSubtleBrush` | `#EEF4F8` | Cartes secondaires, survols discrets |
| `SurfaceHoverBrush` | `#E4EDF4` | État pressé des surfaces |
| `PanelBorderBrush` | `#DCE6EF` | Bordure d'une **carte** : filet décoratif, aucun seuil — la carte est déjà identifiée par sa surface |
| `FieldBorderBrush` | `#7A8FA3` | Bordure d'un **champ de saisie** : 3:1 (WCAG 1.4.11). Un champ blanc sur carte blanche n'est identifiable que par sa bordure. Teinte du thème, saturation abaissée à 18 % pour rester gris à l'œil |
| `BorderStrongBrush` | `#B9CCDD` | Bordure renforcée (survol des champs) — état de survol, l'identification est déjà portée par `FieldBorderBrush` au repos |
| `TextPrimaryBrush` | `#0F2337` | Texte principal |
| `TextSecondaryBrush` | `#4A6A87` | Texte secondaire (alias `MutedBrush`) |
| `TextMutedBrush` | `#5B6C7E` | Texte atténué (légendes). Choisi pour tenir 4,5:1 sur **tous** les fonds clairs du thème, fond de ligne alternée compris — pas seulement sur blanc |
| `TextLabelBrush` | `#475569` | Libellés de champs |
| `TextPlaceholderBrush` | `#5C7188` | Textes indicatifs des champs. Un texte indicatif est une consigne de saisie : il se lit, donc il respecte 4,5:1 comme n'importe quel texte |
| `DisabledBackgroundBrush` / `DisabledForegroundBrush` / `DisabledBorderBrush` | `#E5EBF1` / `#576C82` / `#DDE5EC` | États désactivés. WCAG exempte les contrôles désactivés ; la règle 3.2 ne le permet pas : c'est par la désactivation qu'un profil en lecture seule apprend ce qu'il ne peut pas faire. Le libellé reste donc lisible (4,52:1) |
| `FocusRingBrush` | `#073B78` | Anneau de focus clavier sur surface claire (9,3 à 11,0:1) |
| `FocusRingOnFilledBrush` | `#FFFFFF` | Anneau de focus sur une surface d'action pleine : 5,4:1 sur l'accent d'action, 11,0 sur le primaire, 5,4 sur le danger |
| `FocusRingOnDarkBrush` | `#FFFFFF` | Anneau de focus sur le fond structure (en-tête) : 18,4:1. Distinct du précédent **parce que les deux divergent en thème sombre** — la surface d'action y devient claire (anneau sombre) quand l'en-tête reste sombre (anneau clair). Un token unique tombait alors à 1,17:1 |
| `ModuleInactiveBackgroundBrush` / `ModuleActiveBackgroundBrush` | `#EEF4F8` / `#1A0AA3AD` | Navigation latérale |
| `RowHoverBrush` / `RowAltBrush` / `GridHeaderForegroundBrush` | `#F0F6FA` / `#FAFCFE` / `#5B7591` | Grilles |
| `ScrollThumbBrush` / `ScrollThumbHoverBrush` | `#6C90B0` / `#567DA0` | Barres de défilement. Un ascenseur est un composant d'interface : 3:1 s'y applique, et il était à 1,44 |

#### Les deux apparences

Le produit a **deux palettes** : claire et sombre. Le choix est **par poste**, pas par compte —
il dépend de l'écran et de l'éclairage du lieu, pas de qui se connecte, et une réception de nuit
garde son écran sombre quand l'équipe change. Bascule rapide dans l'en-tête, trois états
(Système / Clair / Sombre) dans « Paramétrage global → Poste de travail ».

**Comment cela tient techniquement** : `ThemeManager` **remplace les entrées** du dictionnaire
de ressources par des brushes neufs. Muter la propriété `Color` des brushes en place aurait été
plus élégant — un `{StaticResource}` capture la référence de l'objet, donc muter l'objet aurait
repeint tout à chaud — mais WPF **scelle** les `Freezable` d'un `ResourceDictionary` rattaché à
l'`Application`, y compris les copies qu'on tenterait d'y réinjecter. La tentative échoue à
l'exécution : *« impossible de définir une propriété pour l'objet '#FF073B78', car il est en
lecture seule »*.

**Conséquence à connaître** : une référence `{StaticResource}` déjà résolue garde l'ancien objet.
Sans effet au démarrage, où rien n'est encore résolu — c'est pourquoi `App.OnStartup` applique le
thème **avant** `base.OnStartup`, et pourquoi ce n'est pas négociable. En cours de session, en
revanche, seuls les écrans pas encore ouverts prennent la nouvelle apparence ; les autres
attendent le redémarrage, et le bandeau de session le dit au lieu de laisser croire à un bug.

Repeindre réellement à chaud demanderait de convertir les références de couleur en
`{DynamicResource}` — environ 3 000 lignes de XAML. C'est mécanique et faisable, mais c'est un
chantier à part. Les deux clés de densité, elles, **sont** en `{DynamicResource}` : c'est pour
cela que changer la densité repeint les grilles déjà affichées, contrairement au thème.

**Conséquence pour un nouveau token de couleur** : tout `SolidColorBrush` ajouté au thème doit
recevoir une valeur dans `ThemePalette.Sombre`, sinon il garde sa valeur claire — une carte
blanche au milieu d'un écran de nuit. `ThemeManager.VerifierCouverture` lève une assertion en
Debug si la table et le thème divergent, dans un sens comme dans l'autre. Les deux palettes
couvrent aujourd'hui **82 brushes sur 82** (70 historiques + 12 brushes de maturité des domaines).

**Ce que le mode sombre n'est pas** : une inversion. Chaque valeur est posée pour son rôle —
les surfaces s'éclaircissent avec l'élévation au lieu de s'assombrir, les accents s'éclaircissent
et le texte qu'ils portent devient sombre (d'où `AccentActionForegroundBrush`), les badges gardent
leur teinte sémantique mais renversent le couple fond/texte, et le texte principal est un blanc
bleuté plutôt que du blanc pur, qui éblouit sur fond sombre.

Deux ressources ne suivent pas et c'est voulu : `StructureShadowColor` est une `Color` (une
valeur, pas un objet — elle ne se mute pas), et `LoginBackdropBrush` habille un écran de connexion
déjà sombre dans les deux thèmes, qui est la scène de marque.

#### Badges de statut (teintes sémantiques)

Chaque statut = fond doux + texte foncé contrasté de la même teinte (`RaqmiTheme.xaml:87-95`) :

| Statut sémantique | Fond | Texte | Sens |
|---|---:|---:|---|
| Draft (« Brouillon ») | `#E2E8F0` | `#475569` | Neutre, non engagé |
| Submitted (« Soumise », « Approuvé », avertissement) | `#FCEFC7` | `#92600A` | En attente, attention |
| Validated (« Validée », « Confirmé », « Payée », « Actif ») | `#DBF2E3` | `#187A41` | Accompli, positif |
| Rejected (« Rejetée », « Annulé », « Verrouillé ») | `#FBE3E1` | `#B3261E` | Refusé, négatif |

Statuts d'avancement des modules (accueil) : quatre paires fond/texte à contraste ≥ 4,5:1
(`ModuleStatusAvailable/Api/Partial/Planned…`, `RaqmiTheme.xaml:100-107`) et leurs versions
saturées pour la barre segmentée (`ModuleProgress…`, `RaqmiTheme.xaml:109-112`).

#### Badges de maturité des domaines

La maturité d'un domaine fonctionnel (`FunctionalMaturity`, quatre niveaux calculés depuis la
readiness, jamais saisis — § 4.2) a ses propres brushes (`Maturity<Niveau>Background/Foreground/
AccentBrush`) et ses styles nommés `MaturityBadge.<Niveau>` (`Border` : fond doux, coins 9, texte
10,5 SemiBold hérité par `TextElement.Foreground`) et `MaturityDot.<Niveau>` (`Ellipse` 7 px) :

| Niveau | Libellé | Fond | Texte | Accent | Sens |
|---|---|---:|---:|---:|---|
| `Planned` | « Planifié » | `#E8EEF4` | `#46617B` | `#7798B5` | Ardoise : non engagé |
| `TechnicalPreview` | « Aperçu technique » | `#FBEBC8` | `#8A5A08` | `#BF8B16` | Ambre : attention, parcours incomplet |
| `Functional` | « Fonctionnel » | `#DDF2F4` | `#096B73` | `#0AA3AD` | Teal de marque : en service |
| `ProductionReady` | « Prêt pour la production » | `#DCF3E5` | `#14713C` | `#1F9D57` | Vert : accompli, homologué |

Le vert est **réservé au dernier niveau** : un domaine seulement Fonctionnel ne peut pas l'obtenir,
et un bandeau sans vert dit la vérité (§ 4.3). Contraste 5,0 à 5,5:1 en clair, 7,0 à 8,0:1 en
sombre ; les accents tiennent 3:1 sur `SurfaceBrush`, pas sur `AppBackgroundBrush` — comme les
`ModuleProgress…`, ils se posent sur une carte. Les libellés viennent d'une source unique
(`FunctionalMaturityDisplay`, sur le modèle de `ModuleCatalog.StatusLabel`). Où et quand le badge
s'affiche : `docs/design/navigation-shell.md`, § 6.

### 1.3 Typographie

- **Famille unique** : `Manrope, Noto Kufi Arabic, Segoe UI` (`AppFontFamily`,
  `RaqmiTheme.xaml:13`), posée sur la fenêtre (`MainWindow.xaml:11`) et **répétée sur chaque
  UserControl de module** (ex. `TreasuryView.xaml:5`), car un UserControl n'hérite pas des
  ressources de la fenêtre au moment du design.
- **Échelle** (styles nommés du thème, à réutiliser tels quels, jamais des tailles ad hoc) :

| Style | Taille | Graisse | Rôle |
|---|---:|---|---|
| `HomeGreetingText` | 26 | SemiBold | Salutation de l'accueil |
| `PageTitleText` | 21 | SemiBold | Titre d'écran (`RaqmiTheme.xaml:119`) |
| `MetricValueText` | 18 | SemiBold | Valeur chiffrée d'un indicateur |
| `SectionTitleText` | 16 | SemiBold | Titre de section / de carte |
| `EmptyStateTitleText` | 14 | SemiBold | Titre d'un état vide |
| `BodyText` | 13 | Regular | Corps de texte ; taille par défaut des contrôles |
| `SubtitleText` | 12,5 | Regular | Sous-titre sous un titre d'écran |
| `LabelText` | 12 | SemiBold | Libellé au-dessus d'un champ (marge basse 6) |
| `CaptionText` | 11,5 | Regular | Légende atténuée |
| `MetricLabelText` | 11 | SemiBold | Étiquette de métrique / de section latérale |

- Graisse : **SemiBold pour tout ce qui titre ou agit** (titres, libellés, boutons, en-têtes de
  colonnes) ; jamais de Bold.

### 1.4 Iconographie

- **Uniquement des `Path` vectoriels dessinés main**, au trait, dans une boîte 16×16,
  `StrokeThickness` 1.5 (2 pour le pictogramme « barres »), extrémités et jointures arrondies —
  jamais d'emoji, jamais de bitmap, jamais de police d'icônes. Références : les onze icônes de la
  sidebar (`MainWindow.xaml:307-517`) et les icônes de groupe `ModuleGroupIcon.<clé>`
  (`RaqmiTheme.xaml:1134-1155`, style `ModuleCardIcon` ligne 1157).
- Le trait d'une icône de bouton se lie à la couleur du texte du bouton
  (`Stroke="{Binding Foreground, RelativeSource=…}"`, `MainWindow.xaml:314`) : l'icône suit
  automatiquement les états actif/désactivé.
- Icônes utilitaires du thème (flèche de ComboBox, coche de CheckBox, flèche de tri, cadenas
  `ModuleCardLockIcon`) : même langage au trait.
- **Une icône par domaine fonctionnel** : les 22 domaines de `FunctionalArchitectureCatalog` ont
  chacun leur `PathGeometry` `ModuleGroupIcon.<IconKey>` (sept reprises des familles historiques,
  quinze dessinées pour la réorganisation), même boîte, même trait, un seul motif, contrôlées à
  16 px dans les deux thèmes. Les clés de famille qui ne portent plus de domaine (`Socle`,
  `Exploitation`, `Controle`, `Conformite`, `Specifique`) restent déclarées : le catalogue
  historique les cite. Inventaire, descriptions et règles d'ajout : `docs/design/icones-domaines.md`.
  Un cercle s'écrit `M cx,(cy−r) A r,r 0 1 0 cx+0.01,(cy−r)` (départ au sommet).

### 1.5 Langue

- **Tout libellé visible est en français accentué**, y compris les messages d'état, les
  info-bulles, les en-têtes CSV (`CsvExportHelper.cs:20`) et les libellés d'énumérations traduits
  une seule fois par un dictionnaire ou un convertisseur dédié
  (`DailyRevenueStatusDisplay.cs` — accords au féminin compris : « Validée », « Rejetée » ;
  `CustomersView.xaml.cs:21-26`). La valeur envoyée à l'API reste celle du domaine.
- **Les commentaires de code et les clés de ressources sont sans accents** (convention constante
  du dépôt : voir `RaqmiTheme.xaml`, `ModuleViewContext.cs`, et la table `IconKeys` en clés ASCII,
  `ModuleCatalog.cs:67-84`).
- Les exports CSV sont écrits en UTF-8 **avec BOM** pour qu'Excel lise correctement les accents
  (`CsvExportHelper.cs:68-71`), avec protection contre l'injection de formule
  (`CsvExportHelper.cs:88-100`).
- Typographie française soignée dans les libellés : guillemets « », tirets cadratins —, espaces
  avant `? : ;` (voir les messages de `UsersView.xaml.cs`).

---

## 2. Architecture d'écran

### 2.1 Le contrat de vue

Tout nouveau module est un **UserControl autonome** dans `Views/`, qui respecte le contrat
observé sur les six vues existantes (Closing, Treasury, Customers, Invoices, Settings, Users) :

1. **`Initialize(ModuleViewContext)`** — mémorise le contexte prêté par la fenêtre, relève les
   permissions du profil, **aucun appel réseau** (`CustomersView.xaml.cs:51-57`,
   `TreasuryView.xaml.cs:73-85`).
2. **`LoadAsync()`** — (re)charge les données ; **sort silencieusement** si le contexte est
   absent ou la session fermée (`if (context is null || !context.ApiClient.IsAuthenticated) return;`,
   `TreasuryView.xaml.cs:91-99`).
3. **`ResetState()`** — vide grilles, formulaires, compteurs **et tout secret affiché** ; appelée
   à la déconnexion pour ne jamais laisser les données d'un utilisateur au suivant
   (`UsersView.xaml.cs:110-129`, `MainWindow.xaml.cs:571-576`). Attention : les vues **survivent
   à la déconnexion** et resservent au profil suivant sur les mêmes instances — tout état posé
   pour un profil doit être réversible.
4. **Jamais d'accès à `MainWindow`** ni aux autres vues : tout passe par le
   `ModuleViewContext` (client API, URL, `SetStatus`, `RunAsync`, `HasPermission`,
   `CurrentUserId`) — contrat documenté dans `Views/ModuleViewContext.cs:5-12`.

La fenêtre construit un unique contexte à la connexion et l'injecte dans chaque vue
(`MainWindow.xaml.cs:508-522`).

### 2.2 Chargement paresseux

La première ouverture de l'onglet d'un module déclenche son `LoadAsync()`, les suivantes non
(`EnsureModuleTabLoadedAsync`, `MainWindow.xaml.cs:388-429`, ensemble `loadedModuleTabs`).
Un nouveau module s'enregistre dans ce `switch` avec son index d'onglet. La navigation est
**unique** : `NavigateToModule(tabIndex, bouton)` synchronise l'onglet et la surbrillance de la
sidebar, et sert aussi bien à la sidebar qu'aux cartes de l'accueil
(`MainWindow.xaml.cs:149-156`). Le changement de module s'accompagne d'un fondu de 150 ms
(`MainTabs_SelectionChanged`, `MainWindow.xaml.cs:357-391`).

### 2.3 Structure type d'un écran

Le gabarit constant, observable sur `CustomersView.xaml`, `InvoicesView.xaml`, l'onglet Unités
(`MainWindow.xaml:787-963`) :

1. **En-tête d'écran** : `PageTitleText` + `SubtitleText` à gauche ; boutons Actualiser /
   Exporter / Imprimer (`SecondaryButton`) alignés à droite dans un `DockPanel`.
2. **Carte de filtres ou de saisie** (`SubtleCardBorder` posée dans une `CardBorder`) : champs
   avec `LabelText` au-dessus, boutons d'action en bout de rangée (`GhostButton` « Nouveau »,
   `PrimaryButton` pour créer/enregistrer, `SecondaryButton` / `DangerButton` pour les actions
   d'état).
3. **Grille** (`DataGrid` du thème : lecture seule, sélection simple, lignes de 40, alternance)
   surmontée d'un **état vide** (voir § 3.7).
4. **Panneau de détail** de la ligne sélectionnée quand l'objet est riche (facture :
   `DetailPanel` piloté par `DataContext`, `InvoicesView.xaml.cs:239-253`).
5. Le formulaire bascule **création ↔ modification** sur la sélection de grille, avec un titre
   qui le dit (« Nouveau client » / « Modifier HTL-01 ») et un bouton renommé
   (`CustomersView.xaml.cs:186-206`), et la sélection est **restaurée** après rechargement par sa
   clé stable (`RestoreSelection`, `CustomersView.xaml.cs:160-176`).

### 2.4 Sous-onglets

Le panneau latéral « Modules » est **l'unique navigation** de l'application : le style implicite
du thème supprime les en-têtes de `TabControl` (`RaqmiTheme.xaml:994-1011`). Un module qui couvre
plusieurs sections internes (au moins deux grilles indépendantes avec leurs propres filtres)
utilise des sous-onglets **visibles** avec les styles locaux `SectionTabItem` /
`SectionTabControl` : onglet souligné d'un indicateur accent de 2,5 px, texte `PrimaryBrush` à
l'état sélectionné. **Référence : `TreasuryView.xaml:11-87`** (trois sections : encaissements,
ordres de paiement, comptes bancaires). `AccountingView.xaml` reprend ce gabarit et montre le
second niveau admis : un `SectionTabControl` imbriqué dans l'onglet « Exercices, tiers et livres ».

**Nom des sous-onglets** (règle unique, vague 1) : quand un module couvre plusieurs sous-modules
de la cartographie cible dans la même vue, les sous-onglets portent **les noms des sous-modules
cibles** ; quand la vue ne couvre qu'un sous-module, ils portent les noms de ses écrans. Jamais plus
de deux niveaux d'onglets, jamais un nom que la cartographie ne connaît pas, jamais une action en
guise d'onglet (« Nouvelle réservation » est un bouton `New…Button`, pas un onglet). Inventaire des
sous-onglets actuels et correspondance : `docs/design/navigation-shell.md`, § 4.

### 2.5 Navigation à quatre niveaux

La navigation cible est un arbre `Domaine → Module → Sous-module → Écran` posé sur les 22 domaines
de `FunctionalArchitectureCatalog` (`src/RaqmiSystem.Application/Navigation/`, identifiants stables
`01`…`22`). Ce que le shell en montre, et les règles qui ne se discutent pas écran par écran :

1. **Barre latérale = domaines, dans l'ordre `01`…`22`**, une section repliable par domaine
   (`ModuleNavGroup`), l'icône du domaine en en-tête, `22 Administration Système` épinglée en pied.
   Elle **ne liste que les écrans ouvrables** par le profil (écran existant **et** permission de
   lecture) ; un domaine sans écran ouvrable n'y apparaît pas. Le sommaire complet — cadenas,
   modules planifiés, maturité — est l'accueil. Le masquage reste un confort, jamais une sécurité
   (§ 3.9). L'état déroulé est mémorisé **par poste**, comme le thème et la densité.
2. **Fil d'Ariane** au-dessus de chaque vue, posé par la fenêtre (jamais par la vue) : icône du
   domaine, puis `Domaine › Module › Sous-module › Écran`, ancêtres cliquables en
   `TextSecondaryBrush`, segment courant en `TextPrimaryBrush`, segments absents omis. Il suit les
   sous-onglets de la vue et affiche le chemin **cible** même quand un onglet relève d'un autre
   domaine que celui par lequel on est entré (cas transitoires listés dans la spécification).
3. **Sous-onglets nommés comme les sous-modules cibles** (§ 2.4).
4. **Un seul chemin de navigation** (`NavigateToModule`) et **un seul vocabulaire** : le libellé
   d'un écran est le même dans la barre latérale, sur la carte de l'accueil, dans le fil d'Ariane et
   dans la recherche.
5. **Maturité affichée là où elle informe, tue là où elle bruite** : badge complet dans l'en-tête de
   domaine de l'accueil et le bandeau d'avancement ; point discret dans la barre latérale et badge
   dans le fil d'Ariane **seulement** sous `Functional`. La carte module garde son statut de module.
6. **`01 Mon Espace` est la racine** : première ligne de la barre latérale, `Alt+Origine`. Accueil-
   catalogue aujourd'hui, portail personnel en phase 4 (tuiles alimentées par des projections
   serveur, aucune donnée métier propre) ; le catalogue devient alors un écran « Où en est le
   produit ? », jamais supprimé.
7. **Accessibilité** : en-tête de domaine = `ToggleButton` (Espace/Entrée), noms
   `AutomationProperties` sur en-têtes (« Domaine …, n écrans »), lignes (« Ouvrir … »), segments
   (« Remonter à … ») et points de maturité ; ordre de tabulation en-tête → barre latérale → fil
   d'Ariane → vue → bandeau de session ; raccourcis inchangés (§ 3.13, `F1`).

Spécification complète (densités, mémorisation, recherche, états vides, profils, liste
d'intégration) : `docs/design/navigation-shell.md` ; maquette de validation :
`docs/design/maquette-shell.html`.

---

## 3. Règles UX non négociables

Chaque règle est observée dans le code, avec sa référence. Un écart sur l'une d'elles bloque la
revue de design.

### 3.1 Tout appel API passe par `context.RunAsync`

Jamais de `try/catch` maison autour d'un appel réseau dans une vue : `RunAsync` gère curseur
d'attente, barre de progression, neutralisation des onglets contre la double soumission, et la
traduction des erreurs (HTTP, réseau, validation) en message d'état
(`ModuleViewContext.cs:48-53`, implémentation `RunApiActionAsync` + `SetBusy`,
`MainWindow.xaml.cs:1222-1276`). « Toute action déclenchée par un bouton d'une vue de module doit
passer par là. » Les vérifications de saisie se font **à l'intérieur** du `RunAsync` ou avant,
via `SetStatus(…, isError: true)`.

### 3.2 Boutons d'écriture conditionnés aux permissions, info-bulles symétriques

Les permissions sont relevées dans `Initialize()` (`HasPermission`, clé de `PermissionCatalog`)
et les actions d'écriture sont **grisées** quand le droit manque — plutôt que de laisser
découvrir un 403 après avoir saisi tout un formulaire (`ModuleViewContext.cs:36-43`).
L'info-bulle explicative est posée quand le droit manque et **RESTAURÉE quand il est présent** :
c'est le rôle d'`ApplyPermissionHint`, qui capture l'info-bulle d'origine avant toute
substitution (`TreasuryView.xaml.cs:770-784`, `SettingsView.xaml.cs:645-655`, généralisé en
`SetActionState` avec motifs par cause dans `UsersView.xaml.cs:812-821`). Motif : les vues
survivent à la déconnexion — un message « permission requise » posé pour un profil restreint ne
doit pas survivre à la reconnexion d'un profil qui a le droit.
**Divergence notée** : `CustomersView.UpdateActionButtons` pose l'info-bulle sans jamais la
restaurer (`CustomersView.xaml.cs:414-428`) — c'est la pratique minoritaire et la plus ancienne ;
la règle est le motif symétrique de Treasury/Settings/Users, et Customers est à aligner.
Variante lecture seule : un profil sans droit d'écriture voit des champs **en lecture seule**
(valeur lisible et copiable) plutôt que désactivés, avec un encart qui explique pourquoi
(`SettingsView.xaml.cs:608-637`).

### 3.3 Confirmation des actes engageants

Tout acte engageant (émission d'une facture, clôture d'une journée, désactivation d'un compte,
purge d'audit, remplacement de rôles…) passe par une boîte de confirmation :
**fenêtre propriétaire (`Window.GetWindow(this)`), icône `MessageBoxImage.Warning`, défaut sur
`MessageBoxResult.No`** — le gabarit `Confirm(message, caption)` observé identiquement dans
`UsersView.xaml.cs:825-834`, `TreasuryView.xaml.cs:1268-1274`, `InvoicesView.xaml.cs:880-888`,
`SettingsView.xaml.cs:670-679`. Le message dit **ce qui devient irréversible** et rappelle les
identifiants concernés (client, montant, date, motif).
**Divergences notées** : `CustomersView` (défaut Non mais sans fenêtre propriétaire,
`CustomersView.xaml.cs:382-387`) et `ClosingView` (ni propriétaire ni défaut explicite — le
défaut tombe alors sur Oui, `ClosingView.xaml.cs:207-212` et `281-286`). La règle est le gabarit
majoritaire et le plus récent (`Confirm` avec propriétaire + défaut Non) ; les deux vues
divergentes sont à aligner.

### 3.4 `MaxLength` aligné sur les bornes du domaine

Chaque champ texte porte le `MaxLength` de la contrainte du domaine ou de la colonne : NIF 15,
RC/AI/NIS 20, nom 200, ville 80, téléphone 40, courriel 160 (`CustomersView.xaml:110-212`,
mêmes bornes dans `SettingsView.xaml:193-272`), motifs 500/1000 (`ClosingView.xaml:143,219`),
IBAN/RIB 34 (`TreasuryView.xaml:923`). Les bornes numériques hors `MaxLength` sont vérifiées
avant l'envoi, avec un message explicite plutôt qu'une erreur serveur après l'aller-retour :
capacité `numeric(18,3)` / `numeric(18,2)` des lignes de facture, contrôle d'overflow du total
par division (`InvoicesView.xaml.cs:36-44` et `676-745`).

### 3.5 États vides explicites dans chaque grille

Toute grille superpose un état vide (pictogramme au trait + `EmptyStateTitleText` +
`EmptyStateHintText` indiquant l'action qui remplira la grille), `IsHitTestVisible="False"`,
affiché par un `DataTrigger` sur `Items.Count == 0`. Gabarit : onglet Unités
(`MainWindow.xaml:933-959`) ; présent dans toutes les vues (`CustomersView.xaml:345`,
`TreasuryView.xaml:541, 839, 1058`, `InvoicesView.xaml:337, 731`, `ClosingView.xaml:331`,
`UsersView.xaml:467`).

### 3.6 Badges de statut aux teintes sémantiques du thème

Un statut s'affiche en pastille arrondie fond doux + texte foncé, exclusivement avec les brushes
`Status…` du thème (§ 1.2), la couleur pilotée par `DataTrigger` sur la valeur du statut
(recettes : `MainWindow.xaml:1220-1240` ; factures : `InvoicesView.xaml.cs:348-353` ;
trésorerie : `TreasuryView.xaml:494-810` ; comptes : `UsersView.xaml:407-423`). Le libellé
français vient d'une source unique par énumération (`DailyRevenueStatusDisplay.cs:5-9` : grille,
impression et CSV rendent **le même mot**). Jamais de rouge/vert inventés localement.

### 3.7 Montants et quantités

- Montants `decimal` affichés en **`N2` de la culture courante, alignés à droite** :
  `StringFormat={}{0:N2}` + `AmountCellText` + `RightAlignedColumnHeader` dans les grilles
  (`MainWindow.xaml:1188-1208`, `TreasuryView.xaml:480`), `ToString("N2",
  CultureInfo.CurrentCulture)` dans le code (`TreasuryView.xaml.cs:380-385`). Saisie via
  `AmountTextBox` (texte aligné à droite, `RaqmiTheme.xaml:562-564`).
- **Quantités de facturation en `N3`** (le domaine admet 3 décimales) :
  `InvoicesView.xaml:466`, messages `InvoicesView.xaml.cs:698`.
- La vue règle sa `Language` sur la culture courante pour que les `StringFormat` XAML suivent la
  même culture que le code (`InvoicesView.xaml.cs:61-68`).
- Les CSV, eux, restent en culture invariante sans séparateur de milliers — format machine,
  distinct de l'affichage (`CsvExportHelper.cs:24-27`).

### 3.8 Dates et heures en heure locale

Tout horodatage UTC renvoyé par l'API est converti en heure du poste avant affichage :
`UtcToLocalTimeConverter` (`ClosingView.xaml.cs:359-380`, utilisé `ClosingView.xaml:285-292`)
ou `value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture)`
(`UsersView.xaml.cs:843-846`, `SettingsView.xaml.cs:700-703`, `InvoicesView.xaml.cs:903`).
Les bornes de calendrier raisonnent aussi en local (`DateTime.Today`, jamais l'horloge UTC —
motif expliqué `ClosingView.xaml.cs:82-89`).

### 3.9 L'écran ne promet jamais une règle différente de celle du serveur

Les garde-fous affichés (permissions, auto-désactivation interdite, champs requis selon le mode
de paiement, bornes de dates) sont le **miroir** des règles du serveur, jamais leur remplacement :
« L'autorisation reste évidemment appliquée par le serveur : ceci n'est qu'un confort
d'interface, jamais une mesure de sécurité » (`ModuleViewContext.cs:40-42` ; doctrine complète en
tête d'`UsersView.xaml.cs:19-24`). Les règles métier rappelées à l'écran référencent le domaine
(`CashReceipt.RequiresReference` / `RequiresBankAccount`, `TreasuryView.xaml.cs:708-722` ;
`InvoiceLine.AllowedVatRates`, `InvoicesView.xaml.cs:31` — aucune valeur recopiée). Corollaire :
une carte de l'accueil ne navigue pas vers un écran dont le titre ne tient pas la promesse de son
libellé (`ModuleCatalog.cs:232-234`).

### 3.10 Les chiffres financiers viennent du serveur

Un total affiché comme fiable est **renvoyé par l'API**, jamais recalculé côté client : résumé
des encaissements par `GetCashReceiptSummaryAsync` (`TreasuryView.xaml.cs:332-339`), totaux de
factures dans la réponse (`TotalExclVat/TotalVat/TotalInclVat`, `InvoicesView.xaml:521-533`),
totaux des recettes et du tableau de bord (`MainWindow.xaml.cs:761, 784`). Seule exception
admise : un **aperçu local pendant la saisie**, explicitement non contractuel — « La source de
vérité reste le serveur, qui renvoie les totaux définitifs à l'enregistrement »
(`UpdateEditorTotals`, `InvoicesView.xaml.cs:858-861`). Et quand un chiffre affiché ne répond pas
exactement au filtre visible, l'écran le **dit** par un bandeau d'avertissement
(`WarningBanner`, `TreasuryView.xaml:95-109` et `TreasuryView.xaml.cs:341-346`).

### 3.11 Secrets affichés une seule fois

Le mot de passe temporaire (création de compte, réinitialisation) suit le protocole complet
d'`UsersView` :

- son affichage ne dépend d'**aucun appel ultérieur susceptible d'échouer** (bloc
  `try/finally` : rechargement d'abord, affichage garanti ensuite,
  `UsersView.xaml.cs:481-492` et `586-596`) ; il reste **persistant à l'écran** tant que le
  contexte ne change pas ;
- **copiable avec exclusion de l'historique du presse-papiers** : `DataObject` portant
  `ExcludeClipboardContentFromMonitorProcessing`, `CanIncludeInClipboardHistory=false`,
  `CanUploadToCloudClipboard=false` — jamais `Clipboard.SetText` — avec message d'échec honnête
  si le presse-papiers est verrouillé (`UsersView.xaml.cs:711-744`) ;
- **effacé à tout changement de contexte** : changement de sélection
  (`UsersView.xaml.cs:287-293`), onglet quitté (`IsVisibleChanged`,
  `UsersView.xaml.cs:131-139`), déconnexion (`ResetState`, `UsersView.xaml.cs:110-129`), ou
  fermeture manuelle par l'utilisateur.

Même exigence côté identifiants mémorisés : chiffrés DPAPI liés à la session Windows, jamais en
clair (`MainWindow.xaml:175-181`).

### 3.12 Message d'état plutôt que boîte de dialogue

Le retour d'information courant (succès, erreur de saisie, erreur API) passe par
`context.SetStatus(message, isError)` vers le bandeau « Session » de la sidebar — rouge
`DangerBrush` en erreur (`MainWindow.xaml.cs:1281-1293`). Les `MessageBox` sont réservées aux
**confirmations** (§ 3.3), jamais à l'information.

### 3.12 bis Le message d'état s'annonce

`SetStatus` fait **clignoter brièvement le fond** du bandeau de session à chaque nouveau
message (`MainWindow.xaml.cs`, `FlashSessionStrip`) : teinte accent pour une information,
teinte danger pour une erreur, qui tient plus longtemps (1,6 s contre 0,9 s) et part d'un fond
plus dense. Une pastille de couleur double le signal, reprise de la carte de connexion.

La raison : le bandeau est en pied de fenêtre, toujours visible mais loin du geste. Après un
clic sur un bouton placé en haut d'un écran défilant, un texte qui change sans bouger passe
inaperçu — et l'utilisateur reclique, ou croit l'action perdue. Le mouvement attire l'œil sans
rien déplacer, sans fenêtre à fermer et sans bulle qui recouvre l'écran.

### 3.14 Densité des tableaux

Deux densités, réglées par poste dans « Paramétrage global → Poste de travail » :
**Confortable** (lignes de 40 px, l'historique) et **Compact** (32 px, environ un quart de
lignes en plus à hauteur d'écran égale).

Compact **ne réduit pas la taille du texte** — il retire de l'air. Sur ces écrans, le facteur
limitant est le nombre de lignes visibles sans défiler, pas la finesse des caractères ;
rabougrir la police ferait perdre en lisibilité ce qu'on gagnerait en lignes.

Techniquement, deux ressources `Double` (`GridRowHeight`, `GridHeaderHeight`) lues en
**`{DynamicResource}`** par les styles de `DataGrid` — la seule exception au `{StaticResource}`
du reste du thème, et elle est nécessaire : un `Double` n'est pas un objet mutable, changer la
densité remplace l'entrée du dictionnaire, et seul `DynamicResource` voit un remplacement.
Un écran n'a rien à déclarer : la densité s'applique aux 759 grilles du produit.

### 3.13 Nommer les contrôles pour que le clavier les trouve

Neuf raccourcis sont déclarés une seule fois, sur la fenêtre (`MainWindow.xaml`,
`Window.InputBindings`) — F1 les liste à l'utilisateur (`Views/ShortcutsWindow.xaml`). Quatre
d'entre eux agissent sur **l'écran affiché** sans qu'aucune vue ait à les déclarer :
`ShortcutRouter` cherche le contrôle dans le module ouvert et le déclenche comme un clic.

Il le trouve par son **nom**. C'est donc le nom du contrôle qui décide si un écran répond au
clavier, et la convention n'est pas cosmétique :

| Raccourci | Cherche | Convention |
|---|---|---|
| `F5` | Bouton d'actualisation | `x:Name` commence par `Refresh` |
| `Ctrl+S` | Bouton d'enregistrement | `x:Name` commence par `Save` |
| `Ctrl+N` | Bouton de création | `x:Name` commence par `New` |
| `Ctrl+F` | Champ de recherche | `x:Name` contient `Search` ou `Filter` |

Ces noms sont ceux déjà en place : `Refresh…Button` dans 23 des 24 vues, `Save…Button` dans 15,
`New…Button` dans 11, `…Search…TextBox` dans 10. Une vue nouvelle qui les suit hérite des quatre
raccourcis sans écrire une ligne ; une vue qui nomme son bouton `ReloadButton` ne répondra pas à
F5, et rien ne le signalera à la compilation.

Trois garanties tiennent par construction, à ne pas défaire :

- **Le raccourci n'ouvre jamais ce que le clic n'ouvre pas.** Le routeur n'actionne qu'un
  contrôle `IsVisible` **et** `IsEnabled` : un bouton grisé par une permission manquante
  (§ 3.2) le reste au clavier, et l'onglet non affiché d'une vue à sous-onglets est hors
  d'atteinte.
- **Le raccourci agit là où l'on saisit.** La recherche part du contrôle qui a le focus clavier
  et remonte ses ancêtres : dans une vue à plusieurs formulaires, `Ctrl+S` enregistre celui de la
  section courante, pas le premier de l'écran.
- **Un raccourci sans cible le dit.** Pas de cible ⇒ message d'état (§ 3.12), jamais une action
  approchante.

---

## 4. Catalogue des modules (accueil)

### 4.1 Source unique de vérité

**La taxonomie est `FunctionalArchitectureCatalog`**
(`src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.cs`) : les 22 domaines,
leur identifiant stable (`01`…`22`), leur nom, leur clé d'icône, leur maturité et le rattachement
de chacune des 50 entrées historiques par son numéro d'ordre (`LegacyModuleOrders`). Elle ne
dépend ni de WPF ni des `TabIndex` ; deux gardes statiques (`ExpectedDomainCount = 22`,
`ExpectedLegacyModuleCount = 50`) et `FunctionalArchitectureCatalogTests` la verrouillent. Toute
surface qui groupe, ordonne ou nomme un domaine — accueil, barre latérale, fil d'Ariane, filtre de
domaine, documentation — lit ce catalogue et rien d'autre.

`ModuleCatalog.cs` reste, **pendant la transition**, la source des 50 entrées (nom, description,
priorité, statut de module, clé de permission, index d'onglet) ; `ModuleTile` joint les deux
(`FunctionalDomainId`, `FunctionalMaturity`, `GroupIconKey` = icône du domaine). Le lot 1.1 du plan
de migration remplace ce couple par l'arbre `DomainNode → ModuleNode → SubmoduleNode → ScreenNode`
généré depuis une définition unique, les `TabIndex` étant conservés par un adaptateur.

L'accueil est entièrement piloté par ces données : **un seul gabarit de carte**
(`ModuleCatalogCard`), aucune carte écrite en dur.

### 4.2 Statuts à quatre niveaux, calculés

Deux échelles coexistent, et ne s'additionnent pas :

- **Statut d'un module** (`ModuleStatus`, transitoire) : `Disponible` (écran utilisable
  maintenant), `ApiPrete`, `Partiel` (précisé par `StatusNote`), `Planifie`. Il est saisi dans
  `ModuleCatalog` et gardé par les constantes `ExpectedTotal / ExpectedAvailable / ExpectedApiReady /
  ExpectedPartial / ExpectedPlanned` (le constructeur statique vérifie les totaux par
  `Debug.Assert`) et par `tools/check-module-readiness.ps1`.
- **Maturité d'un domaine** (`FunctionalMaturity`) : `Planned`, `TechnicalPreview`, `Functional`,
  `ProductionReady`. Elle est **calculée** depuis les preuves de readiness (Domain, Application, API,
  PostgreSQL, RBAC, Desktop, tests, documentation, smoke — `docs/reorganisation/07-plan-migration.md`,
  modèle de readiness), **jamais saisie à la main** ; la valeur portée aujourd'hui par le catalogue
  est la maturité initiale de `03-cartographie-cible.md` § 3.6, et elle descendra au niveau de chaque
  `ScreenNode` avec le catalogue hiérarchique. Aucun domaine n'est `ProductionReady` tant que les
  tests PostgreSQL réels, le smoke WPF et l'E2E ne sont pas industrialisés.

Livrer un écran = **une seule édition cohérente** : entrée `ModuleCatalog` (statut, `PermissionKey`,
`TabIndex` enregistré dans `EnsureModuleTabLoadedAsync` et `ApplyModulePermissions`), constantes de
garde recalculées, numéro d'ordre rattaché à son domaine dans `FunctionalArchitectureCatalog`, fiche
de readiness mise à jour — la maturité suit, on ne la retouche pas.

### 4.3 Règle d'honnêteté des statuts

**Un statut qui ment au dirigeant est un défaut grave.** Le bandeau d'avancement est « la réponse
directe à “où en est le produit ?” » ; les statuts restent distincts dans la légende car les
additionner surestimerait le travail fait, et les deux échelles (modules, domaines) sont montrées
côte à côte sans jamais être fusionnées. En pratique :

- la maturité d'un domaine n'est jamais relevée « pour faire avancer la barre » : `Functional`
  exige le parcours annoncé utilisable de bout en bout, `ProductionReady` les preuves d'exploitation ;
  le vert des badges est réservé à ce dernier niveau (§ 1.2) ;

- un module dont l'écran ne tient pas la promesse du libellé n'est **pas** cliquable : pas de
  `TabIndex`, statut `Partiel` + note explicative (cas Dashboard PDG / Cockpit DEC,
  `ModuleCatalog.cs:232-242`) ;
- une carte sans permission de lecture est verrouillée : cadenas, info-bulle explicite, fond
  atténué mais **texte à pleine lisibilité** (`RaqmiTheme.xaml:1453-1465`,
  `ApplyModulePermissions`, `MainWindow.xaml.cs:177-208`) ;
- on ne gonfle jamais un statut « pour faire avancer la barre » : `Partiel` exige une
  `StatusNote` qui dit précisément ce qui est couvert et ce qui manque
  (`ModuleCatalog.cs:264-267`).

---

## 5. Checklist de revue design

À dérouler par le gardien du design sur chaque nouvel écran, point par point. Un « non » sans
justification écrite bloque la livraison.

1. **Contrat de vue** — UserControl autonome dans `Views/`, `Initialize(ModuleViewContext)` sans
   appel réseau, `LoadAsync()` avec sortie silencieuse hors session, `ResetState()` complet ;
   aucune référence à `MainWindow` ni à une autre vue.
2. **Enregistrement** — entrée `ModuleCatalog` mise à jour (statut, `PermissionKey`,
   `TabIndex`) **et** constantes de garde recalculées ; onglet ajouté à
   `EnsureModuleTabLoadedAsync`, `SidebarButtonForTab`, `ApplyModulePermissions` ; numéro d'ordre
   rattaché à son domaine dans `FunctionalArchitectureCatalog` (l'icône est celle du domaine,
   `ModuleGroupIcon.<IconKey>` — pas d'icône par écran) ; sous-onglets nommés comme les
   sous-modules cibles (§ 2.4) ; chemin `Domaine › Module › Sous-module › Écran` renseigné pour le
   fil d'Ariane (§ 2.5).
3. **Aucune couleur en dur** — tout passe par les brushes de `RaqmiTheme.xaml` ; aucun hexa dans
   le XAML ou le code-behind de la vue.
4. **Typographie** — styles nommés du thème uniquement (`PageTitleText`, `LabelText`…) ;
   `FontFamily="{StaticResource AppFontFamily}"` posé sur le UserControl.
5. **Structure** — en-tête titre + sous-titre + actions à droite ; cartes `CardBorder` /
   `SubtleCardBorder` ; sous-onglets `SectionTabControl` uniquement si plusieurs sections
   indépendantes (référence TreasuryView).
6. **`RunAsync` partout** — aucun `try/catch` réseau maison ; aucune action bouton qui appelle
   l'API hors `context.RunAsync`.
7. **Permissions symétriques** — actions d'écriture grisées sans le droit, info-bulle posée
   **et restaurée** via le motif `ApplyPermissionHint` / `SetActionState` ; champs en lecture
   seule (pas désactivés) quand la valeur doit rester copiable.
8. **Confirmations** — actes engageants confirmés par le gabarit `Confirm` : fenêtre
   propriétaire, `MessageBoxImage.Warning`, **défaut sur Non**, message qui nomme l'objet et dit
   ce qui devient irréversible.
9. **Actions motivées** — toute annulation/réouverture/purge exige un motif obligatoire, rappelé
   dans la confirmation et tracé côté serveur.
10. **`MaxLength` et bornes** — chaque champ borné comme le domaine/la colonne ; contrôles
    numériques (négatifs, décimales, capacité) avant l'envoi, avec messages explicites.
11. **État vide** — chaque grille superpose pictogramme + titre + indice d'action,
    `IsHitTestVisible="False"`, déclenché sur `Items.Count == 0`.
12. **Badges** — statuts en pastilles `Status…` du thème, libellés français d'une source unique
    par énumération (même mot à l'écran, à l'impression, au CSV).
13. **Montants** — `N2` culture courante alignés à droite (`AmountCellText` +
    `RightAlignedColumnHeader`), `N3` pour les quantités de facturation ; saisie en
    `AmountTextBox` ; `Language` de la vue réglée sur la culture courante si des `StringFormat`
    sont utilisés.
14. **Heure locale** — aucun horodatage UTC brut à l'écran ; `UtcToLocalTimeConverter` ou
    `ToLocalTime()` systématiques ; bornes de calendrier sur `DateTime.Today`.
15. **Chiffres du serveur** — totaux et agrégats renvoyés par l'API ; tout aperçu local est
    marqué comme indicatif ; bandeau d'avertissement si un chiffre ne répond pas au filtre
    visible.
16. **Miroir, pas promesse** — aucune règle affichée qui diffère de celle du serveur ; les
    constantes métier sont référencées depuis le domaine, jamais recopiées.
17. **Sélection restaurée** — après rechargement, la ligne sélectionnée est retrouvée par sa clé
    stable, ou le formulaire repart proprement en création si elle a disparu.
18. **Secrets** — un secret affiché une fois ne dépend d'aucun appel ultérieur, se copie avec
    exclusion de l'historique du presse-papiers, s'efface à tout changement de contexte et à la
    déconnexion.
19. **Français accentué** — tous les libellés, info-bulles, messages et en-têtes d'export en
    français correct (accents, « », espaces avant `? :`) ; commentaires de code sans accents.
20. **Retour d'information** — succès et erreurs via `SetStatus` (jamais de `MessageBox`
    d'information) ; boutons Actualiser avec message de confirmation du rechargement.

---

## Fichiers de référence

- `src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` — source de vérité : palette, typographie,
  styles de contrôles, badges, cartes, carte module, états vides, info-bulles.
- `src/RaqmiSystem.Desktop/MainWindow.xaml` — coquille : en-tête, sidebar, écran de connexion,
  accueil des 49 modules, onglets historiques (Unités, Recettes, Tableau de bord, Audit).
- `src/RaqmiSystem.Desktop/MainWindow.xaml.cs` — navigation, chargement paresseux,
  `RunApiActionAsync`, `SetBusy`, `SetStatus`, permissions de modules.
- `src/RaqmiSystem.Desktop/Views/ModuleViewContext.cs` — le contrat prêté aux vues.
- `src/RaqmiSystem.Desktop/Views/CustomersView.xaml(.cs)` — gabarit d'écran fichier + formulaire.
- `src/RaqmiSystem.Desktop/Views/InvoicesView.xaml(.cs)` — détail riche, cycle de vie, bornes du
  domaine, totaux serveur.
- `src/RaqmiSystem.Desktop/Views/TreasuryView.xaml(.cs)` — sous-onglets `SectionTabControl`,
  `ApplyPermissionHint`, bandeau d'avertissement de périmètre.
- `src/RaqmiSystem.Desktop/Views/ClosingView.xaml(.cs)` — actes engageants datés,
  `UtcToLocalTimeConverter`.
- `src/RaqmiSystem.Desktop/Views/SettingsView.xaml(.cs)` — lecture seule sous permission, purge
  confirmée, `ApplyPermissionHint`.
- `src/RaqmiSystem.Desktop/Views/UsersView.xaml(.cs)` — protocole des secrets, `SetActionState`,
  garde-fous miroir du serveur.
- `src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.cs` — les 22 domaines
  fonctionnels : identifiants stables, icône, maturité, rattachement des 50 entrées historiques.
- `src/RaqmiSystem.Desktop/ModuleCatalog.cs` — catalogue transitoire des 50 entrées et constantes
  de garde.
- `src/RaqmiSystem.Desktop/ThemePalette.cs` — la palette sombre, clé par clé (82 brushes).
- `src/RaqmiSystem.Desktop/DailyRevenueStatusDisplay.cs` — libellés français à source unique.
- `docs/design/navigation-shell.md` — spécification du shell à quatre niveaux (barre latérale, fil
  d'Ariane, sous-onglets, accueil, badges, états vides, accessibilité, profils).
- `docs/design/icones-domaines.md` — inventaire des icônes des 22 domaines.
- `docs/design/maquette-shell.html` — maquette autonome pour validation (deux profils, deux thèmes).
- `src/RaqmiSystem.Desktop/CsvExportHelper.cs` — exports CSV : UTF-8 BOM, culture invariante,
  anti-injection de formule.
- `assets/brand/raqmi-system/README.md` — identité de marque (symbole, palette, typographies,
  règles d'usage du logo).
