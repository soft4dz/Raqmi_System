# Refonte de la barre latérale — décision finale « Panneau plat, présent partout »

> **Statut** : décision de design, prête à chiffrer. Version du 07/09/2026 (révision après relecture adverse).
> **Maquette interactive** : `docs/design/navigation/maquette-barre-laterale.html` (Avant / Après, profils Directeur d'unité et Administrateur, écran de module et Mon Espace, densité compacte, thème sombre).
> **Sources de vérité** : `src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` (jetons), `src/RaqmiSystem.Desktop/ThemePalette.cs` (thème sombre), `src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog*.cs` (arbre), `src/RaqmiSystem.Infrastructure/Security/SecuritySeeder.cs` (clés des rôles), `docs/design/navigation-shell.md` (règles du shell), `docs/design/accueil/refonte-accueil.md` (accueil refondu), `docs/design/icones-domaines.md` (icônes).

---

## L'essentiel (à lire en cinq minutes)

**La phrase** : « la barre latérale ne me plaisait pas ». Rien d'autre. Le code a été lu pour trouver pourquoi.

**Les trois causes principales** (preuves au § 2) :
1. **Elle disparaît sur l'accueil.** Sur « Mon Espace », l'écran le plus visité, la barre est repliée à zéro et le contenu saute de 268 px sans transition ; elle réapparaît sur tout autre écran. La justification d'origine (« le sommaire EST l'accueil ») n'est plus vraie : l'accueil s'ouvre sur « Mon travail », pas sur le catalogue.
2. **Les noms sont coupés.** ≈ 123 px utiles pour un libellé : 10 domaines sur 15 finissent en « … », « Administration & S… » et « Administration Sy… » sont indiscernables, sans info-bulle.
3. **Trois niveaux pour un seul écran.** « Stocks & Économat › Stocks › Stocks & consommations » : un libellé de module mort entre le domaine et l'écran, un badge « 1 » sur presque chaque ligne.

Et derrière : une carte blanche flottante à la place d'un chrome, un focus clavier invisible (1,11:1), « Mon Espace » en double.

**La décision, en cinq points** :
- Un **panneau de 260 px, collé au bord gauche, pleine hauteur, sans arrondi**, séparé du contenu par un filet ; il **ne disparaît jamais**, Mon Espace compris.
- **Deux niveaux : Domaine › Écran.** Le libellé de module ne subsiste que comme séparateur quand un module regroupe au moins deux écrans. Le fil d'Ariane garde ses quatre niveaux.
- **Trois strates** : « Mon Espace » et ses écrans en tête, la recherche, la liste défilante des domaines ; « Administration Système » épinglée en pied **pour les profils qui y ont un écran**.
- **Un libellé court par domaine** porté par le catalogue (« Admin & Socle ERP », « Groupes & MICE », « Revenue Management »), nom complet en info-bulle, dans le fil d'Ariane et pour les lecteurs d'écran ; deux lignes plutôt qu'une ellipse.
- **Des états qui se voient** : anneau de focus 2 px, écran actif = filet + fond + graisse, domaine contenant l'écran actif = icône en accent, compteur seulement en recherche, état déplié mémorisé par poste, densité compacte enfin appliquée.

**Avant / Après** (maquette : boutons « Avant » / « Après », puis « Mon Espace ») :

```
AVANT — Facturation                AVANT — Mon Espace               APRÈS — Facturation           APRÈS — Mon Espace
┌────────┐┌─────────────────┐      ┌──────────────────────────┐     ┌────────┬────────────────┐   ┌────────┬────────────────┐
│ MODULES││ 05 → … → Factur.│      │ [Mon travail] Catalogue  │     │⌂ Mon E.│05 › … › Factur.│   │┃⌂ Mon E│                │
│⌂ Mon E.││┌───────────────┐│      │ Bonjour, Samir           │     │ Workfl.│┌──────────────┐│   │ Workfl.│[Mon travail]   │
│[⌕ Rech.]││ Factures      ││      │ EN RETARD (5 cartes/rang)│     │[⌕ Rech.]││ Factures     ││   │[⌕ Rech.]│Bonjour, Samir  │
│▸ Mon Es││               ││      │ ▢ ▢ ▢ ▢ ▢                │     │▸ Admin.││              ││   │▸ Admin.│EN RETARD       │
│▸ Financ││               ││  →   │ AUJOURD'HUI              │     │▸ Financ││              ││   │▸ Financ│▢ ▢ ▢ ▢         │
│▾ Factur││               ││      │ ▢ ▢ ▢ ▢ ▢                │     │▾ Factur││              ││   │▾ Factur│▢               │
│  DOC. V││               ││      │                          │     │┃ Factur││              ││   │  Factur│AUJOURD'HUI     │
│  Factur││               ││      │  (la barre a disparu :   │     │▸ PMS   ││              ││   │▸ PMS   │▢ ▢ ▢ ▢         │
│▸ PMS   ││               ││      │   saut de 268 px)        │     │▸ …     ││              ││   │▸ …     │                │
└────────┘└───────────────┘┘      └──────────────────────────┘     └────────┴────────────────┘   └────────┴────────────────┘
 carte blanche 248 px, arrondie      colonnes à 0, fil d'Ariane      panneau 260 px, filet, deux     rien ne bouge ; seule la
 « Mon Espace » deux fois            masqué, contenu remonté         niveaux, focus visible          rangée active change
```

**Le coût** : **8,25 jours** (dont 0,5 j de test clavier et Narrateur), aucune fonctionnalité retirée (annexe A.5). **Le prix visible** : l'accueil perd 260 px ; la grille des files de travail passe de **5 à 4 cartes par rangée** à 1280 comme à 1366 ; la bande « En retard » du Directeur d'unité (5 files) et « Aujourd'hui » (jusqu'à 21 cartes chez l'administrateur) gagnent une rangée (≈ 168 px). Cette décision renverse « barre repliée sur l'accueil » consignée dans `navigation-shell.md` § 5.2 et `refonte-accueil.md` § 1.1, § 1.3, § 2.1, § 5.2 (voir § 3.7).

**Les quatre questions à trancher** (§ 4.3) : (1) les libellés courts « Admin & Socle ERP », « RH & Paie », « Maintenance » ; (2) Ctrl+K sur l'accueil : recherche de la barre ou du catalogue ; (3) le « Journal d'audit » du domaine 15 ; (4) **accepter 4 cartes par rangée sur l'accueil** contre une barre stable partout.

---

## 1. Contexte

Quatre propositions ont été instruites (A rail + volet, B arbre plat, C shell sombre, D domaine actif) et jugées par trois lentilles (utilisateur métier, système de design et accessibilité, faisabilité WPF et risque produit). Classement cumulé : **B 22**, A 21,5, C 17, D 16,5. Ce document part de B, y greffe ce que le jury a retenu des trois autres et respecte tous les vetos. Les pistes écartées et leurs raisons sont en annexe A.2.

---

## 2. Diagnostic : les cinq causes vérifiées dans le code

Seize problèmes ont été relevés et vérifiés (les onze autres, P06 à P16, sont en annexe A.1). Les cinq premiers expliquent, à eux seuls, la phrase du propriétaire.

| Id | Problème prouvé | Preuve (fichier:ligne) | Gravité |
|---|---|---|---|
| P01 | La barre est **repliée à zéro sur l'accueil** et réapparaît ailleurs : le contenu saute de 268 px vers la gauche et d'≈ 26 px vers le haut, sans transition, dix fois par jour (Alt+Origine, bouton Mon Espace, cartes, repli de sécurité). | `MainWindow.Navigation.cs:204-206` (Collapsed + `GridLength(0)`), constantes 248 + 20 l. 53-54, seul fondu : `MainTabs` l. 291-296, fil d'Ariane masqué l. 222/258, aveu « on y revient dix fois par jour » l. 316 | bloquant |
| P02 | La justification du repli (« la racine EST le sommaire ») est **caduque** : l'onglet 0 s'ouvre sur « Mon travail », le catalogue n'est qu'une seconde section non mémorisée. Sur l'écran le plus visité, aucun chemin visible vers un module. | `HomeView.xaml:19-26`, `MainWindow.xaml:431-434` (commentaire périmé), `MainWindow.Shortcuts.cs:108-113` (Ctrl+K détourné) | bloquant |
| P03 | La barre est dessinée comme une **carte de contenu flottante** (CardBorder, coins 10 px, marge 24) et non comme du chrome ; rupture avec l'en-tête #071525. | `MainWindow.xaml:449-452`, `:421` (Margin 24), `RaqmiTheme.xaml:260-266` | majeur |
| P04 | **Noms tronqués** (≈ 123 px utiles, ~18 caractères) sans info-bulle : 10 des 15 domaines affichables dépassent ; « Administration & S… » et « Administration Sy… » indiscernables. | `RaqmiTheme.xaml:1427, 1444-1452, 1486-1515` (largeurs), `:1497-1500` (CharacterEllipsis, pas de ToolTip) ; `FunctionalArchitectureCatalog.cs:42-62` | majeur |
| P05 | **Trois niveaux pour des branches à un seul écran** : libellé de module mort, même mot répété (« Stocks & Économat › Stocks › Stocks & consommations »). | `MainWindow.xaml:79-82`, `RaqmiTheme.xaml:247-251`, `FunctionalArchitectureCatalog.Tree.cs:268-270, 279-281, 301-303` | majeur |

**Ce qui marche et qu'on garde** : source unique (arbre élagué par `NavigationTreeBuilder`, aucun bouton en dur), chemin de navigation unique `NavigateToModule` gardé par `CanOpenModule`, recherche normalisée avec Échap et état vide, groupes non reconstruits (`Apply`), Administration épinglée, ToggleButton pleine ligne, écran verrouillé désactivé avec info-bulle `ShowOnDisabled`, fil d'Ariane annoncé au lecteur d'écran, palette documentée et thème sombre par jetons.

---

## 3. Proposition finale

### 3.1 Concept en cinq points

1. **La barre devient du chrome, pas du contenu.** Panneau `SurfaceBrush` de **260 px**, collé au bord gauche, pleine hauteur sous l'en-tête bleu nuit, sans arrondi, séparé du corps par un seul filet vertical `BorderStrongBrush`. Il **ne disparaît jamais**, Mon Espace compris : les colonnes de la grille ne sont plus touchées après le chargement (P01, P02, P03).
2. **Deux niveaux : Domaine › Écran.** Un en-tête de domaine (icône 16 px + libellé court + chevron) et, déplié, ses écrans ouvrables à plat. Le libellé de module ne survit que comme **séparateur** de 11 px, et seulement pour un module qui regroupe **au moins deux écrans** (Pilotage › DASHBOARDS › 3 écrans ; Administration Système › MAINTENANCE › 2 écrans). Finance chez l'administrateur : 6 lignes au lieu de 6 titres + 6 lignes (P05, P06). Le fil d'Ariane conserve les quatre niveaux.
3. **Trois strates fixes, une seule qui défile.** En tête : « Mon Espace » (= domaine 01, avec ses écrans réels dessous : plus de doublon), puis la recherche ; au centre la liste défilante des domaines 02 → 21 ; en pied « Administration Système » épinglée, **présente seulement pour les profils qui y détiennent un écran** (`maintenance.read`, `audit.read` ou `sync.read` : administrateur, pas Directeur d'unité) (P08).
4. **Un libellé court par domaine, porté par le catalogue** (`ShortLabel` ≤ 22 caractères, **contraction du nom officiel**, jamais un renommage), utilisé par la barre et par les puces de domaine du catalogue de l'accueil. Le nom complet reste dans le fil d'Ariane, l'info-bulle, le nom UIA et les cartes. Les libellés qui débordent encore passent sur **deux lignes** au lieu d'une ellipse (P04, P13, P15).
5. **Des états qui se voient.** Focus clavier = anneau 2 px `FocusRingBrush` (jamais le fond) ; survol = `SurfaceHoverBrush` ; écran actif = fond `AccentSoftBrush` + filet 3 px `AccentActionBrush` + texte `PrimaryBrush` semi-gras ; le domaine qui contient l'écran affiché porte son icône en **trait** `AccentActionBrush` et, s'il est replié, un point ; compteur uniquement en recherche ; info-bulle seulement si verrouillé ou tronqué ; état déplié mémorisé par poste ; densité compacte via `ThemeManager.AppliquerDensite` (P07, P09, P10, P11, P12).

### 3.2 Wireframes (1280 × 800, profil Directeur d'unité, densité confortable)

Profil = rôle `unit.manager` tel que seedé (`SecuritySeeder.cs` l. 193-260, clés de lecture : `units.read`, `revenue.read`, `dashboard.read`, `closing.read`, `customers.read`, `invoices.read`, `settings.read`, `budget.read`, `tariffs.read`, `lodging.read`, `housekeeping.read`, `crm.read`, `approvals.read`, `reports.read`, `inventory.read`, `purchasing.read`, `kitchen.read`, `mice.read`). Après élagage par `NavigationTreeBuilder` (les alias ne sont pas des rangées, `ModuleNavigationGroup.cs:90`) : **12 domaines dans la liste** (02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 20 ; Finance = Budget + CA journalier seulement), l'écran « Workflows & validations » sous Mon Espace, **aucun pied** (ni `maintenance.read`, ni `audit.read`, ni `sync.read` ; ni `hr.read` : pas de RH). 1 caractère ≈ 11 px.

**Écran de module — 05 Facturation & Ventes › Facturation (onglet 8)**

```
x=0                     x=260                                                             x=1280
┌───────────────────────┬─────────────────────────────────────────────────────────────────────┐ y=0
│ ▣ Raqmi System        │                              ALG-CEN · Hôtel Riadh   Samir M. ▾   ⏻  │ en-tête 76 px  StructureBrush #071525
├───────────────────────┼─────────────────────────────────────────────────────────────────────┤ y=76
│                       │  05 Facturation & Ventes → Documents de vente → Factures → Facturation│ fil d'Ariane : ligne réservée 24 px
│ ⌂  Mon Espace         │                                                                     │ (Margin 24 sur la colonne de contenu)
│     Workflows & valid.│ ┌─────────────────────────────────────────────────────────────────┐ │ y≈124
│ ───────────────────── │ │  Factures                                     [ + Nouvelle facture ]│ │ CardBorder inchangée
│ [⌕ Rechercher un écran│ │  Unité ▾   Période ▾   Statut ▾                                   │ │
│ ───────────────────── │ │  N°           Client                Date       HT       TTC  Statut│ │
│ ▸ ⬡ Admin & Socle ERP │ │  F-2026-0412  SARL Numidia          06/09/26  120 000  142 800 Émise│ │
│ ▸ ⬡ Finance & Comptab.│ │  F-2026-0411  EURL Tassili Tours    06/09/26   48 500   57 715 Payée│ │
│ ▸ ⬡ Commercial & CRM  │ │  F-2026-0410  Hôtel Riadh Ouest     05/09/26   12 000   14 280 Brou.│ │
│ ▾ ◆ Facturation & Vent│ │  …                                                              │ │ ◆ = icône en trait AccentAction :
│ ┃    Facturation      │ │                                                                 │ │ domaine de l'écran affiché
│ ▸ ⬡ PMS / Hébergement │ │                                                                 │ │ ┃ = ligne active : filet 3 px AccentAction,
│ ▸ ⬡ Revenue Management│ │                                                                 │ │ fond AccentSoft, texte Primary semi-gras
│ ▸ ⬡ Housekeeping      │ │                                                                 │ │
│ ▸ ⬡ Groupes & MICE    │ │                                                                 │ │
│ ▸ ⬡ F&B / Restauration│ │                                                                 │ │ 12 × 36 + 1 × 34 + 8 = 474 px
│ ▸ ⬡ Stocks & Économat │ │                                                                 │ │ sur 548 disponibles :
│ ▸ ⬡ Achats & Fourniss.│ │                                                                 │ │ pas de défilement
│ ▸ ⬡ Pilotage, KPI & BI│ │                                                                 │ │
│                       │ └─────────────────────────────────────────────────────────────────┘ │
│                       │  Session : Samir Merzouk · Directeur d'unité · ALG-CEN · 06/09/2026 │ bandeau de session, colonne contenu
└───────────────────────┴─────────────────────────────────────────────────────────────────────┘ y=800
        ↑ filet 1 px BorderStrongBrush du bas de l'en-tête au bas de la fenêtre ; contenu utile 1280 − 260 − 48 = 972 px
        (pas de pied « Administration Système » pour ce profil : la liste occupe tout le bas du panneau)
```

Zoom sur les rangées :

```
Domaine (36 px, pleine largeur 243, rayon 6, Padding 10,0)      Écran (34 px, indentation 36)
┌──────────────────────────────────────────────────┐             ┌──────────────────────────────────────────────────┐
│ [10] ⬡ [10] Finance & Comptabilité         •  ▸  │             │┃      Budget & prévisions                         │
└──────────────────────────────────────────────────┘             └──────────────────────────────────────────────────┘
  icône 16 px trait 1,5 TextSecondary · libellé 13 px SemiBold     ┃ = filet 3 px AccentAction · fond AccentSoft
  TextPrimary · point 6 px AccentAction (replié + contient           texte 13 px SemiBold PrimaryBrush
  l'écran actif) · chevron 9×7 TextMuted (▸ replié, ▾ déplié)

Domaine à module multi-écrans (Pilotage, KPI & BI déplié)      Pied — profil Administrateur seulement (22 déplié)
│ ▾ ⬡ Pilotage, KPI & BI            │                            │ ───────────────────────────────── │ ← filet PanelBorder
│      DASHBOARDS                   │ ← séparateur 11 px,        │ ▾ ⬡ Administration Système        │
│      Tableaux de bord             │   majuscules, TextLabel,   │      MAINTENANCE                  │ ← 2 écrans : séparateur
│      directionnels                │   24 px, non cliquable     │      Sauvegarde & restauration    │
│      Dashboard PDG                │                            │      Journalisation & traçabilité │
│      Cockpit DEC                  │                            │      Registre des postes &        │ ← module à 1 écran :
│      Comparatif inter-unités      │ ← modules à 1 écran :      │      erreurs clients              │   pas de séparateur,
│      Rapports automatiques        │   à plat, sans séparateur  │                                   │   2 lignes (44 px)
```

**Mon Espace (onglet 0, section « Mon travail »)** — barre identique, seule la rangée active change.

```
x=0                     x=260                                                             x=1280
┌───────────────────────┬─────────────────────────────────────────────────────────────────────┐ y=0
│ ▣ Raqmi System        │                              ALG-CEN · Hôtel Riadh   Samir M. ▾   ⏻  │ 76 px
├───────────────────────┼─────────────────────────────────────────────────────────────────────┤ y=76
│                       │  (ligne du fil d'Ariane réservée : Visibility=Hidden, 24 px)        │ pas de saut vertical
│┃⌂  Mon Espace         │ ┌─────────────────────────────────────────────────────────────────┐ │
│     Workflows & valid.│ │  [ Mon travail ]   Catalogue des modules                        │ │ HomeView inchangée
│ ───────────────────── │ │  Bonjour, Samir Merzouk            Unité du poste ALG-CEN · Hôtel │ │
│ [⌕ Rechercher un écran│ │  lundi 7 septembre 2026            Date métier 06/09/2026 [à jour]│ │
│ ───────────────────── │ ├─────────────────────────────────────────────────────────────────┤ │
│ ▸ ⬡ Admin & Socle ERP │ │  ● EN RETARD · 5 files                                           │ │
│ ▸ ⬡ Finance & Comptab.│ │  ┌ Arrivées en ret. ┐┌ Départ en retard ┐┌ Journée à clôt. ┐┌ Journées non clô.┐│ 4 cartes de 222 px + 3 × 12
│ ▸ ⬡ Commercial & CRM  │ │  └──────────────────┘└──────────────────┘└─────────────────┘└─────────────────┘│ = 924 px ≤ 932 px utiles
│ ▸ ⬡ Facturation & Vent│ │  ┌ Recette rejetée  ┐                                             │ │ ← 5e carte : seconde rangée
│ ▸ ⬡ PMS / Hébergement │ │  └──────────────────┘                                             │ │   (+ ≈ 168 px)
│ ▸ ⬡ Revenue Management│ │  ● AUJOURD'HUI · 16 files                                        │ │
│ ▸ ⬡ Housekeeping      │ │  ┌ Arrivées 14 ┐┌ Sans chambre 3 ┐┌ Départs 9 ┐┌ Départs solde 2 ┐│ │
│ ▸ ⬡ Groupes & MICE    │ │  └─────────────┘└────────────────┘└───────────┘└─────────────────┘│ │
│ ▸ ⬡ F&B / Restauration│ │  ┌ Chambres à prép. 6 ┐┌ Validations 2 ┐ …                       │ │
│ ▸ ⬡ Stocks & Économat │ │  ● À SURVEILLER · 2 files                                        │ │
│ ▸ ⬡ Achats & Fourniss.│ │  ┌ Chambre HS 1 ┐┌ Articles sous min. 5 ┐                        │ │
│ ▸ ⬡ Pilotage, KPI & BI│ │  Derniers écrans ouverts : Facturation · PMS front office · …    │ │
│                       │ └─────────────────────────────────────────────────────────────────┘ │
│                       │  Session : Samir Merzouk · Directeur d'unité · ALG-CEN · 06/09/2026 │
└───────────────────────┴─────────────────────────────────────────────────────────────────────┘
 ┃⌂ Mon Espace : filet AccentAction + fond AccentSoft + icône en trait AccentAction + texte Primary semi-gras :
 exactement le langage de la rangée d'écran active, puisque c'est l'écran 0. Aucune colonne ne change de largeur.
 Prix : 5 cartes par rangée dans le wireframe validé de refonte-accueil.md § 2.1 (1232 px), 4 ici (972 px).
```

### 3.3 Structure

| Strate | Contenu | Défile ? |
|---|---|---|
| **A. Mon Espace** | Rangée de 40 px (domaine 01, navigue vers l'onglet 0, pas de chevron : toujours ouvert), suivie des écrans ouvrables du domaine 01 en rangées de 34 px (« Workflows & validations » si `approvals.read`). Le groupe 01 n'est **plus** injecté dans la liste. | non |
| **B. Recherche** | `ModuleSearchTextBox` inchangé, placeholder « Rechercher un écran… », 34 px. | non |
| **C. Domaines 02 → 21** | Un ToggleButton de 36 px par domaine `HasMatches`, ordre du catalogue. Déplié : rangées d'écran de 34 px indentées de 36 px. **Règle d'aplatissement** (projection calculée dans `Apply`, aucune règle métier) : un module visible à **un** écran → une rangée d'écran, sans libellé ; un module visible à **deux écrans ou plus** → séparateur de 24 px (11 px majuscules, non cliquable) puis ses écrans. Le sous-module n'est jamais affiché (il vit dans le fil d'Ariane). | oui (seule) |
| **D. Pied** | Filet, puis « Administration Système » (domaine 22, `IsPinned`), même gabarit qu'un domaine ; toujours visible sans défiler. **Absent** (`HasMatches` faux) pour les profils sans écran du domaine 22, dont le Directeur d'unité. Le filet se masque quand la recherche ne retient rien (comportement actuel). | non |

Le fil d'Ariane garde ses quatre segments Domaine › Module › Sous-module › Écran : c'est lui qui porte le niveau module que la barre n'affiche plus que comme séparateur. Barre = vue compacte, catalogue de l'accueil = vue riche, fil d'Ariane = position exacte : trois formes du même arbre, une seule source.

### 3.4 Dimensions et typographie

| Élément | Valeur |
|---|---|
| Colonne | `SidebarColumn` = **260** fixe ; `SidebarGapColumn` **supprimée** (la grille passe à **deux colonnes** : toutes les références `Grid.Column="2"` de la grille principale — `BreadcrumbBorder` l. 565, `MainTabs` l. 578 — deviennent `Grid.Column="1"`) ; `MainContentGrid` Margin 0, la marge 24 passe sur la colonne de contenu ; `SidebarBorder` `Grid.RowSpan=3` (fil d'Ariane, contenu, bandeau) ; **le bandeau de session** (`MainWindow.xaml:1577`, aujourd'hui `Grid.Row=2 Grid.ColumnSpan=3`, donc sous la colonne de la barre) passe en `Grid.Column="1"` **sans `ColumnSpan`**, sinon il se superposerait au panneau |
| Panneau | Background `SidebarBackgroundBrush`, BorderThickness `0,0,1,0`, CornerRadius 0, Padding `8,12,8,12` → 243 px utiles |
| Mon Espace | 40 px, Padding 10,0, icône 16 px, gap 10, texte 13 px SemiBold ; filet 1 px `PanelBorderBrush` Margin 4,8 |
| Recherche | 34 px, Margin 4,0,4,10, Padding 30,0,26,0 (loupe 14 px, croix 18 px) → ≈ 180 px de saisie utile (contre ≈ 154) |
| Rangée de domaine | **36 px** confortable / **32 px** compact (`SidebarDomainRowHeight`), rayon 6, Padding 10,0, icône 16 × 16 `Stretch=None` trait 1,5, gap 10, libellé 13 px SemiBold `TextWrapping` + `MaxLines 2` (rangée en `MinHeight`), chevron 9 × 7 Margin 8,0,0,0, point 6 px Margin 0,0,6,0. Largeur utile du libellé : 243 − 20 − 26 − 17 = **180 px** (170 avec barre de défilement) ≈ 24 caractères à 13 px : tous les libellés courts (≤ 22) tiennent sur une ligne |
| Rangée d'écran | **34 px** confortable / **32 px** compact (`SidebarScreenRowHeight`, jamais sous 32), indentation 36, Padding 10,0, texte 13 px Regular `TextWrapping` `MaxLines 2` ; largeur utile ≈ **187 px** ≈ 27 caractères ; « Registre des postes & erreurs clients » passe sur 2 lignes (44 px) sans info-bulle |
| Séparateur de module | 24 px (22 compact), 11 px SemiBold majuscules, indentation 36, Margin haut 6, `IsHitTestVisible=False`, non focusable |
| Filet actif | 3 px, hauteur = rangée − 12, RadiusX 1,5, collé au bord gauche de la rangée |
| Anneau de focus | 2 px, inset 1 px, rayon 6 |
| Pied | Filet 1 px `PanelBorderBrush` Margin 4,8, rangée de domaine 36 px |
| Strates fixes | Padding 24 + Mon Espace 40 + écran 01 34 + filet 17 + recherche 44 + filet 17 = **176 px** sans pied ; + filet 17 + rangée 36 = **229 px** avec pied (administrateur) |
| Budget vertical à 1280 × 800 (724 px sous l'en-tête) | **Directeur d'unité** : 724 − 176 = **548 px** de liste ; 12 domaines repliés = 432 px ; Finance déplié en plus (2 écrans) = 510 px : **rien ne défile**, même en confortable. **Administrateur** (13 domaines dans la liste + pied) : 724 − 229 = **495 px** ; 13 × 36 = 468 px repliés : rien ne défile ; Finance déplié (6 écrans) = 684 px : défile (compact : 13 × 32 + 6 × 32 + 6 = 614 sur 497) |
| Budget vertical à 1366 × 768 (692 px sous l'en-tête) | Directeur d'unité : 692 − 176 = **516 px** ≥ 510 : rien ne défile. Administrateur : 692 − 229 = **463 px** < 468 : 5 px de défilement replié en confortable, **rien en compact** (416 px) |
| Contenu | 972 px utiles à 1280 (1232 aujourd'hui sur l'accueil), **1058 px à 1366** (1318 aujourd'hui) ; `WorkQueuesView` (cartes 222 + 12) passe de **5 à 4 cartes par rangée** dans les deux largeurs (5 × 222 + 4 × 12 = 1158 > 1018 px utiles dans la carte à 1366) |
| Polices | **Deux tailles** dans la barre : 13 px (tout ce qui se clique) et 11 px (séparateurs) ; `AppFontFamily`. Le libellé « Modules » et le badge permanent disparaissent |

### 3.5 Libellés courts (`ShortLabel`, nouvelle donnée du catalogue)

Règle testée : ≤ 22 caractères, unique, chaque mot est un mot du libellé officiel, un préfixe d'au moins quatre lettres d'un de ses mots (« Admin »), ou un sigle déclaré (RH = Ressources Humaines). Jamais un synonyme.

| Id | Libellé officiel | `ShortLabel` | Id | Libellé officiel | `ShortLabel` |
|---|---|---|---|---|---|
| 01 | Mon Espace | Mon Espace | 12 | Achats & Fournisseurs | Achats & Fournisseurs |
| 02 | Administration & Socle ERP | **Admin & Socle ERP** | 13 | Ressources Humaines & Paie | **RH & Paie** |
| 03 | Finance & Comptabilité | Finance & Comptabilité | 14 | Maintenance & Patrimoine | **Maintenance** |
| 04 | Commercial, Clients & CRM | **Commercial & CRM** | 15 | Qualité, Audit & Contrôle interne | **Qualité & Audit** |
| 05 | Facturation & Ventes | Facturation & Ventes | 16 | Juridique & Conformité | Juridique & Conformité |
| 06 | PMS / Hébergement | PMS / Hébergement | 17 | GED / Gestion documentaire | **GED / Documentaire** |
| 07 | Revenue Management & Distribution | **Revenue Management** | 18 | PortMaster / Marina | PortMaster / Marina |
| 08 | Housekeeping | Housekeeping | 19 | Parking & Contrôle d'accès | **Parking & Accès** |
| 09 | Groupes, MICE & Événementiel | **Groupes & MICE** | 20 | Pilotage, KPI & BI | Pilotage, KPI & BI |
| 10 | F&B / Restauration | F&B / Restauration | 21 | Intégrations & Matériels | **Intégrations** |
| 11 | Stocks & Économat | Stocks & Économat | 22 | Administration Système | Administration Système |

Douze domaines gardent leur nom entier. Le nom complet est toujours à un survol (info-bulle de l'en-tête, aussi au focus clavier), dans le nom UIA, dans le fil d'Ariane et sur les cartes. Le numéro de domaine (P13) figure dans l'info-bulle (« 06 · PMS / Hébergement »), le nom UIA et le fil d'Ariane, pas sur la rangée.

### 3.6 États et interactions

| État | Domaine | Écran |
|---|---|---|
| Repos | fond transparent, icône trait `SidebarIconBrush`, libellé 13 px SemiBold `SidebarTextBrush`, chevron ▸ `TextMutedBrush` | texte 13 px Regular `SidebarScreenTextBrush`, pas de filet, pas d'info-bulle |
| Survol | fond `SidebarHoverBrush` pleine largeur, curseur main ; info-bulle **seulement** si libellé tronqué (nom complet + numéro) ou verrouillé, délai 600 ms | fond `SidebarHoverBrush`, texte → `TextPrimaryBrush` |
| Appui | même fond, aucune translation | idem |
| Déplié | chevron ▾, rangées d'écran visibles, marge basse 6 px | — |
| **Actif** (écran affiché ; Mon Espace sur l'onglet 0) | icône trait `SidebarActiveIndicatorBrush` (**Stroke, jamais Fill** : les 22 géométries sont des tracés ouverts) | filet 3 px `SidebarActiveIndicatorBrush` + fond `SidebarActiveBackgroundBrush` + texte `SidebarActiveForegroundBrush` SemiBold : triple signal |
| Domaine replié contenant l'écran actif (P12) | icône trait `SidebarActiveIndicatorBrush` + **point 6 px** à gauche du chevron | — |
| Focus clavier | anneau 2 px `SidebarFocusRingBrush`, fond inchangé — jamais confondu avec le survol | idem |
| Verrouillé — **cas exceptionnel** : en régime permanent, un écran non autorisé n'est **pas dans la barre** (`navigation-shell.md` § 2.1 « aucun cadenas », élagage de `NavigationTreeBuilder`) ; la rangée désactivée n'existe que si la permission est retirée **en cours de session**, après le chargement de l'arbre | — | `IsEnabled=False`, texte `SidebarDisabledForegroundBrush`, cadenas 12 px, `ToolTipService.ShowOnDisabled` avec `Tile.ToolTipText`, dont le texte actuel est **« Accès non autorisé pour votre profil »** (`ModuleTile.AccessDeniedToolTip`, `ModuleTile.cs:15`) ; option retenue dans le plan (point 13) : `ToolTipText` ajoute le libellé de la permission manquante (« Accès non autorisé pour votre profil — permission requise : Lire la comptabilité », libellé de `PermissionCatalog`) ; atteignable par flèches pour que le lecteur d'écran lise le motif. **Écart constaté à la livraison (08/09/2026)** : un contrôle `IsEnabled=False` n'est pas focalisable en WPF, les flèches sautent la rangée ; le motif reste lisible à la souris (`ShowOnDisabled`), en mode balayage (`HelpText`) et sur la carte du catalogue. Le libellé du catalogue est sans accent (« Lire la comptabilite ») |
| Recherche active | domaines sans résultat masqués (`HasMatches`), ceux avec résultat dépliés, pastille « n » à droite du libellé (`ShowResultCount`), séparateurs conservés seulement s'ils encadrent encore ≥ 2 écrans ; strate Mon Espace filtrée (la rangée reste) ; pied masque son filet s'il n'a rien | fragments non surlignés (sobriété) |
| Vide de recherche | « Aucun écran ne correspond. L'accueil liste les 50 modules du catalogue. » `TextMutedBrush` 12 px + bouton fantôme **« Ouvrir le catalogue »** (`NavigateToModule(0)` puis `HomeView.FocusCatalogSearch()`) ; compteur annoncé en `LiveSetting=Polite` | — |
| Densité compacte | 32 / 32 / 22 px, via `DynamicResource` | |

| Geste | Comportement |
|---|---|
| Clic / Espace / Entrée sur un domaine | bascule déplié-replié (`IsExpanded` TwoWay inchangé), **ne navigue pas** ; l'état est écrit dans `DesktopSettings.SidebarExpandedDomains` (écriture différée 500 ms, **jamais pendant une recherche**) |
| Clic / Entrée sur un écran | `ModuleTileNavigate_Click` → `NavigateToModule(tab)` (chemin unique, garde `CanOpenModule`) |
| Clic Mon Espace, **Alt+Origine** | `NavigateToModule(0)` + `ShowWorkSection()` inchangés ; la barre ne bouge pas d'un pixel |
| Clic Administration Système | bascule son dépliage ; un clic + un clic sur l'écran, depuis partout, sans défiler |
| **Ctrl+K** | inchangé : recherche de la barre sur les écrans de module, recherche du catalogue sur l'accueil (question ouverte n° 2) |
| Échap dans le champ | efface sans quitter (inchangé) ; Échap dans la liste ramène le focus au champ |
| **Entrée** dans le champ | ouvre le premier résultat ouvrable (greffe A/C/D) |
| Flèche bas depuis le champ | descend sur la première rangée visible de la liste |
| Tab | un arrêt par strate : Mon Espace → ses écrans → recherche → **liste (un seul arrêt, `KeyboardNavigation.TabNavigation=Once`)** → pied → contenu |
| ↑ / ↓ dans la liste | rangée précédente / suivante (`DirectionalNavigation=Contained`), séparateurs ignorés ; Origine / Fin : première / dernière rangée |
| → / ← sur un domaine | déplie / replie (convention TreeView Windows) ; → sur un domaine déplié descend sur son premier écran ; ← sur un écran remonte à son domaine |
| Ctrl+Page haut / bas | inchangés (module précédent / suivant) ; la barre suit |
| Démarrage | restaure `SidebarExpandedDomains` (identifiants inconnus ou sans `HasMatches` ignorés), puis ouvre en plus le domaine de l'écran courant (`SyncSidebarToTab`, sans replier les autres) |
| Redimensionnement | largeur 260 constante, seule la liste absorbe la hauteur |
| Navigation par tout autre chemin (carte, raccourci, vue) | `SyncSidebarToTab` pose `Tile.IsActive`, déplie le domaine propriétaire (`Owns`, **domaine 22 épinglé compris**) et recalcule `ContainsActiveScreen` sur chaque groupe |

Hors périmètre de ce lot, volontairement : rail réduit Ctrl+B, poignée de redimensionnement, épinglage d'écrans (voir § 5).

### 3.7 Comportement sur l'accueil : une décision renversée, et son prix

La barre est **présente sur Mon Espace, à 260 px, identique à tous les autres écrans** ; seule la rangée « Mon Espace » prend l'état actif. `SyncSidebarToTab` ne touche plus ni `SidebarColumn`, ni `SidebarGapColumn` (supprimée), ni `SidebarBorder.Visibility`. Le fil d'Ariane passe en `Visibility=Hidden` (ligne de 24 px réservée) au lieu de `Collapsed` : zéro saut horizontal, zéro saut vertical.

La décision « barre repliée sur l'accueil » est **renversée**. Elle est consignée à cinq endroits, tous à réécrire (plan, point 12) : `navigation-shell.md` § 5.2 ; `refonte-accueil.md` **§ 1.1** (écarte explicitement « la barre latérale déployée sur l'accueil (Cockpit — contredit navigation-shell.md § 5.2 et retire 268 px) »), **§ 1.3**, **§ 2.1** (wireframe « barre latérale repliée »), **§ 5.2** (« Ce qui ne change pas : … la barre latérale (repliée sur l'onglet 0) ») ; commentaires `MainWindow.xaml:431-434` et `Navigation.cs:176-179`. La même spec accueil impose au **§ 4** « aucun brush nouveau, `ThemePalette.Sombre` reste à 82/82 » : la présente proposition n'ajoute **aucun hex nouveau** mais **14 clés d'alias** `Sidebar*` (annexe A.3), donc `ThemePalette.Sombre` passe de 82 à 96 entrées et `VerifierCouverture` est mis à jour — à consigner au même endroit.

Trois raisons, vérifiées dans le code :

1. Sa justification (« le sommaire des modules EST l'accueil ») ne décrit plus l'écran réel : `HomeView` ouvre toujours « Mon travail », le catalogue est une seconde section non mémorisée ; l'écran le plus visité n'offrait plus aucun chemin visible vers un module (P02).
2. Le repli n'est pas animé et déplace l'écran de 268 px dix fois par jour, ce que le code reconnaît (P01) ; aucun shell ERP de référence (Business Central, Fiori, Odoo, Teams) ne fait disparaître son chrome sur l'accueil.
3. **Le prix est réel et il est dit** : le contenu de l'accueil passe de 1232 à **972 px** à 1280 et de 1318 à **1058 px** à 1366. Le wireframe validé de `refonte-accueil.md` § 2.1 montre **5 cartes de files par rangée** (5 × 222 + 4 × 12 = 1158 px) ; à 972 comme à 1058 px, on passe à **4**. La bande « En retard » du Directeur d'unité (5 files) gagne une rangée (≈ 168 px) ; « Aujourd'hui » (16 files chez lui, jusqu'à 21 cartes chez l'administrateur) gagne une rangée aussi. La première rangée d'« Aujourd'hui » reste visible sans défiler à 800 px de hauteur, plus à 768 en densité confortable. C'est la **question 4** posée au propriétaire ; l'alternative « 248 px » ne change rien (4 cartes aussi), seul le repli sur l'accueil rend les 5 cartes, et il est la cause n° 1 de la phrase.

Le catalogue garde son rôle de vue riche (maturité, descriptions, verrous) ; la barre est la vue compacte du même vocabulaire — ce n'est pas un doublon, c'est la même liste sous deux densités. Ce qui est conservé de la refonte de l'accueil : « Mon Espace » en première ligne, section « Mon travail » à l'ouverture, catalogue à un clic, aucune mémorisation de la section, « Derniers écrans ouverts » **reste dans l'accueil** (pas de « Récents » dans la barre : refonte-accueil § 1.1 l'a écarté).

---

## 4. Coût, risques et questions

### 4.1 Coût

**8,25 jours** (plan détaillé en annexe A.6), dont 0,5 j de test clavier et Narrateur et 0,25 j pour nommer la permission manquante dans l'info-bulle d'une rangée désactivée. Aucune fonctionnalité retirée (annexe A.5).

### 4.2 Risques principaux

| Risque | Parade |
|---|---|
| Largeur de l'accueil : le catalogue et les files perdent 260 px (1232 → 972 à 1280 ; **1318 → 1058 à 1366**) : **5 → 4 cartes par rangée, +1 rangée sur « En retard » (Directeur d'unité) et « Aujourd'hui »** | Assumé et soumis (question 4) ; grille du catalogue à contrôler à 972 et 1058 px (plan, point 10) ; le repli à 248 px ne rend pas la 5e carte |
| Libellés courts : « Admin & Socle ERP », « RH & Paie », « Maintenance » sont des contractions | Question 1 ; nom complet toujours visible à un survol, dans l'UIA, le fil d'Ariane et les cartes |
| Persistance des domaines dépliés : `RefreshSidebar` pose `IsExpanded` pendant une recherche (`Navigation.cs:698`) | Sauvegarde conditionnée à `!isSidebarSearchActive` ; identifiants inconnus ou sans `HasMatches` ignorés au chargement ; sur un comptoir partagé, l'état de l'équipe du matin s'applique au soir (cohérent avec `Densite`, à documenter) |
| Flèches + `TabNavigation=Once` sur un ItemsControl imbriqué : sauts de focus | Handler `PreviewKeyDown` explicite qui calcule la liste des rangées visibles ; demi-journée de test clavier budgétée |
| Grille principale à deux colonnes : bandeau de session, fil d'Ariane et `MainTabs` à redéplacer | Listé point par point au plan (point 4) ; `ColumnSpan=3` sur deux colonnes serait toléré par WPF mais faux |
| Sous 620 px de hauteur de fenêtre, la liste tombe à 3-4 rangées visibles | Défilement conservé, strates fixes intactes ; densité compacte recommandée sur les très petits écrans |
| Répétition résiduelle « Facturation & Ventes › Facturation » pour les domaines à écran unique | Assumée : le domaine nomme le territoire, l'écran la tâche ; deux niveaux, pas trois |

Les autres risques (info-bulle `IsTextTrimmed`, restyle de `ModuleNavSubButton` dans le catalogue, thème sombre) sont en annexe A.6.

### 4.3 Questions ouvertes pour le propriétaire du produit

1. **Libellés courts** : valides-tu la liste du § 3.5, en particulier « Admin & Socle ERP » (02), « RH & Paie » (13) et « Maintenance » (14) ? Alternative sans contraction : renommer le domaine 02 dans le catalogue (« Organisation & Socle ERP », 22 caractères), ce qui règle aussi la confusion avec « Administration Système ».
2. **Ctrl+K sur l'accueil** : maintenant que la barre y est visible, Ctrl+K doit-il focaliser la recherche de la barre partout (un seul comportement, le catalogue restant atteignable par un second Ctrl+K quand le champ a déjà le focus), ou garder l'existant (catalogue sur l'accueil, barre ailleurs) ? Ce lot garde l'existant par défaut.
3. **Journal d'audit** (P16) : faut-il faire du « Journal d'audit » un écran réel du domaine 15 « Qualité, Audit & Contrôle interne » (et un alias de 22), comme `navigation-shell.md` § 2.1 le prescrivait ? C'est une décision d'arbre (`FunctionalArchitectureCatalog.Tree.cs`), hors de la barre, mais c'est dans la barre que le contrôleur le cherchera.
4. **Quatre cartes par rangée sur l'accueil** : acceptes-tu que la grille des files passe de 5 à 4 cartes par rangée (une rangée de plus sur « En retard » et « Aujourd'hui ») en échange d'une barre qui ne disparaît plus ? C'est le seul vrai coût du renversement ; sans ce oui, la décision de fond est à revoir, pas seulement la largeur.

---

## 5. Itération suivante, non bloquante

Si, après usage, la place manque sur les portables 1366 × 768 ou si un profil veut un chrome minimal, la proposition A (rail 64 px + volet) est la candidate naturelle d'un **mode réduit Ctrl+B mémorisé par poste** : le rail reprendrait les icônes 16 px et les `ShortLabel` de ce lot, le volet la projection `Rows`. Rien dans le présent lot ne l'empêche ; rien ne l'exige.

---
---

# Annexe technique

## A.1 Problèmes P06 à P16

| Id | Problème prouvé | Preuve (fichier:ligne) | Gravité |
|---|---|---|---|
| P06 | **Densité insuffisante** : ~370 px de liste à 760 lignes, défilement même repliée pour un administrateur ; densité compacte prescrite (§ 2.4) jamais livrée. | hauteurs codées en dur `RaqmiTheme.xaml:1404, 1418` ; `ThemeManager.AppliquerDensite` (`ThemeManager.cs:148-166`) pose `GridRowHeight`, `GridHeaderHeight`, `WorkCardPadding` et `WorkCardMinHeight`, mais **aucune hauteur de barre latérale** | majeur |
| P07 | **Focus clavier invisible** : identique au survol, 1,11:1. | `RaqmiTheme.xaml:557, 580-591, 1421, 1466-1468` | majeur |
| P08 | **Doublons** : « Mon Espace » deux fois (bouton + domaine 01 repliable), deux « Administration ». | `MainWindow.xaml:469-488`, `ModuleNavigationGroup.cs:83-101` (01 non exclu), `Tree.cs:64-66` | majeur |
| P09 | **Bruit** : libellé « Modules », badge compteur sur chaque en-tête, icônes du même gris que le chevron, quatre tailles de texte. | `MainWindow.xaml:461-463`, `RaqmiTheme.xaml:1486-1515` | majeur |
| P10 | **État déplié non mémorisé** entre deux lancements (prescrit § 2.5 règle 3). | aucune clé dans `DesktopSettings.cs` ; `ModuleNavigationGroup.cs:13-14` | majeur |
| P11 | Info-bulle de description sur **chaque ligne** au survol. | `RaqmiTheme.xaml:1408-1413`, `MainWindow.xaml:93`, `ModuleTile.cs:157-159` | mineur |
| P12 | Domaine replié contenant l'écran actif : **aucun repère** sur l'en-tête. | `MainWindow.xaml:72, 91`, `RaqmiTheme.xaml:585-600` | mineur |
| P13 | Numéro de domaine absent de la barre, présent dans le fil d'Ariane et l'accueil. | `Navigation.cs:229`, `ModuleCatalogView.xaml:34` | mineur |
| P14 | En-tête de domaine sans `AutomationProperties.Name` (Content gabarité). | `MainWindow.xaml:69-73`, `RaqmiTheme.xaml:1520-1530` | mineur |
| P15 | Vocabulaire flottant module/écran, champ de recherche de ~154 px. | `MainWindow.xaml:461, 495, 497, 533` | mineur |
| P16 | Journal d'audit sous 22 (épinglé), simple alias dans 15 « Qualité, Audit… » qui n'apparaît donc pas. | `Tree.cs:330-332, 422-424`, `ModuleNavigationGroup.cs:90` | mineur (arbre, pas WPF) |

## A.2 Pistes écartées, et pourquoi

| Piste | Raison de l'écart (une ligne) |
|---|---|
| **A — Rail 64 px + volet** (2 lentilles sur 3 l'ont préférée) | Libellés de rail à 10 px illisibles à 1 m, second vocabulaire (« Chambres », « Socle ERP ») contraire à la règle du vocabulaire unique, débordement « Plus » sur portable qui détruit la mémorisation spatiale, deux modes ancré/flottant + heuristique « 10 s » = la dette la plus coûteuse ; reste la meilleure candidate si un jour un rail réduit est demandé (voir § 5). |
| **C — Shell sombre en L** | Veto : état actif `SecondaryBrush` et survol `PrimaryPressedBrush` remappés en bleus clairs en sombre (**2,00:1** pour #6FAEF0 et **3,24:1** pour #4585D2 sur `TextPrimaryBrush` sombre #E6EEF7) ; Ctrl+F entre en collision avec `ApplicationCommands.Find` ; 30 % de la fenêtre en aplat sombre pour 8 h de saisie ; familles = un niveau de plus et un ordre qui contredit la numérotation du fil d'Ariane. |
| **D — Domaine actif + commutateur** | Popup à deux volets suivi au survol (méga-menu capricieux en WPF, Narrateur), perte de la vue d'ensemble pour les profils multi-domaines, colonne de 240 px vide sur la majorité des écrans, « Récents » en doublon de l'accueil (écarté par refonte-accueil § 1.1), Ctrl+K du catalogue « optionnel ». |
| Garder le repli sur l'accueil mais l'animer | Ne traite ni P02 (aucune navigation visible sur « Mon travail ») ni la cause profonde : un chrome que l'application déplace toute seule. |
| Rail d'icônes seules (23 puces) | Déjà écarté par la refonte de l'accueil (§ 1.1) : 22 pictogrammes de trait abstraits ne s'identifient pas sans texte, encore moins au tactile. |
| Domaine-feuille qui navigue directement s'il n'a qu'un écran | Deux comportements pour un même en-tête (bascule / navigation) : modèle clavier et UIA non uniforme ; la persistance rend le coût du second clic négligeable. |
| Fusion de 02 et 22 en pied | Décision d'arbre (Application), pas de barre ; les libellés courts distincts et la position (liste / pied) suffisent à lever l'ambiguïté visuelle (P08). |
| Barre à 248 px au lieu de 260 (pour rendre la 5e carte) | Ne la rend pas : 1280 − 248 − 48 = 984 px, 5 cartes exigent 1158 + 40 px. Seul le repli sur l'accueil rend les 5 cartes, et il est la cause n° 1. |

## A.3 Jetons

**Aucun hex nouveau.** La barre passe par une couche de **14 alias sémantiques** `Sidebar*` (greffe de C) déclarés en `DynamicResource` dans `RaqmiTheme.xaml` et redéfinis dans `ThemePalette.Sombre` (82 → 96 entrées ; `VerifierCouverture` mis à jour, puisque `ThemeManager.cs:133` remplace les brushes par clé et exige la parité). Un seul alias diverge entre les deux thèmes (`SidebarActiveForegroundBrush`), parce que `PrimaryBrush` sombre (#5B9BE8) ne pèse que 4,25:1 sur `AccentSoftBrush` sombre (#0E3B40).

| Alias (nouveau) | Clair → jeton existant | Contraste clair | Sombre → jeton existant | Contraste sombre |
|---|---|---|---|---|
| `SidebarBackgroundBrush` | `SurfaceBrush` #FFFFFF | 1,08:1 sur #F4F7FA (séparation par le filet) | `SurfaceBrush` #152233 | — |
| `SidebarBorderBrush` (filet droit) | `BorderStrongBrush` #B9CCDD | 1,53:1 sur #F4F7FA — décoratif, doublé par la rupture de contenu | `BorderStrongBrush` #4B6E93 | 3,36:1 |
| `SidebarTextBrush` (domaines, Mon Espace) | `TextPrimaryBrush` #0F2337 | **15,96** sur blanc, 13,47 sur survol | `TextPrimaryBrush` #E6EEF7 | 13,71 |
| `SidebarScreenTextBrush` (écrans au repos) | `TextSecondaryBrush` #4A6A87 | **5,67** sur blanc, **4,79** sur survol | `TextSecondaryBrush` #AFC3D6 | 8,86 / 7,00 |
| `SidebarSectionLabelBrush` (séparateurs) | `TextLabelBrush` #475569 | **7,58** | `TextLabelBrush` #C2D2E1 | 10,39 |
| `SidebarIconBrush` (icône au repos) | `TextSecondaryBrush` #4A6A87 | 5,67 (≥ 3:1 composant) | `TextSecondaryBrush` | 8,86 |
| `SidebarHoverBrush` | `SurfaceHoverBrush` #E4EDF4 | 1,19 — indice de courtoisie, doublé par le curseur et le passage du texte en `TextPrimary` | `SurfaceHoverBrush` #22344A | — |
| `SidebarActiveBackgroundBrush` | `AccentSoftBrush` #E1F4F5 | 1,14 sur blanc (le fond n'est pas seul) | `AccentSoftBrush` #0E3B40 | 1,31 |
| `SidebarActiveIndicatorBrush` (filet 3 px, icône, point) | `AccentActionBrush` #07767D | **4,74** sur AccentSoft, **5,39** sur blanc | `AccentActionBrush` #2FCBD6 | **6,19** / 8,13 |
| `SidebarActiveForegroundBrush` | `PrimaryBrush` #073B78 | **9,69** sur AccentSoft | **`TextPrimaryBrush` #E6EEF7** (divergent) | **10,44** sur AccentSoft |
| `SidebarFocusRingBrush` | `FocusRingBrush` #073B78 | **11,02** sur blanc, 9,69 sur AccentSoft | `FocusRingBrush` #7FB6F5 | 7,58 / 5,77 |
| `SidebarDisabledForegroundBrush` | `DisabledForegroundBrush` #576C82 | 5,42 | `DisabledForegroundBrush` #8195AB | 5,21 |
| `SidebarCountBadgeBackgroundBrush` / `SidebarCountBadgeForegroundBrush` (recherche seulement) | fond `SurfaceSubtleBrush` #EEF4F8, texte `TextSecondaryBrush` | 5,11 | idem sombre | 7,94 |
| Filets internes (pas d'alias) | `PanelBorderBrush` #DCE6EF | 1,26 décoratif | #26374D | — |
| Champ de recherche (pas d'alias) | `FieldBorderBrush` #7A8FA3 au repos (3,34 ≥ 3:1), `FocusRingBrush` au focus, `TextPlaceholderBrush` #5C7188 (5,03) | | styles TextBox existants | 6,24 |

Deux `Double` nouveaux en `DynamicResource` : `SidebarDomainRowHeight` (36 / 32) et `SidebarScreenRowHeight` (34 / 32), posés par `ThemeManager.AppliquerDensite` à côté de `GridRowHeight`.

Jetons qui **quittent** la barre : `ModuleActiveBackgroundBrush` #1A0AA3AD (1,11:1) et l'indicateur `AccentBrush` posé dessus (2,76:1 réel, pas les 3,06 sur blanc annoncés par le thème) ; `SurfaceSubtleBrush` comme signal de focus.

## A.4 Accessibilité

- **Noms UIA** produits par un convertisseur unique `DomainAutomationNameConverter` : en-tête « Domaine 05 Facturation & Ventes, 1 écran » (nom complet, jamais le libellé court ; en recherche « 2 résultats ») ; état déplié/replié exposé nativement par le pattern ExpandCollapse/Toggle du ToggleButton ; `HelpText` = description du domaine (P14) — **non livré le 08/09/2026** : le domaine n'a pas de description dans le catalogue (`DomainNode`, `FunctionalDomainDefinition`), à poser le jour où il en aura une. Rangée d'écran : `Name` = libellé complet, `ItemStatus` = « écran affiché » quand active ; désactivée : `IsEnabled=False` + `HelpText` = texte de `ToolTipText`. Mon Espace garde « Mon Espace, accueil ». Séparateur : `AutomationProperties.HeadingLevel=Level3`, non focusable. Panneau : `Name` = « Navigation des modules » ; liste : « Domaines ».
- **Focus visible** (WCAG 2.4.7, 1.4.11) : anneau 2 px `SidebarFocusRingBrush` 11,02:1 (clair) / 7,58:1 (sombre) ; la souris ne dessine jamais d'anneau, le clavier ne change jamais le fond (règle du thème, `RaqmiTheme.xaml:118-128`).
- **Contrastes** : tous les textes ≥ 4,5:1 sur leurs fonds réels dans les deux palettes (minimum : 4,79 pour un écran survolé en clair) ; composants non textuels ≥ 3:1 (filet actif 4,74 / 6,19, icône 5,39 / 8,13, bordure de champ 3,34).
- **Jamais la couleur seule** : actif = filet + fond + graisse + icône en trait accent ; déplié = chevron + annonce UIA ; désactivé = texte désactivé + cadenas + info-bulle + `HelpText` ; résultat = pastille numérique + dépliage.
- **Cibles** : rangées pleine largeur de 32 à 40 px (≥ 24 × 24 WCAG 2.5.8 ; 32 px minimum imposé par la charte, même en compact).
- **Info-bulle au focus clavier** : `ToolTipService` ouvert sur `GotKeyboardFocus` quand le libellé est tronqué, fermé sur `LostKeyboardFocus` — le nom complet ne dépend pas de la souris.
- **Annonce du filtrage** : TextBlock `LiveSetting=Polite` « 3 écrans dans 2 domaines » / « Aucun écran ne correspond ».
- **Fil d'Ariane** inchangé (phrase complète, `LiveSetting=Polite`) ; `Hidden` sur l'accueil donc rien d'annoncé.
- **Texte agrandi** : `MinHeight` + `TextWrapping` sur toutes les rangées, aucune hauteur fixe ; à 125–150 % les libellés passent sur deux lignes avant toute ellipse.
- **Thème sombre et contraste élevé** : aucune couleur en dur, seulement des alias résolus par `ThemePalette.cs`.

## A.5 Checklist des fonctions préservées

| Fonction (règle produit : rien n'est retiré) | Comment elle est préservée |
|---|---|
| Retour « Mon Espace » (bouton, Alt+Origine) | Rangée fixe en tête (`ShowHomeButton` conservé, même handler) ; `GoHomeShortcut` inchangé ; état actif sur l'onglet 0 ; barre visible partout, donc le chemin de retour l'est aussi |
| Recherche : nom / description / famille / numéro, sans accent ni casse | `ModuleSearchTextBox` et `ApplySidebarFilter`/`RefreshSidebar` inchangés (moteur `NavigationTreeBuilder` + `ModuleTile.SearchText` partagé avec l'accueil ; le numéro indexé est l'ordre du catalogue « 5.2 », jamais l'index d'onglet) ; le `ShortLabel` est **ajouté** aux champs indexés |
| Échap efface sans quitter le champ | `ModuleSearchTextBox_PreviewKeyDown` conservé tel quel ; ajout : Échap dans la liste renvoie au champ |
| Message « Aucun écran ne correspond » | `SidebarSearchEmptyTextBlock` conservé (même texte) + bouton « Ouvrir le catalogue » |
| Seuls les écrans autorisés apparaissent | `Build`/`Apply` consomment toujours l'arbre élagué ; l'aplatissement ne réordonne que les `ScreenNode` retenus ; aucun cadenas en régime permanent |
| Écran verrouillé désactivé avec info-bulle (permission manquante) | `IsEnabled=Tile.IsClickable`, `ToolTip=Tile.ToolTipText`, `ShowOnDisabled=True` conservés ; texte actuel « Accès non autorisé pour votre profil » (`ModuleTile.AccessDeniedToolTip`) ; le plan (point 13) y ajoute le libellé de la permission manquante ; même texte en `HelpText` ; l'info-bulle n'est masquée que sur les écrans ouvrables non tronqués |
| Surbrillance du module courant et ouverture de son domaine | `SyncSidebarToTab` : `Tile.IsActive` + `IsExpanded=true` via `Owns` inchangés (domaine 22 compris) ; **ajout** de `ContainsActiveScreen` (icône en trait accent + point si replié) |
| Administration système en un clic depuis partout | `SidebarPinnedPanel` conservé hors du `ScrollViewer`, filet masqué en recherche vide ; désormais visible aussi depuis Mon Espace, pour les profils qui y ont un écran |
| Fil d'Ariane Domaine › Module › Sous-module › Écran | `UpdateBreadcrumb` inchangé (séparateur « → » actuel conservé) ; ligne de 24 px réservée (`Hidden` sur l'accueil) |
| Navigation clavier complète (Tab, Espace/Entrée, flèches) | ToggleButton / Button conservés ; ajout de `TabNavigation=Once`, `DirectionalNavigation=Contained`, handler `SidebarList_PreviewKeyDown` (→ ← Origine Fin Échap) |
| Noms d'automatisation | « Mon Espace, accueil » conservé ; ajout du `Name` sur chaque en-tête, rangée, panneau, liste ; `HeadingLevel` sur les séparateurs |
| Vocabulaire unique avec les cartes de l'accueil | Rangées liées à `Screen.Label` et à la même `ModuleTile` ; `ShortLabel` = colonne du catalogue affichée **aussi** par les puces de `ModuleCatalogView` ; nom complet dans le fil d'Ariane, l'info-bulle, l'UIA, les cartes |
| Aucune règle métier dans le WPF | Pas de filtrage de permission ni de maturité côté client ; aplatissement, compteur, marqueur actif = projections de l'arbre élagué ; `ShortLabel` vit dans `RaqmiSystem.Application` |
| Un seul chemin de navigation (`NavigateToModule`) | Les rangées appellent `ModuleTileNavigate_Click` ; rien d'autre ne navigue |
| Restauration de l'état déplié après recherche, pas de clignotement | `groupsExpandedBeforeSearch` et `Apply` (pas de reconstruction) conservés ; la collection `Rows` est rejouée dans `Apply` |
| Ctrl+K vers la recherche | `GoToModuleShortcut_Executed` inchangé (barre hors accueil, catalogue sur l'accueil) |
| Repli de sécurité vers l'accueil sur onglet non autorisé | `MainTabs_SelectionChanged` inchangé ; ne provoque plus de saut |
| Bandeau de session lisible sur tous les écrans | Conservé dans la colonne de contenu (`Grid.Column=1`), `BusyProgressBar` et `StatusTextBlock` inchangés ; le commentaire l. 1573-1576 (« sorti de la barre latérale, qui est repliée sur l'accueil ») est réécrit |
| Densité compacte prescrite par la spec (§ 2.4) | Enfin livrée : deux `Double` posés par `AppliquerDensite`, sans réduire le texte |
| Écrans du domaine 01 (« Workflows & validations ») | Rendus sous la rangée Mon Espace (strate A), pas retirés |

## A.6 Plan d'implémentation par fichiers

| # | Fichier | Changement | Effort |
|---|---|---|---|
| 1 | `src/RaqmiSystem.Application/Navigation/FunctionalArchitectureCatalog.ShortLabels.cs` (nouveau) + `NavigationNodes.cs` | Dictionnaire Id → `ShortLabel` (22 entrées, § 3.5) ; propriété `DomainNode.ShortLabel` (défaut = `Label`) alimentée à la construction de `Tree` ; `NavigationTreeBuilder` l'ajoute au texte de recherche. Les lignes `Domain("NN", …)` lues par l'outillage ne changent pas de forme | 0,5 j |
| 2 | `tests/RaqmiSystem.Tests/NavigationTreeTests.cs` | Tests : chaque domaine a un `ShortLabel` non vide, unique, ≤ 22 caractères ; chaque mot est un mot du `Label`, un préfixe ≥ 4 lettres d'un mot du `Label`, ou un sigle de la liste blanche ; la recherche trouve un domaine par son libellé court (« revenue » → 07) ; `NavigationTreeBuilderTests` : un domaine 01 élagué ne contient que des `Screen` (pas d'alias) ; **projection `unit.manager` = 12 domaines, sans 13 ni 22** (le test qui aurait évité l'erreur de la première version de ce document) | 0,5 j |
| 3 | `src/RaqmiSystem.Desktop/ModuleNavigationGroup.cs` | `ShortLabel` ; `ObservableCollection<object> Rows` (`ModuleNavigationScreen` et nouveau `record ModuleNavigationSectionLabel(string Label)`) calculée dans `Apply` selon la règle « séparateur seulement si le module retient ≥ 2 écrans » ; `bool ContainsActiveScreen` ; `bool ShowResultCount` ; `Build` renvoie le groupe 01 à part (`IsHome`) au lieu de l'inclure dans la liste ; correction du commentaire « VisibleModules » | 1 j |
| 4 | `src/RaqmiSystem.Desktop/MainWindow.xaml` | `SidebarColumn=260`, **suppression de `SidebarGapColumn`** (grille à deux colonnes) ; **`BreadcrumbBorder` (l. 565) et `MainTabs` (l. 578) passent de `Grid.Column="2"` à `Grid.Column="1"`** ; **bandeau de session (l. 1577) : `Grid.Row="2" Grid.Column="1"`, sans `ColumnSpan`** ; Margin 24 déplacée sur la colonne de contenu ; `SidebarBorder` `Grid.RowSpan=3` avec style `SidebarPanel` ; `BreadcrumbBorder Height=24` ; suppression du TextBlock « Modules » ; `SidebarHomeScreensItemsControl` sous `ShowHomeButton` ; `SidebarGroupTemplate` réécrit (ItemsControl lié à `Rows`, deux DataTemplate implicites) ; `TabNavigation=Once` + `DirectionalNavigation=Contained` + `PreviewKeyDown` sur la liste ; placeholder « Rechercher un écran… » ; bouton « Ouvrir le catalogue » ; TextBlock `LiveSetting` ; `AutomationProperties` sur panneau, liste, rangées ; **commentaires l. 431-434 et l. 1573-1576 réécrits** (« la barre est repliée sur l'accueil » n'est plus vrai) | 1,5 j |
| 5 | `src/RaqmiSystem.Desktop/MainWindow.Navigation.cs` | `SyncSidebarToTab` ne touche plus ni `Visibility` ni colonnes (suppression de `SidebarWidth`/`SidebarGapWidth` et des trois lignes l. 204-206) ; pose `ContainsActiveScreen` via `Owns` ; `RefreshSidebar` pose `ShowResultCount` ; Entrée → premier résultat ; `SidebarList_PreviewKeyDown` ; chargement/sauvegarde différée des domaines dépliés (jamais si `isSidebarSearchActive`) ; commentaire l. 176-179 réécrit | 1 j |
| 6 | `src/RaqmiSystem.Desktop/DesktopSettings.cs` | Clé `string[]? SidebarExpandedDomains` (par poste, comme `Densite`) + `Load`/`Save` | 0,25 j |
| 7 | `src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` | Alias `Sidebar*` (A.3) ; ressources `SidebarDomainRowHeight` 36 / `SidebarScreenRowHeight` 34 ; style `SidebarPanel` ; `ModuleNavButton` / `ModuleNavSubButton` / `ModuleNavGroupHeader` : `MinHeight` en `DynamicResource`, `TextWrapping` + `MaxLines 2`, survol `SidebarHoverBrush`, `IsKeyboardFocused` → Border anneau 2 px (au lieu du fond), `Tag=Active` → `SidebarActiveBackgroundBrush` + indicateur `SidebarActiveIndicatorBrush` + texte `SidebarActiveForegroundBrush`, `InitialShowDelay 600`, DataTrigger `IsTextTrimmed` → ToolTip ; `ModuleNavGroupHeaderTemplate` : `Text=ShortLabel`, icône `Stroke` `SidebarIconBrush` / `SidebarActiveIndicatorBrush` selon `ContainsActiveScreen`, point 6 px si `ContainsActiveScreen && !IsExpanded`, pastille liée à `ShowResultCount` ; nouveau style `ModuleNavSectionLabel` ; gabarit désactivé avec cadenas 12 px | 1,5 j |
| 8 | `src/RaqmiSystem.Desktop/ThemePalette.cs` + `ThemeManager.cs` | Mapping sombre des 14 alias (dont `SidebarActiveForegroundBrush` → `TextPrimaryBrush`) : `Sombre` 82 → 96 entrées, **`VerifierCouverture` mis à jour** ; `AppliquerDensite` pose les deux hauteurs 36/32 et 34/32 | 0,25 j |
| 9 | `src/RaqmiSystem.Desktop/Converters.cs` | `DomainAutomationNameConverter` (multi-valeurs Nom complet + compteur + mode recherche) ; comportement attaché `TrimmedTextToolTip` (ouverture au focus clavier) ; `ModuleGroupIconConverter` inchangé | 0,5 j |
| 10 | `src/RaqmiSystem.Desktop/Views/ModuleCatalogView.xaml` | Puces de domaine : `ShortLabel` avec nom complet en info-bulle (même source) ; **vérifier le reflow** de la grille de cartes à **972 px (1280 × 800) et 1058 px (1366 × 768)** ; `ModuleNavSubButton` restylé y est aussi utilisé (l. 337) : contrôle visuel | 0,5 j |
| 11 | `src/RaqmiSystem.Desktop/MainWindow.xaml.cs` | Appel de `LoadSidebarExpandedDomains` après la construction des groupes ; abonnement `PropertyChanged(IsExpanded)` → sauvegarde différée | 0,25 j |
| 12 | `docs/design/navigation-shell.md` (§ 2.4, 2.5, 5.2, 8.2, 9, 11.5, 11.6) et `docs/design/accueil/refonte-accueil.md` (**§ 1.1, § 1.3, § 2.1, § 4, § 5.2**) | Consigner la décision inverse (barre stable partout, 260 px, 01 non répété, aplatissement, libellé court, numéro hors rangée, **4 cartes par rangée**) ; au § 4 de refonte-accueil : « aucun hex nouveau, 14 clés `Sidebar*` dans `ThemePalette.Sombre` (82 → 96), `VerifierCouverture` mis à jour » ; § 9 de navigation-shell : projection `unit.manager` alignée sur `SecuritySeeder` (12 domaines, pas de pied) — sinon la prochaine vague repliera la barre « par conformité » | 0,25 j |
| 13 | `src/RaqmiSystem.Desktop/ModuleTile.cs` | `ToolTipText` d'une tuile verrouillée : « Accès non autorisé pour votre profil — permission requise : {libellé} », libellé lu dans `PermissionCatalog` (`PermissionDefinition.Name`, ex. « Lire la comptabilité ») à partir de `Entry.PermissionKey` ; même texte pour `AutomationName` ; utilisé par la barre **et** par les cartes du catalogue (cohérence) | 0,25 j |
| | **Total** | | **8,25 j** (dont 0,5 j de test clavier + Narrateur) |

### A.6.1 Tests à écrire

- `NavigationTreeTests` (Application, projet de tests existant) : les tests du point 2 ci-dessus. **Aucun test n'est annoncé sur `ModuleNavigationGroup` ni `SyncSidebarToTab`** : `RaqmiSystem.Tests.csproj` ne référence pas `RaqmiSystem.Desktop` (net10.0-windows). Si l'on veut tester la règle d'aplatissement, elle descend dans `RaqmiSystem.Application` sous forme d'une fonction pure `FlattenForSidebar(DomainNode) → IReadOnlyList<SidebarRow>` consommée par `Apply` (option recommandée, +0,25 j, incluse dans le total).
- `NavigationTreeBuilderTests` : pour un jeu de permissions donné, la projection aplatie d'un domaine ne contient un libellé de module que si ce module retient ≥ 2 écrans ; le domaine 01 élagué n'apparaît pas dans la liste principale mais expose ses écrans ; pour les clés de `unit.manager`, la liste vaut exactement 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 20 et le domaine 22 est absent.
- Plan de test manuel (0,5 j) : parcours clavier complet (Tab, flèches, → ←, Entrée, Échap) avec des groupes masqués par la recherche ; Narrateur sur un en-tête, une rangée active, une rangée désactivée, le compteur de résultats ; thème sombre sur les cinq états ; densité compacte à 1366 × 768 avec le profil administrateur ; retour Mon Espace par les quatre chemins (aucun saut) ; bandeau de session visible sur Mon Espace et sur un écran de module.

### A.6.2 Risques secondaires

| Risque | Parade |
|---|---|
| `TextBlock.IsTextTrimmed` n'est levé qu'après mesure : première info-bulle possiblement manquante | Liste < 60 rangées, virtualisation désactivée ; avec `TextWrapping` 2 lignes, la troncature devient exceptionnelle |
| Restyler `ModuleNavSubButton` touche aussi les résultats de recherche de `ModuleCatalogView` | Cohérence souhaitable ; contrôle visuel prévu au point 10 |
| Thème sombre : `PrimaryBrush` sombre sur `AccentSoft` sombre = 4,25:1 | Réglé par l'alias divergent `SidebarActiveForegroundBrush` → `TextPrimaryBrush` sombre (10,44:1) ; vérifié pour les cinq états dans les deux palettes (A.3) |
| Le libellé de permission ajouté à `ToolTipText` (point 13) expose un vocabulaire technique | On affiche `PermissionDefinition.Name` (« Lire la comptabilité »), jamais la clé (`accounting.read`) |
