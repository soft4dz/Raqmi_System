# Refonte de l'accueil — spécification finale « Mon Espace · Mon travail »

> **Statut** : spécification de référence, version du 03/09/2026, branche `reorg/phase-1` (HEAD `e7dcaad`).
> Décision du directeur de la conception après jury de trois lentilles sur quatre concepts
> (`docs/design/accueil/explorations/`). Maquette de référence : [`maquette-accueil.html`](./maquette-accueil.html)
> (autonome, sept profils, deux thèmes, deux densités, unité du poste affectée ou non, quatre simulations de
> données). Aucun code n'est modifié par ce livrable ; les chiffres de la maquette sont des exemples annotés.
>
> Références : `docs/charte-ui-desktop.md` (loi du projet), `docs/design/navigation-shell.md` (shell),
> `docs/design/icones-domaines.md`, `docs/reorganisation/03-cartographie-cible.md` (domaine 01),
> `src/RaqmiSystem.Infrastructure/Security/SecuritySeeder.cs` (droits réels), `src/RaqmiSystem.Api/Endpoints/*`
> (politiques des routes), `src/RaqmiSystem.Desktop/Api/RaqmiApiClient.*.cs` (méthodes clientes).

---

## 1. Décision

### 1.1 Le concept retenu et ce qu'on y greffe

**Concept directeur : « Poste de travail » (exploration 1).** L'accueil devient un établi : il répond à la seule
question qu'un réceptionniste, un directeur d'unité, une caissière ou une DRH se posent en ouvrant l'application —
*« qu'est-ce qui attend un geste de ma part, maintenant ? »* — avec des files de travail comptées par le serveur,
regroupées en trois bandes d'urgence, chaque carte ouvrant l'écran qui agit.

Les trois lentilles du jury l'ont classé premier (52, 49 et 54 sur 60 ; 155 cumulés contre 130 pour le Cockpit,
131 pour le Portail, 124 pour le Catalogue vivant), pour les mêmes raisons : c'est le seul concept qui sert les cinq
postes à la fois (réception à 7 h, directeur d'unité à 9 h, caisse, RH, direction le lundi), le seul dont le
registre a survécu intégralement à la vérification dans le code, le seul qui respecte le contrat de vue sans le
modifier, et celui dont la logique (« lecture compose, action donne le verbe, sinon Suivi ; cible verrouillée à
chiffre lisible ») reste utile au lecteur seul comme au profil réduit à `settings.read`.

Les greffes retenues — là où les jurés les ont signalées, et seulement là — ne changent pas la logique de page ;
elles la complètent :

| Greffe | Origine | Pourquoi elle entre |
|---|---|---|
| Files « Arrivées sans chambre affectée » et « Départs avec solde à régler » (`/lodging/arrivals`, `/lodging/departures`) | Cockpit | les deux files de comptoir les plus vraies ; champs serveur (`UnassignedCount`, `PendingCount`, `OutstandingBalance`) |
| Repère « Recettes J-1 · n/N unités saisies » pour les porteurs de `dashboard.read` (`UnitDashboardResponse`) | jury (lentille exploitation) | la direction garde un seul repère de valeur sans que l'accueil devienne un cockpit |
| Écran cible de repli résolu par permission (Recettes → 2 si `revenue.read` sinon 20 ; OP → 6 si `treasury.read` sinon 20 ; Clôture → 5 si `closing.read` sinon 30) | Cockpit | moins de cadenas, jamais une carte muette |
| Fraîcheur « actualisé à HH:mm » (`LiveSetting=Polite`), mention « toutes unités » sur les cartes groupe | Cockpit | dire d'où vient le chiffre et de quand il date |
| « suivi seulement » sur une bande sans aucun verbe | Cockpit (« À suivre ») | le lecteur n'est jamais sommé d'agir |
| Raisons d'état vide typées (`NoQueues` / `UnitMissing` / `AllClear`) et libellés d'absence explicites quand l'utilisateur peut agir | Portail | un état vide dit pourquoi et quoi faire |
| Groupes de clés résolus par `PermissionRegistry.AcceptedClaims` (clé historique **ou** clé cible), appliqués aussi à `HasModulePermission` | Portail | un rôle personnalisé porteur de clés cibles compose le même accueil que l'API lui accorde |
| Dédoublonnage des routes (une réponse, plusieurs cartes), table de vérité par rôle seedé, test `/pending` 200/403 | Portail | le registre est testé contre `SecuritySeeder`, pas contre une projection documentaire |
| Pastille de date métier « à jour » / « en retard · n j » dans le bandeau | Portail | la date métier est lue dès `lodging.read` + unité, indépendamment de la carte Clôture |
| Rafraîchissement au retour sur l'onglet 0 au-delà de cinq minutes, sans `Timer` | Portail, Catalogue vivant | la cadence du battement de poste, déjà dans le produit |
| Recherche universelle sur l'arbre (sous-modules, écrans, nœuds planifiés badgés, `Entrée` ouvre le premier ouvrable) dans la section Catalogue | Catalogue vivant | « night audit » ou « balance âgée » doivent se trouver |
| Badge de maturité et icône dans l'en-tête de domaine du catalogue, retiré de la carte ; filtre Maturité ; `AutomationProperties.Name` / `HelpText` sur `ModuleCatalogCard` ; `HeadingLevel` sur les en-têtes | Catalogue vivant (= `navigation-shell.md` § 5.1) | dette de la vague 1, payée dans le lot d'extraction du catalogue quel que soit le concept |
| `WarningBanner` agrégé « n compteurs n'ont pas répondu (…) · Réessayer (F5) » | Catalogue vivant | un message par source noierait le bandeau de session |

Ce qui a été **écarté**, et pourquoi : la barre latérale déployée sur l'accueil (Cockpit — contredit
`navigation-shell.md` § 5.2 et retire 268 px) ; les instruments de remplissage (Effectif, Comptes, Journal 24 h,
NPS) ; la grille « Unités — santé du jour » et la bande KPI (recouvrement de l'onglet 20, chantier de phase
suivante sous `dashboard.read`) ; « Mes écrans » (répète la barre latérale) ; « Mon activité » (décoration pour
trois rôles, paramètre client manquant — candidat de lot suivant) ; les réglages Apparence/Densité dans l'accueil
(troisième endroit qui écrit `DesktopSettings`) ; le badge « Aperçu technique » posé à la main sur une carte (la
maturité est calculée par domaine, jamais saisie — l'honnêteté passe par le texte de la légende) ; la passe unique
dans un seul `RunAsync` avec absorption d'erreur (contraire à § 3.1) ; le rail de 23 puces à icône seule.

### 1.2 Tableau des scores du jury

| Concept | Lentille exploitation | Lentille design & WPF | Lentille produit & readiness | Cumul |
|---|---:|---:|---:|---:|
| 1 — Poste de travail | 52 | 49 | 54 | **155** |
| 2 — Cockpit | 44 | 42 | 44 | 130 |
| 3 — Portail Mon Espace | 42 | 39 | 50 | 131 |
| 4 — Catalogue vivant | 40 | 41 | 43 | 124 |

(Chaque lentille note sur 60 : usage, charte, honnêteté, adaptativité, faisabilité, accessibilité, 10 points chacun.)

### 1.3 Points ouverts tranchés

| Point | Décision | Motif |
|---|---|---|
| Libellé de la racine : « Accueil » ou « Mon Espace » | **« Mon Espace »** sur la première ligne de la barre latérale (`ShowHomeButton`), `AutomationProperties.Name` « Mon Espace, accueil », `Alt+Origine` inchangé ; l'attribut `Header="Accueil"` du `TabItem` 0 reste tel quel (jamais affiché : le `TabControl` n'a pas d'en-têtes) | `navigation-shell.md` § 2.1 le prescrit dès la vague 1 « pour que le vocabulaire soit stable avant le contenu » ; l'onglet 0 devient réellement la page de la personne connectée (files composées de **ses** permissions, unité de **son** poste, **ses** derniers écrans) ; trois chaînes à changer, aucune structure |
| Nom de la première section | **« Mon travail »** | c'est le libellé du module `01 → Mon travail` de l'arbre (`FunctionalArchitectureCatalog.Tree`) : un seul vocabulaire ; « Poste de travail » est déjà `Paramétrage global › Poste de travail` (collision relevée par le jury) |
| Place du catalogue des 50 cartes | **Seconde section de l'onglet 0**, « Catalogue des modules », dans un `SectionTabControl` (charte § 2.4) ; atteignable par l'onglet de section, par la carte « Où en est le produit ? », par `Ctrl+K` (bascule + focus `HomeSearchTextBox`) ; ouverture de session toujours sur « Mon travail », section non mémorisée | les 50 cartes restent complètes, filtrées, cadenassées, à un clic ; un `Expander` ou une puce `FilterChip` ne sont pas les composants de la charte pour deux sections indépendantes ; une mémorisation par poste ferait ouvrir un catalogue à l'équipe de nuit d'un poste partagé |
| Barre latérale sur l'accueil | **repliée**, comportement de `SyncSidebarToTab` inchangé ; fil d'Ariane masqué | `navigation-shell.md` § 5.2 : la racine est le sommaire, un second sommaire ferait doublon |
| Unité de l'accueil | **l'unité du poste** (`DesktopSettings.StationUnitCode`, réglage par poste dans `Paramétrage global › Poste de travail`, liste si `units.read`, code saisi sinon) ; **aucun sélecteur dans le bandeau** : le bandeau l'affiche en texte avec un bouton fantôme « Changer » vers Paramétrage | un seul endroit écrit le réglage ; un poste de réception appartient à une unité ; piloter plusieurs unités est le rôle des onglets 3, 19 et 20, pas de l'accueil ; supprime le sélecteur inutile chez RH |
| Densité | deux `Double` en `DynamicResource` (`WorkCardPadding` 16,14 / 12,10 ; `WorkCardMinHeight` 104 / 88) posés par `ThemeManager.AppliquerDensite` à côté de `GridRowHeight` ; espacements de bande 20 / 14 ; Compact retire de l'air, jamais de texte | charte § 3.14 |
| Badges | maturité : **dans l'en-tête de domaine du catalogue uniquement** (retirée de la carte), jamais sur une file de travail ; pastilles de statut : « Suivi » (`StatusDraft`), « Indisponible » (`StatusRejected`), date métier « à jour » (`StatusValidated`) / « en retard · n j » (`StatusSubmitted`) ; bande « En retard » en **ambre** (`StatusSubmitted*` : attention), « Aujourd'hui » point `AccentBrush`, « À surveiller » point `TextMutedBrush` | `navigation-shell.md` § 6.3 ; `StatusRejected` veut dire « refusé », pas « en retard » (remarque du jury) |
| Rafraîchissement au retour sur l'onglet 0 | **oui, au-delà de cinq minutes** (même cadence que `HeartbeatInterval`), sans `Timer`, en plus de la connexion et de `F5` ; « actualisé à HH:mm » toujours visible | une réception revient dix fois par jour sur l'accueil ; un compteur périmé est pire qu'un rechargement borné et annoncé |
| Files lisibles sans droit d'action | **restent dans leur bande d'urgence**, en mode « Suivi » ; la bande dit *quand*, le mode dit *qui* | corrige l'incohérence relevée entre le texte et la maquette du concept 1 (« Journées non clôturées » tantôt En retard tantôt À surveiller) |
| Montants sur les cartes « compte de lignes » | **retirés** (« Commandes à approuver », « Ordres de paiement à régler », « Départs en retard ») ; un montant n'apparaît que si le serveur renvoie l'agrégat (`PendingValidationAmount`, `PendingPaymentOrderAmount`, `OutstandingBalance`, `Total.Over90`, `GrandTotal`) | charte § 3.10 ; défaut de la maquette du concept 1 relevé par le jury |
| `RunAsync(quiet: true)` | **hors périmètre** : une `RunAsync` par source, `MainTabs` gelé par intermittence, accepté et borné (2 à 23 appels légers à moyens selon le profil) ; la surcharge est instruite séparément avec le propriétaire de la charte | § 3.1 ne se contourne pas dans une vue |
| Navigation vers un sous-onglet | **hors périmètre v1** : une carte ouvre l'onglet (`NavigateRequested(int)`) ; le libellé de la carte nomme l'écran, pas le sous-onglet | aucune API `SelectSection` n'existe sur les vues |

---

## 2. Structure définitive

### 2.1 Wireframe (profil Directeur d'unité, unité du poste ALG-CEN, 1240 × 760, barre latérale repliée)

```text
┌ Onglet 0 « Mon Espace » — barre latérale repliée, fil d'Ariane masqué ─────────────────────────────────────┐
│  Mon travail ▁▁▁▁▁▁▁▁▁   Catalogue des modules                            ← SectionTabControl (charte § 2.4) │
│                                                                                                             │
│ ┌ CardBorder — Bandeau ─────────────────────────────────────────────────────────────────────────────────┐   │
│ │ Bonjour, Samir Merzouk                                     Unité du poste  ALG-CEN · Hôtel Riadh     │   │
│ │ mardi 1 septembre 2026 · Groupe Riadh Hôtels               Date métier     31/08/2026 [en retard · 1 j]│   │
│ │ 5 en retard · 16 aujourd'hui · 2 à surveiller  (Polite)                               [Changer]      │   │
│ │ [Mon profil] [Mes préférences] [Ma sécurité]                 [⟳ Actualiser]   actualisé à 08:12       │   │
│ └───────────────────────────────────────────────────────────────────────────────────────────────────────┘   │
│ ▲ 2 compteurs n'ont pas répondu (Cockpit DEC, Housekeeping). Les autres sont à jour. [Réessayer (F5)]      │
│                                                                                                             │
│ ● EN RETARD · 5 files                                                                                       │
│ ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐ ┌──────────────────┐   │
│ │ PMS front office │ │ PMS front office │ │ Clôture journ.   │ │ Cockpit DEC      │ │ CA journalier    │   │
│ │          ALG-CEN │ │          ALG-CEN │ │          ALG-CEN │ │ Groupe · toutes u│ │ Groupe · toutes u│   │
│ │ 2                │ │ 1                │ │ 1                │ │ 2                │ │ 1                │   │
│ │ Arrivées en ret. │ │ Départ en retard │ │ Journée à clôt.  │ │ Journées non clô.│ │ Recette rejetée  │   │
│ │ candidates no-sh.│ │ date de départ.. │ │ dernière 30/08   │ │ la + ancienne…   │ │ rejetée par DEC  │   │
│ │ [Traiter →]      │ │ [Traiter →]      │ │ [Clôturer →]     │ │ [Ouvrir le cockpit]│ [Corriger →]     │   │
│ └──────────────────┘ └──────────────────┘ └──────────────────┘ └──────────────────┘ └──────────────────┘   │
│ ● AUJOURD'HUI · 16 files                                                                                    │
│ │14 Arrivées│3 Sans chambre affectée│9 Départs│2 Départs avec solde · 27 400,00 DA│6 Chambres à préparer│… │
│ ● À SURVEILLER · 2 files                                                                                    │
│ │1 Chambre hors service│5 Articles sous le minimum│                                                        │
│                                                                                                             │
│ DERNIERS ÉCRANS OUVERTS (ce poste)   (PMS front office · Front Office) (Clôture journalière) (Hébergement)  │
│ ┌ Où en est le produit ? ── 31 modules disponibles sur 50 ▮▮▮▮▮▮░░ · 11 fonctionnels · 5 aperçus · 6 planifiés│
│ │ chiffres du catalogue embarqué, pas du serveur                     [Ouvrir le catalogue des modules →] │   │
└─────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Budget vertical mesuré sur la maquette à 1240 × 760 (≈ 600 px sous l'en-tête) : onglets de section 38, bandeau
≈ 150, en-tête de bande 22, carte ≈ 168 (légende sur deux lignes), espacements 20. **La bande « En retard » et la
première rangée d'« Aujourd'hui » sont visibles sans défiler** ; en densité Compact, la seconde rangée apparaît.
Un profil court (RH : deux cartes) tient entièrement, carte produit comprise.

### 2.2 Les six sections de « Mon travail », dans l'ordre

| # | Section | Rôle | Toujours présente ? |
|---|---|---|---|
| A | Onglets de section | « Mon travail » / « Catalogue des modules » | oui |
| B | Bandeau | qui, quand, où (unité du poste, date métier), synthèse, accès au compte, actualisation | oui |
| B' | Encarts d'avertissement | unité du poste absente ; sources en échec | seulement si la condition est vraie |
| C | Bande « En retard » | ce que le serveur qualifie déjà de dépassé | oui (état vide ou ligne « aucune file ouverte ») |
| D | Bande « Aujourd'hui » | la journée métier : ce qui attend un geste ou informe | oui (état vide typé) |
| E | Bande « À surveiller » | signaux faibles | oui (état vide ou ligne « aucune file ouverte ») |
| F | Derniers écrans ouverts (ce poste) | jusqu'à six puces | masquée si vide |
| G | « Où en est le produit ? » | chiffres statiques du catalogue + accès à la seconde section | oui |

Section 2, « Catalogue des modules » : l'accueil-catalogue actuel (blocs 2 à 4 de `MainWindow.xaml` 611-944)
extrait tel quel dans `ModuleCatalogView`, plus les quatre corrections de `navigation-shell.md` § 5.1 (§ 3.8).

---

## 3. Spécification section par section

Conventions : *clé de lecture* = compose la carte ; *clé d'action* = donne le verbe (sinon mode **Suivi**, bouton
« Voir ») ; *cible* = onglet ouvert par `NavigateToModule` ; *clé cible* = clé du modèle `domaine.ressource.action`
couverte par la clé historique (`PermissionRegistry`), acceptée à égalité par le composeur.

### 3.1 A — Onglets de section

| | |
|---|---|
| Contenu | `SectionTabControl` à deux `SectionTabItem` : **Mon travail**, **Catalogue des modules** (styles promus de `TreasuryView.xaml:14-87` vers le thème, ou copie locale comme `AccountingView` si la promotion est refusée) |
| Source / permission | aucune (statique) |
| Clavier | `Ctrl+Tab` / `Ctrl+Maj+Tab` circulent entre les deux sections (réservé à cet usage, `MainWindow.xaml`) ; `Ctrl+K` sélectionne la seconde et donne le focus à `HomeSearchTextBox` ; `Alt+Origine` revient sur l'onglet 0 **et** sur « Mon travail » |
| AutomationLabels | `AutomationProperties.Name` des onglets = leur libellé ; le `TabControl` est nommé « Sections de Mon Espace » |
| Ce qui ne change pas | aucune balise `<TabItem>` ajoutée à `MainTabs` : les sections vivent dans le **contenu** de l'onglet 0 |

### 3.2 B — Bandeau

| Élément | Contenu | Source → champ | Permission (historique / cible) | Absence |
|---|---|---|---|---|
| Salutation | « Bonjour, {DisplayName} » (`HomeGreetingText`, `HeadingLevel=Level1`) | `LoginResponse.User.DisplayName` | aucune | — |
| Date | « mardi 1 septembre 2026 » (`HomeDateText`, `fr-FR` comme aujourd'hui) · établissement | `DateTime.Today` ; `GET /api/v1/settings` → `CompanyName` | établissement : `settings.read` / `admin.settings.read` | établissement omis sans la clé (rien à faire pour l'utilisateur, aucun rôle seedé n'en manque) |
| Unité du poste | « Unité du poste : ALG-CEN » (code ; le nom n'est connu que si `units.read` : la liste `GET /organization/hotel-units` le fournit) + bouton fantôme **Changer** → onglet 9 | `DesktopSettings.StationUnitCode` (réglage par poste, **N'EXISTE PAS** : à créer) ; `HotelUnitResponse.Name` si `units.read` / `admin.unit.read` | `settings.read` pour le bouton (onglet 9) | la ligne n'existe que si au moins une file de périmètre Unité est **lisible** (clé de lecture détenue) : « — non définie » + bouton **Définir** quand le réglage manque ; omise sinon (RH n'a rien à régler) |
| Date métier | « Date métier ALG-CEN : 31/08/2026 » + pastille **à jour** (`StatusValidated`) ou **en retard · 1 jour** (`StatusSubmitted`) | `GET /lodging/business-date?hotelUnitCode` → `BusinessDate`, `IsLate`, `PendingDays` (`BusinessDay.Resolve` : le client relaie trois champs, ne compare aucune date) | `lodging.read` / `lodging.front_office.read` **et** unité du poste — lue même si la carte Clôture (`closing.read`) est fermée : le bandeau est sa cible première | « Date métier : — unité du poste non définie » si `lodging.read` sans unité ; ligne omise sans `lodging.read` ; « — indisponible » si la source a échoué |
| Synthèse | « 4 en retard · 15 aujourd'hui · 2 à surveiller » (`SubtitleText`, `LiveSetting=Polite`, annoncée à chaque fin de chargement) ; suffixe « · suivi seulement » si aucune carte ne porte de verbe ; pendant le chargement : « Chargement des files de travail… » | comptes de **cartes** (dits comme tels dans le `HelpText` : « nombre de files, pas d'objets ») | — | — |
| Compte | trois `GhostButton` : **Mon profil** (→ onglet 9, section Santé du système, qui rend `GET /api/v1/me`), **Mes préférences** (→ onglet 9, section Poste de travail), **Ma sécurité** (→ événement `ChangePasswordRequested`, boîte `ShowChangePasswordDialog` existante, `POST /account/change-password`) | écrans existants | `settings.read` pour les deux premiers (cadenassés sinon, même règle que les cartes) ; aucune pour la sécurité | — |
| Actualiser | `SecondaryButton` `x:Name="RefreshHomeButton"` (trouvé par `F5`, charte § 3.13) + « actualisé à HH:mm » (`CaptionText`, heure locale du poste) | — | — | pendant un chargement : bouton désactivé par `SetBusy`, texte « actualisation en cours » |

Règle d'absence (greffe Portail, bornée) : un fragment absent est **nommé** quand l'utilisateur peut y remédier
(unité du poste, date métier sans unité) et **omis** quand il ne le peut pas (établissement sans `settings.read`).

### 3.3 B' — Encarts d'avertissement (`WarningBanner`, promu de `TreasuryView` vers le thème)

| Encart | Condition | Texte | Action |
|---|---|---|---|
| Sans unité | au moins une file de périmètre **Unité** est composée et `StationUnitCode` est vide | « Ce poste n'est rattaché à aucune unité : arrivées, départs, chambres, date métier et événements ne sont pas affichés. » | lien `GhostButton` « Paramétrage global › Poste de travail » (→ onglet 9) ; `role=status` |
| Sources en échec | au moins une `RunAsync` de source a échoué | « 2 compteurs n'ont pas répondu (Cockpit DEC, Housekeeping). Les autres sont à jour. » — libellés = écrans cibles des sources, pas des routes | bouton « Réessayer (F5) » → même chemin que `RefreshHomeButton` ; le détail HTTP est déjà dans le bandeau de session (`RunApiActionAsync`) ; `LiveSetting=Polite` |

Un seul encart par cause ; jamais de `MessageBox` (charte § 3.12).

### 3.4 C, D, E — Les trois bandes et le registre des files

Gabarit d'une carte (`CardBorder`, `Width` 222, `Margin` 0,0,12,12, `Padding` `WorkCardPadding`, `MinHeight`
`WorkCardMinHeight`, dans un `WrapPanel`) :

```text
┌ [icône du domaine 16 px] Écran cible (CaptionText)            [ALG-CEN | Groupe · toutes unités | Ma décision | Système] ┐
│ 14  (HomeStatValueText 27)   27 400,00 DA (13 SemiBold TextSecondary, seulement si agrégat serveur)         │
│ Arrivées du jour  (13,5 SemiBold)                                                                            │
│ 31 clients présents · occupation 74 %  (CaptionText, champs serveur)                                         │
│ [Traiter les arrivées →]  (SecondaryButton 32 px)   ou   [Voir] (GhostButton) + pastille « Suivi »          │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Règles communes :

- **Mode** : `À faire` si la clé d'action est détenue (verbe du registre, `SecondaryButton`) ; `Suivi` si la file
  a une clé d'action que le profil n'a pas (bouton `GhostButton` « Voir », pastille `Suivi` en `StatusDraft`, fond
  `SurfaceSubtleBrush`) ; `Information` si la file n'a pas de clé d'action (bouton « Voir », pas de pastille).
- **Cible verrouillée** : la permission de lecture de l'onglet cible manque et aucun repli n'est ouvrable →
  bouton `IsEnabled=False`, cadenas `ModuleCardLockIcon`, `ToolTipService.ShowOnDisabled`, info-bulle
  « Accès non autorisé pour votre profil » (`ModuleTile.AccessDeniedToolTip`) ; **le chiffre reste lisible** — le
  droit de lire un compteur n'est pas le droit d'ouvrir l'écran.
- **Bande finale** : celle du registre, ou celle qu'un booléen serveur impose (`IsLate`, `IsOverdue`). Aucun seuil
  côté client. Trois placements sont **éditoriaux et documentés** : « Journées non clôturées » (des journées métier
  passées non clôturées sont dépassées par définition), « Recettes rejetées » (un rejet est une décision serveur qui
  attend une reprise) et « Créances > 90 jours » (la tranche « > 90 » est le seau « échu » que le serveur calcule
  lui-même).
- **Zéros** : En retard et À surveiller masquent une carte à 0 ; Aujourd'hui la **garde, atténuée** (« 0 arrivée
  aujourd'hui » est une information ; fond `SurfaceSubtleBrush`, compteur `TextSecondaryBrush`).
- **Ordre dans une bande** : À faire, puis Suivi, puis Information ; à mode égal, ordre du registre.
- **En-tête de bande** : `HomeSectionLabel` + point 8 px + compteur de **cartes** en pastille, `HeadingLevel=Level2`,
  `AutomationProperties.Name` « En retard, 4 files » ; suffixe « · suivi seulement » quand aucune carte de la bande
  ne porte de verbe.
- **Montants** : `N2` culture courante + `CurrencyLabel` (`GET /settings`), uniquement quand le serveur renvoie
  l'agrégat ; un compte de lignes n'est jamais additionné.
- **Périmètre** : pastille `SurfaceSubtle` / `TextSecondary` portant le code d'unité, « Groupe · toutes unités »
  (aucune affectation utilisateur ↔ unité n'existe côté serveur : le cockpit DEC est groupe-entier même pour un
  directeur d'unité, et la carte le dit), « Ma décision » (filtré par rôle côté serveur) ou « Système ».

#### Registre des files (source unique : `HomeWorkQueueCatalog`, § 5)

Colonnes : bande (● éditoriale / ⚑ booléen serveur), périmètre (U unité, G groupe, M ma décision, S système),
clé de lecture historique / cible, clé d'action historique / cible (— = information), source → champ, cible
(onglet · clé de lecture de l'onglet ; → repli), verbe.

| Id | Carte | Bande | P. | Lecture | Action | Source → champ | Cible | Verbe |
|---|---|---|---|---|---|---|---|---|
| `arrivals-late` | Arrivées en retard | En retard ⚑ (`OverdueArrivals`) | U | `lodging.read` / `lodging.front_office.read` | `lodging.checkin` / `lodging.checkin.execute` | `GET /lodging/front-desk?hotelUnitCode&date` → `OverdueArrivals.Count` | 30 PMS front office · `lodging.read` | Traiter |
| `departures-late` | Départs en retard | En retard ⚑ (`OverdueDepartures`) | U | `lodging.read` | `lodging.checkout` / `lodging.checkout.execute` | front-desk → `OverdueDepartures.Count` (pas de montant : `FolioBalance` est par ligne) | 30 | Traiter |
| `closing-unit` | Journée(s) à clôturer | En retard ⚑ (`IsLate`, sinon absente) | U | `lodging.read` | `closing.close` / `lodging.closing.close` | `GET /lodging/business-date?hotelUnitCode` → `PendingDays`, `LastClosedDate` | 5 Clôture journalière · `closing.read` → 30 | Clôturer |
| `dec-backlog` | Journées non clôturées | En retard ● | G | `dashboard.read` / `pilotage.dashboard.read` | `closing.close` | `GET /pilotage/dec-cockpit?date` → `ClosingBacklogDayCount`, `OldestClosingDelay` (`HotelUnitCode`, `BusinessDate`, `AgeDays`) | 20 Cockpit DEC · `dashboard.read` | Ouvrir le cockpit |
| `dec-rejected` | Recettes rejetées à corriger | En retard ● | G | `dashboard.read` | `revenue.write` / `finance.revenue.record` | dec-cockpit → `RejectedCount` | 2 Recettes journalières · `revenue.read` → 20 | Corriger |
| `aging-90` | Créances à plus de 90 jours | En retard ● | G | `receivables.read` / `finance.receivable.read` | — | `GET /receivables/aging` → `Total.Over90` (montant), `Total.Total`, `AsOfDate` en légende | 13 Créances · `receivables.read` | Voir la balance |
| `backup` | Sauvegarde en retard / Dernière sauvegarde | En retard ⚑ (`IsOverdue`) sinon À surveiller | S | `maintenance.read` / `system.backup.read` | `maintenance.backup` / `system.backup.execute` | `GET /maintenance/backups/status` → `IsOverdue`, `AgeHours`, `OverdueThresholdHours`, `LastBackup` | 18 Sauvegarde · `maintenance.read` | Sauvegarder / Voir |
| `arrivals` | Arrivées du jour | Aujourd'hui | U | `lodging.read` | `lodging.checkin` | front-desk → `Arrivals.Count` ; légende `InHouseCount`, `Occupancy.OccupancyRatePercent` | 30 | Ouvrir les arrivées |
| `arrivals-unassigned` | Arrivées sans chambre affectée | Aujourd'hui | U | `lodging.read` | `lodging.checkin` | `GET /lodging/arrivals?hotelUnitCode&date` → `UnassignedCount` ; légende `RoomsToPrepare`, `NotReadyCount` | 30 | Affecter |
| `departures` | Départs du jour | Aujourd'hui | U | `lodging.read` | `lodging.checkout` | front-desk → `Departures.Count` | 30 | Ouvrir les départs |
| `departures-balance` | Départs avec solde à régler | Aujourd'hui | U | `lodging.read` | `lodging.checkout` | `GET /lodging/departures?hotelUnitCode&date` → `PendingCount`, `OutstandingBalance` (montant) | 30 | Encaisser |
| `hk-dirty` | Chambres à préparer | Aujourd'hui | U | `housekeeping.read` / `housekeeping.task.read` | `housekeeping.write` / `housekeeping.task.manage` | `GET /housekeeping/board?hotelUnitCode&date` → `DirtyRooms` ; légende `CleanRooms`/`TotalRooms` | 21 Housekeeping · `housekeeping.read` | Ouvrir le tableau |
| `hk-inspect` | Chambres à inspecter | Aujourd'hui | U | `housekeeping.read` | `housekeeping.inspect` / `housekeeping.room.inspect` | board → `AwaitingInspectionTasks` | 21 | Inspecter |
| `approvals` | Validations en attente de ma décision | Aujourd'hui | M | `approvals.decide` / `workflow.request.decide` (**jamais** `approvals.read` seul : la route répond 403) | `approvals.decide` | `GET /approvals/instances/pending` (filtré par rôle côté serveur) → `Count` ; légende « ordres de paiement — seul sujet en circuit aujourd'hui » (`ApprovalSubjectType.PaymentOrder`) | 16 Validations · `approvals.read` | Décider |
| `dec-revenue` | Recettes à valider (DEC) | Aujourd'hui | G | `dashboard.read` | `revenue.validate` / `finance.revenue.validate` | dec-cockpit → `PendingValidationCount`, `PendingValidationAmount` (montant) ; légende `OldestAgeDays` de la plus ancienne | 2 · `revenue.read` → 20 | Valider |
| `dec-po` | Ordres de paiement à approuver | Aujourd'hui | G | `dashboard.read` | `treasury.approve` / `finance.payment_order.approve` | dec-cockpit → `PendingPaymentOrderCount`, `PendingPaymentOrderAmount` (montant) | 6 Trésorerie · `treasury.read` → 20 | Approuver |
| `revenue-yesterday` | Recettes J-1 | Aujourd'hui | G | `dashboard.read` | — | `GET /revenue/daily/dashboard?date=J-1` → `GrandTotal` (montant, compteur principal), `UnitsWithEntry`/`TotalUnits`, `UnitsMissing`, `UnitsPendingValidation` | 3 Tableau de bord · `dashboard.read` | Voir le tableau |
| `revenue-draft` | Recettes en brouillon à soumettre | Aujourd'hui | U | `revenue.read` / `finance.revenue.read` | `revenue.write` | `GET /revenue/daily/summary?from=J-1&to=J&hotelUnitCode` → `DraftCount` | 2 · `revenue.read` | Soumettre |
| `po-pay` | Ordres de paiement à régler | Aujourd'hui | G | `treasury.read` / `finance.treasury.read` | `treasury.write` / `finance.payment_order.manage` | `GET /treasury/payment-orders?status=Approved` → nombre de lignes (pas de montant) | 6 | Régler |
| `receipts-draft` | Encaissements en brouillon | Aujourd'hui | U si unité connue, sinon G | `treasury.read` | `treasury.write` / `finance.receipt.manage` | `GET /treasury/receipts/summary?from=J&to=J[&hotelUnitCode]` → `DraftCount` | 6 | Confirmer |
| `receipts-today` | Encaissé aujourd'hui | Aujourd'hui | idem | `treasury.read` | — | même route **avec** `status=Confirmed` → `GrandTotal` (montant, compteur principal), `ConfirmedCount` ; `GrandTotal` sans filtre n'est pas documenté et n'est pas affiché | 6 | Voir |
| `counts-draft` | Inventaires à valider | Aujourd'hui | G | `inventory.read` / `inventory.stock.read` | `inventory.validate` / `inventory.count.validate` | `GET /inventory/counts?status=Draft` → nombre | 24 Stocks · `inventory.read` | Valider |
| `po-approve` | Commandes d'achat à approuver | Aujourd'hui | G | `purchasing.read` / `purchasing.order.read` | `purchasing.approve` / `purchasing.order.approve` | `GET /purchasing/orders?status=Draft` → nombre (pas de montant) | 25 Achats · `purchasing.read` | Approuver |
| `po-receive` | Commandes à réceptionner | Aujourd'hui | G | `purchasing.read` | `purchasing.receive` / `purchasing.receipt.execute` | `GET /purchasing/orders?status=Approved` → nombre de lignes dont `CanReceive` (drapeau serveur) | 25 | Réceptionner |
| `haccp` | Relevés HACCP non conformes | Aujourd'hui | G | `kitchen.read` / `fnb.kitchen.read` | `kitchen.write` / `fnb.kitchen.manage` | `GET /kitchen/readings?nonCompliantOnly=true&from=J&to=J` → nombre | 26 Cuisine · `kitchen.read` | Traiter |
| `absences` | Absences à approuver | Aujourd'hui | G | `hr.read` / `hr.employee.read` | `hr.write` / `hr.time.manage` | `GET /hr/absences?status=Requested` (`AbsenceStatus.Requested`) → nombre | 22 RH & paie · `hr.read` | Approuver |
| `payroll` | Bulletins en brouillon | Aujourd'hui | G | `hr.read` | `hr.payroll` / `hr.payroll.process` | `GET /hr/payroll/periods` → première période `Status ≠ Closed` : `DraftPayslipCount` / `PayslipCount`, `Period` | 22 | Ouvrir la paie |
| `events-today` | Événements du jour | Aujourd'hui | U | `mice.read` / `mice.event.read` | — | `GET /mice/events?hotelUnitCode&from=J&to=J` → nombre ; légende premier `Title` · `FunctionSpaceLabel` | 28 Groupes & MICE · `mice.read` | Voir |
| `hk-ooo` | Chambres hors service | À surveiller | U | `housekeeping.read` | — | board → `OutOfOrderRooms` | 21 | Voir |
| `low-stock` | Articles sous le minimum | À surveiller | G | `inventory.read` | — | `GET /inventory/low-stock` → nombre de lignes | 24 | Voir |
| `workstations` | Postes en service | À surveiller | S | `sync.read` / `system.workstation.read` | — | `GET /sync/stations` → `Workstations.Count` ; légende `DistinctAppVersions`, lignes dont `Freshness ≠ "Recent"` (vocabulaire serveur : Recent / Stale / Silent) | 27 Postes & erreurs · `sync.read` | Voir |

Trente et une files, **toutes** adossées à une route existante et à une méthode déjà présente dans
`RaqmiApiClient` (`GetFrontDeskAsync`, `GetBusinessDateAsync`, `GetArrivalsAsync`, `GetDeparturesAsync`,
`GetHousekeepingBoardAsync`, `GetPendingApprovalInstancesAsync`, `GetDecCockpitAsync`, `GetUnitDashboardAsync`,
`GetDailyRevenueSummaryAsync`, `GetPaymentOrdersAsync`, `GetCashReceiptSummaryAsync`, `GetInventoryCountsAsync`,
`GetPurchaseOrdersAsync`, `GetTemperatureReadingsAsync`, `GetHrAbsencesAsync`, `GetPayrollPeriodsAsync`,
`GetEventsAsync`, `GetLowStockAsync`, `GetBackupStatusAsync`, `GetWorkstationsAsync`, `GetAgingBalanceAsync`).
Aucune route ni méthode cliente nouvelle pour la v1.

Ce qui **n'est pas** dans le registre, et n'y sera jamais présenté comme une fonction : tâches transverses,
notifications, messagerie, agenda, favoris, documents, demandes, délégations (aucune route serveur —
`03-cartographie-cible.md` § 01). Ils restent des nœuds « Planifié » de l'arbre, visibles avec leur badge dans la
section Catalogue et dans ses résultats de recherche, jamais ouvrables.

#### États d'une carte

| État | Rendu | `AutomationProperties.Name` |
|---|---|---|
| Chargement | trois barres `SurfaceSubtleBrush` (22/10/10 px) à la place du compteur et de la légende, libellé visible, bouton « Chargement… » désactivé ; `BusyProgressBar` du bandeau de session pendant chaque appel | « {Libellé}, chargement » |
| Prêt · À faire | compteur, montant éventuel, légende serveur, `SecondaryButton` avec le verbe | « {Libellé}, {compte}[, {montant}], {périmètre}, à faire » |
| Prêt · Suivi | fond `SurfaceSubtle`, compteur `TextSecondary`, `GhostButton` « Voir », pastille « Suivi » | « …, suivi » |
| Prêt · Information | comme À faire avec « Voir », sans pastille | « …, information » |
| Zéro (Aujourd'hui) | fond `SurfaceSubtle`, compteur `TextSecondary`, bouton conservé | « …, 0 » |
| Cible verrouillée | bouton désactivé + cadenas + info-bulle « Accès non autorisé pour votre profil » ; chiffre lisible | « …, écran non autorisé pour votre profil » |
| Indisponible | compteur « — », pastille « Indisponible » (`StatusRejected`), légende « F5 pour réessayer · détail dans le bandeau de session » ; toutes les cartes d'une même source basculent ensemble | « {Libellé}, indisponible » |
| Sans unité | la carte n'est pas composée ; l'encart B' le dit une fois | — |

#### États vides des bandes (raisons typées — greffe Portail)

| Bande | `NoQueues` (aucune file lisible pour cette bande) | `UnitMissing` (seules des files unitaires étaient lisibles, unité du poste absente) | `AllClear` (slots composés, rien à montrer) |
|---|---|---|---|
| En retard | en-tête sur une ligne, atténué : « En retard · aucune file ouverte à votre profil » (pas de boîte) | en-tête sur une ligne : « En retard · dépend de l'unité du poste, non définie » — la bande ne prétend jamais « rien en retard » quand rien n'a été lu | boîte `EmptyState*` : « Rien en retard » / « Les arrivées, départs, clôtures, rejets et sauvegardes en retard apparaîtront ici. » |
| Aujourd'hui | boîte : « Rien à traiter » / « Aucune file de travail n'est ouverte à votre profil. Vos écrans restent accessibles par la barre latérale et le catalogue. » | boîte : « Rien à traiter » / « Vos files dépendent d'une unité : fixez l'unité de ce poste dans Paramétrage global › Poste de travail. » | impossible par construction (les zéros restent) ; si toutes les cartes sont indisponibles, elles s'affichent indisponibles |
| À surveiller | en-tête sur une ligne, atténué : « À surveiller · aucune file ouverte à votre profil » | en-tête sur une ligne : « À surveiller · dépend de l'unité du poste, non définie » | boîte : « Rien à surveiller » / « Chambres hors service, articles sous le minimum et postes apparaîtront ici. » |

Gabarit d'état vide : pictogramme au trait 28 px (`TextMutedBrush`, 1.3), `EmptyStateTitleText`,
`EmptyStateHintText`, bordure `PanelBorderBrush` en pointillé, `IsHitTestVisible=False` (charte § 3.5). Aucun texte
ne promet tâches, notifications ou messages.

### 3.5 F — Derniers écrans ouverts (ce poste)

| | |
|---|---|
| Contenu | `HomeSectionLabel` « Derniers écrans ouverts (ce poste) » + jusqu'à six **`Button`** style `HomeChipButton` (nouveau style : visuel de `FilterChip` non coché — `Surface`, `PanelBorder` 1 px, rayon 15, padding 14,6, 12,5 SemiBold `TextSecondary` ; survol `SurfaceSubtle`/`BorderStrong` ; focus `FocusRingBrush` — aucun brush nouveau) portant l'icône du domaine et « {Écran} · {Module} » du chemin primaire de l'arbre |
| Source | `DesktopSettings.RecentTabs` (liste d'index d'onglets, ≤ 6, **N'EXISTE PAS** : à créer, écrite par `NavigateToModule` hors onglet 0, même schéma load-modify-write qu'`Apparence`) ; libellés par `FunctionalArchitectureCatalog.TryGetPrimaryPath(tab)` |
| Permission | chaque puce est soumise à `CanOpenModule(tab)` à l'affichage : un onglet verrouillé pour le profil courant n'est pas listé (décision 7 du README) |
| État vide | la ligne entière est masquée |
| Action | clic / `Entrée` → `NavigateRequested(tab)` ; la puce est un bouton, pas un `RadioButton` (sémantique clavier et lecteur d'écran) |
| AutomationLabels | « Ouvrir {Écran}, {Module}, {Domaine} » |
| Honnêteté | « (ce poste) » est dans le libellé : sur un poste partagé, les récents sont ceux du poste, pas de la personne ; ce n'est pas la préfiguration de favoris par compte, qui n'existent pas |

### 3.6 G — « Où en est le produit ? »

| | |
|---|---|
| Contenu | `CardBorder` : titre 13,5 SemiBold avec `ModuleGroupIcon.Pilotage`, ligne « 31 modules disponibles sur 50 · domaines : 11 fonctionnels · 5 aperçus techniques · 6 planifiés · 0 prêt pour la production » (`MaturityDot.*`), mini-barre segmentée `HomeProgressSegment` 8 px, mention « chiffres du catalogue embarqué, pas du serveur » (`CaptionText`), `SecondaryButton` « Ouvrir le catalogue des modules » |
| Source | `ModuleCatalog.ExpectedAvailable` / `ExpectedTotal`, compteurs de maturité de `FunctionalArchitectureCatalog.Domains` — faits du binaire, admissibles hors § 3.10 parce qu'annoncés comme tels |
| Permission | aucune |
| Action | sélectionne la section « Catalogue des modules » |

### 3.7 Composition par permissions (tableau profil → sections)

Droits = `SecuritySeeder.RolePermissions` (HEAD `e7dcaad`), jamais un nom de rôle : le composeur ne reçoit que
des clés. Réception est un rôle personnalisé à créer (README, décision 8) ; les clés proposées sont celles de la
maquette. L'unité du poste est supposée réglée pour les postes d'unité (Réception, Directeur d'unité, Caisse,
Lecture seule) et absente pour les postes de siège (Direction, RH, Administrateur) — la maquette permet d'inverser.

| Profil (rôle, unité du poste) | Bandeau | En retard | Aujourd'hui | À surveiller | Appels | Ce qu'il ne voit pas, et pourquoi |
|---|---|---|---|---|---|---|
| **Réception** (personnalisé : `lodging.read/checkin/checkout/reserve/room_move`, `customers.read`, `crm.read`, `housekeeping.read`, `settings.read` ; unité) | date métier | Arrivées en retard, Départs en retard — À faire ; Journée à clôturer — **Suivi** (pas de `closing.close`), cible 5 fermée → repli 30 | Arrivées, Sans chambre affectée, Départs, Départs avec solde — À faire ; Chambres à préparer, Chambres à inspecter — Suivi | Chambres hors service | 5 (business-date, front-desk, arrivals, departures, board) | validations (`approvals.*` absents), finance, RH, système ; sans unité : encart + « Rien à traiter · UnitMissing » |
| **Directeur d'unité** (`unit.manager` ; unité) | date métier | Arrivées/Départs en retard, Journée à clôturer — À faire ; Journées non clôturées (Groupe) — À faire (`closing.close` : règle uniforme) ; Recettes rejetées — À faire (`revenue.write`) | Arrivées, Sans chambre affectée, Départs, Départs avec solde, Chambres à préparer, Chambres à inspecter, Validations (Ma décision), Recettes en brouillon, Commandes à réceptionner, Relevés HACCP — À faire ; Recettes à valider, OP à approuver (repli 20), Inventaires à valider, Commandes à approuver — Suivi ; Recettes J-1, Événements du jour — Information | Chambres hors service, Articles sous le minimum | 15 | trésorerie (`treasury.read` absent : la carte OP porte le chiffre DEC et ouvre le cockpit), créances, RH, sauvegarde, postes |
| **Direction générale** (`direction` ; siège, sans unité) | pas de date métier (« — unité du poste non définie ») ; encart sans unité | Journées non clôturées, Recettes rejetées — Suivi ; Créances > 90 j — Information ; Sauvegarde si `IsOverdue` — Suivi (pas de `maintenance.backup`) | Validations, OP à approuver, Inventaires à valider, Commandes à approuver — À faire ; Recettes à valider, OP à régler, Encaissements en brouillon (Groupe), Commandes à réceptionner, Relevés HACCP, Absences, Bulletins — Suivi ; Recettes J-1, Encaissé aujourd'hui — Information | Articles sous le minimum, Postes en service, Dernière sauvegarde (si à l'heure) | 16 | files unitaires (pas d'unité de poste au siège) ; rien de 01 au-delà des trois boutons ; le tableau de bord PDG reste à un clic |
| **Caisse** (`cashier` ; unité saisie dans Paramétrage, pas d'`units.read`) | date métier ; unité en code seul | Arrivées/Départs en retard — À faire ; Journée à clôturer — Suivi → repli 30 | Arrivées, Sans chambre affectée, Départs, Départs avec solde, Chambres à préparer (`housekeeping.write`), Recettes en brouillon, OP à régler, Encaissements en brouillon — À faire ; Chambres à inspecter — Suivi ; Encaissé aujourd'hui — Information | Chambres hors service | 9 | validations (`approvals.read` seul : aucun appel, aucune carte), tout pilotage |
| **RH** (`hr.manager` ; siège) | pas de date métier, pas d'encart (aucune file unitaire composée) | ligne « aucune file ouverte à votre profil » | Absences à approuver, Bulletins en brouillon — À faire | ligne « aucune file ouverte » | 2 | tout le reste : l'accueil le plus court, et utile |
| **Administrateur** (`system.administrator` ; siège par défaut) | encart sans unité | Journées non clôturées, Recettes rejetées — À faire ; Créances > 90 j ; Sauvegarde si en retard — À faire | toutes les files de groupe et « Ma décision », À faire ; les unitaires dès qu'une unité est réglée (jusqu'à 21 cartes : la densité Compact lui est destinée) | Articles sous le minimum, Postes, Dernière sauvegarde si à l'heure (+ Chambres hors service avec unité) | 16 (23 avec unité) | rien |
| **Lecture seule** (`reader` ; unité) | date métier | Arrivées/Départs en retard, Journée à clôturer (cible 5 ouvrable : `closing.read`), Journées non clôturées, Recettes rejetées — tous Suivi ; Créances > 90 j — Information ; suffixe « suivi seulement » | Arrivées, Sans chambre affectée, Départs, Départs avec solde, Chambres à préparer / à inspecter, Recettes à valider, OP à approuver (repli 20), Recettes en brouillon, Inventaires, Commandes à approuver / à réceptionner, Relevés HACCP — Suivi ; Recettes J-1, Événements — Information | Chambres hors service, Articles sous le minimum | 15 | aucun verbe nulle part ; validations absentes (pas de `decide`) ; ni trésorerie, ni RH, ni système |

Ce tableau est celui que la maquette produit avec son composeur (`composer()` / `cartesParBande()`), profil par profil ;
il devient le test paramétré du § 5.1.

Un profil sans donnée d'exploitation (rôle personnalisé réduit à `settings.read`) voit : le bandeau, les trois
boutons de compte, « Rien à traiter · NoQueues », deux lignes de bande atténuées, ses derniers écrans, la carte
produit et la section Catalogue. Aucune page blanche, aucune promesse.

### 3.8 Section « Catalogue des modules » (`ModuleCatalogView`)

Extraction **à l'identique** des blocs 2 à 4 de l'accueil actuel (`MainWindow.xaml` 611-944 hors en-tête ; ≈ 330
lignes XAML et ≈ 180 lignes de `MainWindow.Navigation.cs` 610-794 : `CollectionViewSource`, filtres domaine /
statut / priorité / recherche, `ModuleCatalogItemsControl`, état vide), avec les quatre corrections dues depuis
`navigation-shell.md` § 5.1 :

| Correction | Détail | Source |
|---|---|---|
| En-tête de domaine | regroupement **par domaine** `01`…`22` (un en-tête par domaine à la place des ~35 en-têtes « Domaine → Module » du lot NAV ; le module de l'arbre reste lisible en légende de pied de carte, `CaptionText`) ; `ModuleCatalogGroupHeaderTemplate` : icône `ModuleGroupIcon.<IconKey>` 16 px + « NN Nom » + compteur + **badge de maturité** `MaturityBadge.<Niveau>` ; `AutomationProperties.HeadingLevel=Level2`, `Name` « Domaine 06 PMS / Hébergement, 3 modules, Fonctionnel » ; le domaine `19` sans module garde son en-tête (« Aucun module rattaché : c'est la promesse du catalogue »), masqué dès qu'un filtre est actif ; badge **retiré** de la carte (`desktop:MaturityBadge.Maturity` et `MaturityBadgeFallback.*` supprimés) | `FunctionalArchitectureCatalog.Domains`, `NavigationModulePlacement` |
| Filtre Maturité | rangée `FilterChipCompact` « Maturité : Toutes · Fonctionnel · Aperçu technique · Planifié », croisée avec statut / priorité / domaine | `ModuleTile.FunctionalMaturity` |
| Noms accessibles | `ModuleCatalogCard` : `AutomationProperties.Name` « Ouvrir {Name}, {StatusLabel} » ; verrouillée : « {Name}, accès non autorisé pour votre profil » ; planifiée : « {Name}, planifié, aucun écran » ; `HelpText` = description | dette relevée par les trois lentilles |
| Recherche universelle | `HomeSearchTextBox` (nom conservé : `Ctrl+F`, `Ctrl+K`) filtre les cartes **et** affiche au-dessus d'elles une liste « Sous-modules et écrans (n) » : chemin `Domaine › Module › Sous-module`, pastille « Écran » si ouvrable, cadenas si non autorisé, badge « Planifié » si nœud sans écran ; `Entrée` ouvre le premier résultat ouvrable ; `Échap` efface. Source : `NavigationTreeBuilder.Build(Tree, AllPermissionKeys, NavigationFilter.Home with { SearchText, IncludeAliases = true })`, chaque écran comparé aux clés du profil pour le cadenas. Navigation seulement — aucune donnée n'est cherchée, et le texte indicatif le dit (« Rechercher un module, un écran… ») | `NavigationSearch`, `NavigationFilter.Home` |
| Textes | « 49 modules » → `ModuleCatalog.ExpectedTotal` (info-bulles, `CaptureTarget.cs`) | — |

Les puces de domaine (« Architecture fonctionnelle »), le bandeau « Avancement des modules » à deux lectures et les
50 `ModuleCatalogCard` restent tels quels : la section est le sommaire du produit, sa longueur n'est plus la
première image de l'accueil. `ModuleCatalog.cs`, `ModuleTile` (`IsLocked` posé par `ApplyModulePermissions`),
`ModuleTileNavigate_Click` et `CanOpenModule` ne changent pas.

---

## 4. Tokens et composants de la charte

| Usage | Ressource (toutes existantes sauf mention) |
|---|---|
| Sections | `SectionTabControl` / `SectionTabItem` (à promouvoir de `TreasuryView.xaml` vers `RaqmiTheme.xaml`, sinon copie locale) |
| Conteneurs | `CardBorder` (bandeau, carte de file, carte produit), `SubtleCardBorder` / `SurfaceSubtleBrush` (carte Suivi, carte à 0, squelettes), `WarningBanner` + `WarningBannerText` (à promouvoir de `TreasuryView` — dupliqués aujourd'hui dans quatre vues) |
| Textes | `HomeGreetingText` 26, `HomeDateText` 13, `SubtitleText` 12,5 (synthèse), `HomeSectionLabel` 11 (titres de bande et de rangée), `HomeStatValueText` 27 (compteur), `CaptionText` 11,5 (légendes, périmètre, fraîcheur), `MetricLabelText`, `EmptyStateTitleText` / `EmptyStateHintText`, `LabelText` |
| Pastilles | `StatusSubmittedBackground/Foreground` (point et compteur de « En retard », date métier en retard), `StatusValidated*` (date métier à jour), `StatusDraft*` (« Suivi », compteur d'« À surveiller »), `StatusRejected*` (« Indisponible »), `MaturityBadge.*` / `MaturityDot.*` (catalogue et carte produit seulement), `ModuleCardLockIcon` |
| Boutons | `SecondaryButton` (verbe d'une carte, Actualiser, Ouvrir le catalogue, Réessayer), `GhostButton` (Voir, Mon profil / Mes préférences / Ma sécurité, Changer, lien de l'encart), **`HomeChipButton`** (nouveau, dérivé visuel de `FilterChip` pour un `Button` : derniers écrans), `FilterChip` / `FilterChipCompact` (filtres du catalogue, inchangés), `SearchClearButton` |
| Icônes | `ModuleGroupIcon.<Clé>` du domaine cible en tête de carte (16 px, trait 1.4, `TextMutedBrush`), `ModuleGroupIcon.Pilotage` sur la carte produit, `ModuleGroupIcon.MonEspace` sur `ShowHomeButton` ; pictogrammes d'état vide au trait 28 px |
| Points de bande | 8 px : `StatusSubmittedForegroundBrush` (En retard), `AccentBrush` (Aujourd'hui — trait, jamais de texte), `TextMutedBrush` (À surveiller) ; toujours doublés du mot |
| Mouvement | `HomeRevealStyle` / `HomeRevealDelayedStyle` / `HomeRevealDelayedMoreStyle` sur bandeau, bande En retard, reste ; aucune animation sur les chiffres |
| Densité | **`WorkCardPadding`** (`Thickness` 16,14 / 12,10) et **`WorkCardMinHeight`** (`Double` 104 / 88), nouvelles, en `DynamicResource`, posées par `ThemeManager.AppliquerDensite` à côté de `GridRowHeight` |
| Couleur | **aucun brush nouveau** : `ThemePalette.Sombre` reste à 82/82, `VerifierCouverture` inchangé ; les accents se posent sur `SurfaceBrush` (carte), jamais sur `AppBackgroundBrush` |
| Retour d'information | `SetStatus` → bandeau de session + `FlashSessionStrip` ; `BusyProgressBar` pendant chaque appel ; jamais de `MessageBox` |

Contraste : toutes les paires sont celles de la charte (`TextSecondary` 7,1:1, `TextMuted` 4,6:1 sur `SurfaceSubtle`,
`StatusSubmittedForeground` 5,0:1) ; l'accent de marque ne porte que des points et des filets.

---

## 5. Plan de découpage WPF

### 5.1 Composition pure — `src/RaqmiSystem.Application/Navigation/`

Testable sans WPF (`tests/RaqmiSystem.Tests` ne référence pas Desktop), sur le modèle de `NavigationTreeBuilder`.

| Fichier (à créer) | Contenu |
|---|---|
| `HomeWorkQueueCatalog.cs` | le registre § 3.4 : `IReadOnlyList<HomeWorkQueueDefinition> Queues` ; `record HomeWorkQueueDefinition(string Id, string Label, HomeBand Band, HomeScope Scope, string ReadKey, string? ActKey, HomeSource Source, int TargetTab, string TargetReadKey, int? FallbackTab, string? FallbackReadKey, string ActVerb, string WatchVerb, string Legend)` ; enums `HomeBand { Overdue, Today, Watch }`, `HomeScope { Unit, Group, Me, System }`, `HomeMode { Act, Watch, Information }`, `HomeSource` (une valeur par appel : `BusinessDate, PendingApprovals, FrontDesk, ArrivalBoard, DepartureBoard, HousekeepingBoard, RevenueSummary, ReceiptsDraft, ReceiptsConfirmed, LowStock, PaymentOrdersApproved, PurchaseOrdersDraft, PurchaseOrdersApproved, InventoryCountsDraft, AbsencesRequested, PayrollPeriods, EventsToday, HaccpReadings, BackupStatus, Workstations, UnitDashboardYesterday, Aging, DecCockpit` — **dans l'ordre d'appel, du plus léger au plus lourd**) ; `HomeSourceRoute` (chaîne documentaire de la route et de sa clé, pour le test de registre) |
| `HomeComposer.cs` | `static HomeLayout Compose(IReadOnlySet<string> grantedKeys, bool hasStationUnit)` — **fonction pure** : `has(key) = PermissionRegistry.AcceptedClaims(key).Any(grantedKeys.Contains)` ; pour chaque file : absente sans clé de lecture ; `Mode` = `Information` si `ActKey` nul, `Act` si détenue, `Watch` sinon ; `Scope == Unit && !hasStationUnit` → comptée dans `UnitQueuesSkipped`, non composée ; cible = `TargetTab` si `has(TargetReadKey)`, sinon `FallbackTab` si `has(FallbackReadKey)`, sinon `TargetTab` avec `TargetLocked = true` ; rend les **sections dans l'ordre** (`Banner`, `Overdue`, `Today`, `Watch`, `RecentScreens`, `Product`) avec, par bande, ses slots triés (Act, Watch, Information, puis ordre du registre) et sa `HomeEmptyReason` (`NoQueues`, `UnitMissing`, `None`) ; `Sources` = sources distinctes des slots composés, dans l'ordre de l'enum ; `ShowBusinessDate = has(lodging.read) && hasStationUnit` ; `ShowUnitMissingBanner = UnitQueuesSkipped > 0` |
| `HomeLayout.cs` | `record HomeLayout(IReadOnlyList<HomeSection> Sections, IReadOnlyList<HomeSource> Sources, bool ShowBusinessDate, bool ShowUnitMissingBanner, int UnitQueuesSkipped)` ; `record HomeSection(HomeSectionKind Kind, IReadOnlyList<HomeSlot> Slots, HomeEmptyReason EmptyReason)` ; `record HomeSlot(HomeWorkQueueDefinition Queue, HomeMode Mode, int TargetTab, bool TargetLocked)` |
| `HomeSourceResults.cs` | sac mutable des **records de réponse existants** (`FrontDeskResponse?`, `BusinessDateResponse?`, `ArrivalBoardResponse?`, `DepartureBoardResponse?`, `RoomBoardResponse?`, `IReadOnlyCollection<ApprovalInstanceResponse>?`, `DecCockpitResponse?`, `UnitDashboardResponse?`, `DailyRevenueSummaryResponse?`, `CashReceiptSummaryResponse?` ×2, `IReadOnlyCollection<PaymentOrderResponse>?`, `IReadOnlyCollection<PurchaseOrderResponse>?` ×2, `IReadOnlyCollection<InventoryCountResponse>?`, `IReadOnlyCollection<AbsenceResponse>?`, `IReadOnlyCollection<PayrollPeriodResponse>?`, `IReadOnlyCollection<EventBookingResponse>?`, `IReadOnlyCollection<TemperatureReadingResponse>?`, `IReadOnlyCollection<LowStockRow>?`, `BackupStatusResponse?`, `WorkstationRegistryResponse?`, `AgingBalanceResponse?`) + `ISet<HomeSource> Failed` |
| `HomeProjection.cs` | `static HomeCard Project(HomeSlot slot, HomeSourceResults results, string? currencyLabel)` — **fonction pure des réponses** : bande finale (registre ou booléen serveur), `Count` (entier ou montant formaté `N2` + devise), `Amount?`, `Legend`, `State` (`Loading`, `Ready`, `Unavailable`), `IsZero`, `IsHidden` (zéro hors Aujourd'hui, `closing-unit` sans `IsLate`) ; `record HomeCard(...)` sans dépendance WPF |

Tests (`tests/RaqmiSystem.Tests`) :

- `HomeComposerTests` : par jeu de clés `Only(...)` — une clé de lecture → sa file ; action absente → `Watch` ;
  `Only("approvals.read")` → aucune file `approvals`, source `PendingApprovals` absente ;
  `Only("workflow.request.decide")` → file `approvals` (alias) ; `Only("lodging.read")` sans unité → aucun slot,
  `UnitQueuesSkipped > 0`, `Today.EmptyReason == UnitMissing`, `ShowUnitMissingBanner` ; `Only("settings.read")` →
  `NoQueues` sur les trois bandes ; cible de repli (`dashboard.read` sans `treasury.read` → `dec-po` cible 20) ;
  cible verrouillée sans repli ; ordre des sources ; sections rendues dans l'ordre.
- **Table de vérité par rôle seedé** : `SecuritySeeder` sur SQLite en mémoire
  (`SecuritySeederTests.CreateSeededContextAsync`) → clés effectives des sept rôles → le tableau § 3.7 devient un
  test paramétré (`[Theory]` : rôle, unité oui/non → identifiants de slots attendus par bande). Toute dérive du
  seeder ou du registre casse le test : c'est voulu.
- `HomeProjectionTests` : `BackupStatusResponse(IsOverdue: true)` → bande Overdue et libellé « Sauvegarde en
  retard » ; `BusinessDateResponse(IsLate: false)` → `closing-unit` masquée ; `FrontDeskResponse` à 0
  `OverdueArrivals` → masquée, `Arrivals` à 0 → visible et `IsZero` ; source dans `Failed` → `Unavailable` pour
  toutes ses cartes ; `CashReceiptSummaryResponse` sans `status=Confirmed` → `GrandTotal` jamais lu ;
  `PurchaseOrderResponse.CanReceive` compté, pas le statut.
- `HomeRegistryRoutesTests` (`RaqmiApiFactory`) : pour chaque `HomeSourceRoute` du registre, l'`EndpointDataSource`
  de l'API contient un `MapGet` de ce chemin dont la politique (`AuthorizeAttribute.Policy`) est dans
  `PermissionRegistry.AcceptedClaims(ReadKey)` — le garde de readiness appliqué aux files.
- `ApprovalsPendingByRoleTests` (`RaqmiApiFactory`) : `GET /approvals/instances/pending` répond 200 aux quatre
  décideurs (`system.administrator`, `direction`, `exploitation.control`, `unit.manager`) et 403 à `cashier`,
  `hr.manager`, `reader` — le contrat que la carte `approvals` consomme.

### 5.2 Desktop — fichiers

| Fichier | Rôle |
|---|---|
| `Views/HomeView.xaml(.cs)` (**créer**) | hôte de l'onglet 0 : `SectionTabControl` [ `WorkQueuesView`, `ModuleCatalogView` ]. Expose `Initialize(ModuleViewContext)`, `OpenSession(AuthenticatedUser user)`, `LoadAsync()`, `RefreshIfStaleAsync()`, `ResetState()`, `FocusCatalogSearch()`, `RecordVisit(int tab)` ; événements `NavigateRequested(int)` et `ChangePasswordRequested()` relayés des deux enfants ; reçoit à la construction `IReadOnlyList<ModuleTile>` (les 50 tuiles de la fenêtre) et `Func<int,bool> canOpenModule` |
| `Views/WorkQueuesView.xaml(.cs)` (**créer**) | la section « Mon travail », contrat de vue § 2.1 : `Initialize(context)` sans réseau ; `LoadAsync()` (§ 5.3) ; `ResetState()` (salutation « Bonjour », cartes vidées, encarts masqués, fraîcheur effacée — les réglages de poste restent) ; `RefreshHomeButton` ; `ItemsControl` par bande sur `ObservableCollection<HomeCardModel>` ; `DataTemplate HomeWorkCardTemplate` |
| `Views/ModuleCatalogView.xaml(.cs)` (**créer**) | extraction § 3.8 ; garde `HomeSearchTextBox`, `ClearHomeSearchButton`, les puces et l'`ItemsControl` ; ajoute la liste de résultats et le filtre Maturité ; reçoit `IReadOnlyList<ModuleTile>`, `Func<IReadOnlySet<string>> grantedKeys`, `Action<int> navigate` |
| `HomeCardModel.cs` (**créer**) | `INotifyPropertyChanged` sur `HomeCard` : `State`, `Count`, `Amount`, `Legend`, `Mode`, `IsTargetLocked`, `Band`, `ScopeLabel`, `TargetLabel`, `IconKey`, `ButtonText`, `ToolTipText`, `AutomationName` |
| `DesktopSettings.cs` (**modifier**) | `StationUnitCode` (`string?`) + `LoadStationUnitCode` / `SaveStationUnitCode` ; `RecentTabs` (`int[]`, ≤ 6) + `LoadRecentTabs` / `SaveRecentTabs` ; load-modify-write |
| `Views/SettingsView.xaml(.cs)` (**modifier**) | section Poste de travail : champ « Unité de ce poste » (`ComboBox` sur `GET /organization/hotel-units` si `units.read`, `TextBox` du code sinon, `MaxLength` = borne de `HotelUnit.Code`) ; encart « confort de poste, jamais un périmètre : le serveur reste seul juge » |
| `Themes/RaqmiTheme.xaml` (**modifier**) | `WorkCardPadding`, `WorkCardMinHeight` (à côté de `GridRowHeight`) ; `HomeChipButton` ; `WarningBanner` + `WarningBannerText` promus ; `SectionTabItem` / `SectionTabControl` promus ; `HomeWorkCardTemplate` ; `ModuleCatalogGroupHeaderTemplate` avec icône, badge, `HeadingLevel` ; `ModuleCatalogCard` avec `AutomationProperties.Name` / `HelpText` ; aucun brush |
| `ThemeManager.cs` (**modifier**) | `AppliquerDensite` pose aussi `WorkCardPadding` et `WorkCardMinHeight` |
| `MainWindow.xaml` (**modifier**) | contenu du `TabItem` 0 (611-944) → `<views:HomeView x:Name="HomeView" NavigateRequested="HomeView_NavigateRequested" ChangePasswordRequested="HomeView_ChangePasswordRequested"/>` — la balise `<TabItem Header="Accueil">` reste la première, sans `x:Name` ; `ShowHomeButton` : libellé « Mon Espace », `ToolTip` « Mon Espace (Alt+Origine) », `AutomationProperties.Name` « Mon Espace, accueil » ; ressources `MaturityBadgeFallback*` supprimées ; info-bulles « 49 modules » |
| `MainWindow.xaml.cs` (**modifier**) | `HasModulePermission` : `currentUserPermissions is null || PermissionRegistry.AcceptedClaims(permission).Any(claim => currentUserPermissions.Contains(claim, OrdinalIgnoreCase))` ; `LoginButton_Click` 238-242 : `HomeView.OpenSession(login.User)` à la place de `HomeGreetingTextBlock` / `RefreshHomeDate`, puis après `NavigateToModule(HomeTabIndex)` : `await HomeView.LoadAsync()` (le préchargement Units / Revenue / Dashboard des onglets 1-3 est conservé) ; `InitializeModuleViews` : `HomeView.Initialize(context)` + abonnements (désabonnement préalable, comme `DecCockpitView`) ; `LogoutButton_Click` 484-490 : `HomeView.ResetState()` ; `RefreshHomeDate` et `HomeGreetingTextBlock` disparaissent |
| `MainWindow.Navigation.cs` (**modifier**) | 610-794 (catalogue) déplacés dans `ModuleCatalogView` ; `NavigateToModule` : `+ if (tabIndex != HomeTabIndex) HomeView.RecordVisit(tabIndex)` ; `HomeView_NavigateRequested` = garde `CanOpenModule` + `NavigateToModule` (copie de `DecCockpitView_NavigateRequested`) ; `EnsureModuleTabLoadedAsync` : `case 0 : loadedModuleTabs.Remove(0); await HomeView.RefreshIfStaleAsync(); break;` ; `GrantedPermissionKeys()` étendu par équivalence `AcceptedClaims` (la barre latérale suit le même correctif) ; `EnsureMaturityBadgeStyles` supprimé ; `SyncSidebarToTab` et `UpdateBreadcrumb` **inchangés** |
| `MainWindow.Shortcuts.cs` (**modifier**) | `Ctrl+K` sur l'onglet 0 → `HomeView.FocusCatalogSearch()` (sélectionne la section puis focus) ; `Alt+Origine` → `NavigateToModule(0)` + `HomeView.ShowWorkSection()` |
| `Views/ShortcutsWindow.xaml`, `tools/RaqmiSystem.DocShots/CaptureTarget.cs` (**modifier**) | « Accueil » → « Mon Espace » ; « 49 » → 50 ; cible 0 : « Mon Espace — Mon travail » (contenu stable après `LoadAsync`, aucune modale : DocShots attend déjà l'inactivité du dispatcher) |

Ce qui **ne change pas** : l'ordre des 31 `<TabItem>`, les 30 `x:Name`, les 30 appels littéraux
`ApplyModuleAccess(PermissionCatalog.X, XTabItem)` (`tools/check-module-readiness.ps1` passe sans modification),
`ModuleCatalog.cs`, `ModuleTile`, `ModuleNavigationGroup`, la barre latérale (repliée sur l'onglet 0), le fil
d'Ariane, `CanOpenModule`, `ModuleTileNavigate_Click`, les 50 cartes (mêmes gestionnaires, même onglet 0).

### 5.3 Contrat de vue et chargements — `WorkQueuesView.LoadAsync`

```csharp
public async Task LoadAsync()
{
    if (context is null || !context.ApiClient.IsAuthenticated || isLoading) return;
    isLoading = true;
    try
    {
        var layout = HomeComposer.Compose(grantedKeys(), stationUnit is not null);
        RenderSkeleton(layout);                                   // cartes en état Loading, encarts masques
        var results = new HomeSourceResults();

        foreach (var source in layout.Sources)                    // du plus leger au plus lourd
        {
            var ok = false;
            await context.RunAsync(async () =>                    // charte § 3.1 : une RunAsync PAR source
            {
                await FetchAsync(source, results);                // ecrit le record de reponse dans results
                ok = true;                                        // pose seulement si l'appel a abouti
            });
            if (!ok) results.Failed.Add(source);                  // RunAsync a deja affiche l'erreur (§ 3.12)
            ProjectSource(source, layout, results);               // seules les cartes de CETTE source changent d'etat
        }

        RenderBanners(results.Failed, layout.ShowUnitMissingBanner);
        RenderSynthesis(layout);                                  // LiveSetting=Polite
        lastLoadedUtc = DateTimeOffset.UtcNow;                    // « actualise a HH:mm » en heure locale
    }
    finally { isLoading = false; }
}

public Task RefreshIfStaleAsync() =>
    DateTimeOffset.UtcNow - lastLoadedUtc > TimeSpan.FromMinutes(5) ? LoadAsync() : Task.CompletedTask;
```

- **Aucun `try/catch` réseau dans la vue** : `RunApiActionAsync` traduit l'erreur en message d'état ; la vue ne sait
  qu'une chose, si son délégué a posé le drapeau.
- **Erreur partielle** : une source en échec bascule ses cartes en « Indisponible » et n'arrête pas les suivantes ;
  l'encart agrégé les nomme ; `F5` relance tout. Un 401 suit le même chemin (message du bandeau de session).
- **Gel de `MainTabs`** pendant chaque appel (`SetBusy`) : accepté en v1, borné par le nombre et le poids des
  routes ; la barre latérale reste active, les cartes se remplissent au fil des réponses.
- **Quand** : après `ApplyModulePermissions` et `NavigateToModule(0)` à la connexion ; `F5`
  (`RefreshHomeButton`) ; retour sur l'onglet 0 au-delà de cinq minutes. Jamais de `Timer`.
- **Hors session** : `LoadAsync` sort ; `ResetState` vide tout à la déconnexion (les vues survivent à la
  déconnexion et resservent au profil suivant).
- **Unité du poste** : lue dans `DesktopSettings` à chaque `LoadAsync` (changer le réglage puis `F5` suffit) ; le
  serveur valide le code au premier appel — un code faux donne des cartes « Indisponible » avec le message du
  serveur, jamais un chiffre inventé.

### 5.4 Lots de livraison

| Lot | Contenu | Dépend de |
|---|---|---|
| 0 — Catalogue | `ModuleCatalogView` extraite à l'identique + quatre corrections § 3.8 ; `HomeView` hôte avec la seule section Catalogue (l'onglet 0 est visuellement inchangé) ; `HasModulePermission` et `GrantedPermissionKeys` par `AcceptedClaims` ; textes « 49 » ; suppression des replis morts | — |
| 1 — Composition | `HomeWorkQueueCatalog`, `HomeComposer`, `HomeLayout`, `HomeProjection`, `HomeSourceResults` + les cinq familles de tests § 5.1 | — |
| 2 — Mon travail | `WorkQueuesView` (bandeau, bandes, états, encarts, F5), `HomeCardModel`, `HomeWorkCardTemplate`, `WorkCardPadding` / `WorkCardMinHeight`, `WarningBanner` et `SectionTab*` promus, câblage `MainWindow` (§ 5.2), `RecentTabs`, `StationUnitCode` + champ de `SettingsView` | 0, 1 |
| 3 — Vocabulaire | « Mon Espace » sur `ShowHomeButton`, `ShortcutsWindow`, DocShots ; captures des sept accueils par compte de démonstration | 2 |

---

## 6. Critères d'acceptation

Chaque critère est vérifiable par un test automatique, par le garde de readiness ou par le protocole de smoke
(`docs/module-readiness.md`).

1. `tools/check-module-readiness.ps1` passe sans modification : 31 `<TabItem>` dans le même ordre, 30 `x:Name`,
   30 `ApplyModuleAccess` littéraux, `ModuleCatalog` à 50 / 31 / 0 / 0 / 19.
2. `HomeComposer.Compose` est déterministe, sans dépendance WPF ni réseau ; les sept rôles seedés produisent
   exactement les slots du tableau § 3.7 (test paramétré sur `SecuritySeeder` / SQLite).
3. `Only("approvals.read")` ne compose pas la file `approvals` et ne planifie pas l'appel `/pending` ;
   `Only("workflow.request.decide")` la compose (alias).
4. Une clé de lecture sans clé d'action donne le mode `Watch` (bouton « Voir », pastille « Suivi ») ; une file sans
   clé d'action est `Information` ; le tri d'une bande est Act, Watch, Information, puis registre.
5. Sans unité de poste, aucune file de périmètre Unité n'est composée ; `ShowUnitMissingBanner` est vrai si et
   seulement si au moins une l'aurait été ; la bande Aujourd'hui porte `UnitMissing` quand seules des files
   unitaires étaient composables.
6. Une cible fermée avec repli ouvrable bascule sur le repli ; sans repli, le bouton est désactivé avec cadenas et
   info-bulle « Accès non autorisé pour votre profil », et le chiffre reste affiché.
7. Aucune carte ne place un objet dans « En retard » sur un seuil client : la bande vient du registre ou de
   `IsLate` / `IsOverdue` ; les trois placements éditoriaux sont ceux documentés § 3.4 (test de registre).
8. Aucun montant n'est calculé côté client : seuls `PendingValidationAmount`, `PendingPaymentOrderAmount`,
   `OutstandingBalance`, `Total.Over90`, `GrandTotal` (dashboard J-1, reçus `status=Confirmed`) sont affichés ; les
   cartes « nombre de lignes » n'ont pas de montant (revue de `HomeProjection`).
9. Chaque `HomeSourceRoute` correspond à un `MapGet` de l'API dont la politique est acceptée par la clé de lecture
   déclarée (`HomeRegistryRoutesTests`).
10. `GET /approvals/instances/pending` : 200 pour les quatre décideurs, 403 sinon (`ApprovalsPendingByRoleTests`).
11. `WorkQueuesView` respecte le contrat § 2.1 : `Initialize` sans appel réseau ; `LoadAsync` sort hors session ;
    une `context.RunAsync` par source ; aucun `try/catch` réseau dans la vue (revue) ; `ResetState` vide cartes,
    salutation, encarts, fraîcheur et laisse `DesktopSettings` intact.
12. Une source en échec laisse les autres cartes chargées, bascule les siennes en « Indisponible », et l'encart
    nomme les écrans concernés ; `F5` recharge tout ; le message HTTP est dans le bandeau de session.
13. Les zéros : masqués dans En retard et À surveiller, visibles atténués dans Aujourd'hui.
14. Le retour sur l'onglet 0 relance `LoadAsync` seulement si la dernière lecture date de plus de cinq minutes ;
    aucun `Timer` n'existe dans la vue.
15. Les 50 cartes restent atteignables dans l'onglet 0 (section Catalogue), avec leurs filtres, leur recherche,
    leurs cadenas et leur état vide ; `Ctrl+K` sélectionne la section et focalise `HomeSearchTextBox` ; `Entrée`
    dans la recherche ouvre le premier écran ouvrable des résultats.
16. L'en-tête de domaine du catalogue porte icône, compteur et badge de maturité ; aucune carte ne porte de badge de
    maturité ; le filtre Maturité se croise avec les autres.
17. Accessibilité : chaque carte a un `AutomationProperties.Name` composé ; les boutons ont un `Content` textuel ;
    en-têtes de bande en `HeadingLevel=Level2`, salutation en `Level1` ; synthèse et encart d'échec en
    `LiveSetting=Polite` ; ordre de tabulation : onglets de section → boutons du bandeau → Actualiser → cartes bande
    par bande → derniers écrans → Ouvrir le catalogue ; `F5` trouve `RefreshHomeButton`, `Ctrl+F` trouve
    `HomeSearchTextBox` sur la section Catalogue ; la couleur n'est jamais seule (mot, pastille, cadenas).
18. Thème : aucun hexa dans le XAML ni le code de la vue ; `ThemePalette.Sombre` reste à 82/82 ;
    `VerifierCouverture` ne lève rien ; la maquette rend les deux thèmes.
19. Densité : changer Compact repeint les cartes déjà affichées (`WorkCardPadding` / `WorkCardMinHeight` en
    `DynamicResource`) sans changer une taille de police.
20. Hors session : l'onglet 0 est invisible (`MainContentGrid` masqué) ; à la reconnexion d'un autre profil, aucune
    carte ni salutation du profil précédent ne subsiste.
21. DocShots capture l'onglet 0 chargé (cible « Mon Espace — Mon travail ») sans intervention.

---

## 7. Hors périmètre (explicite)

| Sujet | Statut | Où il ira |
|---|---|---|
| Notifications, messagerie, tâches transverses, agenda, favoris, documents, demandes, délégations | **jamais simulés** : nœuds « Planifié » de l'arbre, visibles dans le catalogue et la recherche avec leur badge | phase 4 (lot 4.2 / 4.3) : une file de plus dans le registre par service livré — les tâches à échéance iront dans « En retard », les notifications dans « Aujourd'hui » — sans refonte |
| « Mon activité » (5 dernières traces, `audit.read`) | reporté | lot suivant : paramètre `userId` de `RaqmiApiClient.GetAuditLogAsync` (la route et `IAuditQueryService` l'acceptent) |
| Alertes KPI (`GET /kpis/alerts`, méthode cliente absente), bande KPI moteur à la demande, grille « Unités — santé du jour » | reporté | extension sous `dashboard.read`, à la demande, jamais à l'ouverture |
| `RunAsync(quiet: true)` (lecture pure sans `SetBusy`) | décision du propriétaire de la charte | instruite séparément ; la v1 accepte le gel intermittent |
| Paramètre `hotelUnitCode` sur `/pilotage/dec-cockpit` ; champ `IsOverdue` sur `DecPendingValidationUnit` ; total « échu » serveur sur `AgingBucketsResponse` | serveur | les trois améliorations qui rendraient l'accueil plus fin sans y mettre une règle métier |
| Affectation utilisateur ↔ unité côté serveur | décision 4 du README | remplacera `StationUnitCode` sans toucher au composeur |
| Navigation vers un sous-onglet (`SelectSection` des vues) | vague suivante | les cartes ouvriront alors le sous-onglet qui agit |
| `PartiallyReceived` dans « Commandes à réceptionner » | v1 = `status=Approved` + `CanReceive` | filtre multi-statut côté API |
| Rôle système Réception | décision 8 du README | clés proposées § 3.7 |
| Sélecteur d'unité dans le bandeau, mémorisation de la section, catalogue déplié par défaut pour l'administrateur | écartés | — |

---

## Annexe — corrections documentaires à faire dans le même lot

- `03-cartographie-cible.md` § 01 : « Tableau de bord personnel » passe d'*Absent* à *Partiel* (files de travail
  composées par permission, sans entité propre) le jour où le lot 2 est livré ; la maturité du domaine 01 reste
  `Planned` (calculée, jamais saisie).
- `03-cartographie-cible.md` § 3.5 et `navigation-shell.md` § 9 sur-promettent `unit.manager` (`hr.read`,
  `revenue.validate`, trésorerie) et `cashier` (`customers.read`, `invoices.*`) : aligner sur `SecuritySeeder`.
- `navigation-shell.md` § 11 : questions 3 (libellé « Mon Espace ») et 6 (numéro de domaine visible sur l'accueil,
  masqué dans la barre latérale) tranchées par ce document.
