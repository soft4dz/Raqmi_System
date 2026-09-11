# 03 — Avis UI, UX et design

**Révision analysée :** `b412b8c` (11 septembre 2026)
**Sources lues :** `docs/charte-ui-desktop.md` (716 l.), `docs/design/` (2 490 l. au total),
`src/RaqmiSystem.Desktop/Themes/RaqmiTheme.xaml` (2 589 l.), les 37 fichiers XAML,
`assets/brand/raqmi-system/`.

**Limite :** l'application n'a pas pu être lancée (pas de SDK .NET, et le client est WPF/Windows).
Cet avis porte sur le système de design, la charte, les maquettes et le XAML — **pas sur le
ressenti d'usage réel**.

---

## 1. L'identité de marque : excellente

- Le symbole **fusionne le `Q` latin et le `ق` arabe**. Idée juste, culturellement ancrée, et qui
  fonctionne à 32 px comme à 1024.
- Palette navy `#071525` / bleu `#145CAB` / teal `#0AA3AD` : sobre, crédible pour du financier,
  suffisamment distinctive.
- Déclinaisons complètes : horizontal, bilingue, monochrome, blanc, icône d'application en huit
  tailles, clair/sombre, papeterie, cartes de visite, splash screen, signature e-mail.
- Baseline « Un système. Toute votre entreprise. » — courte et positionnante.

**Niveau d'un éditeur établi. Rien à corriger.**

---

## 2. Le système de design : exceptionnel

C'est la meilleure surprise du projet, et le mot est pesé.

### 2.1 Le contraste est calculé, pas espéré

La charte donne le ratio de chaque token, et en tire les conséquences. Le teal de marque
`#0AA3AD` « ne pèse que 3,06:1 sur blanc — assez pour un trait, pas pour du texte ». D'où **deux
rôles d'accent** :

- `AccentBrush` `#0AA3AD` — traits, pastilles, bordures ; **jamais de texte** ;
- `AccentActionBrush` `#07767D` — même teinte (H 184) assombrie pour atteindre **5,39:1**, pour
  toute surface pleine portant du texte ou un glyphe.

C'est le raisonnement d'un designer système senior, et presque personne ne le documente.

### 2.2 Le focus clavier a trois tokens distincts

`FocusRingBrush`, `FocusRingOnFilledBrush`, `FocusRingOnDarkBrush`. Justification imparable : un
token unique tomberait à **1,17:1** en thème sombre sur l'en-tête, parce que la surface d'action
s'éclaircit quand l'en-tête reste sombre.

### 2.3 Les états désactivés restent lisibles (4,52:1)

Contre l'exemption WCAG, avec la bonne raison : *« c'est par la désactivation qu'un profil en
lecture seule apprend ce qu'il ne peut pas faire »*. C'est de l'UX, pas de la conformité.

### 2.4 Couverture des deux thèmes

**82 brushes sur 82** dans les deux palettes, avec assertion en Debug (`ThemeManager.
VerifierCouverture`) si la table et le thème divergent. La barre de défilement a été corrigée
parce qu'elle était à 1,44:1 — un détail que la plupart des produits ignorent.

### 2.5 Et la charte est réellement respectée

Vérifié plutôt que cru :

| Règle annoncée | Vérification dans le code |
|---|---|
| Aucune couleur en dur dans les écrans | **3 619 `StaticResource`** contre **7 couleurs en dur** — 99,8 % |
| États vides explicites | **162** occurrences d'`EmptyState` |
| Info-bulles explicatives | **492 `ToolTip`** |
| Bornes de saisie alignées sur le domaine | **166 `MaxLength`** |
| Message d'état plutôt que boîte de dialogue | **46 `MessageBox`** seulement, pour les confirmations |
| Conventions de nommage clavier | `Refresh*` 53, `Save*` 36, `New*` 28, `Search*` 10 |

### 2.6 Le processus de conception est du même niveau

- **Accueil** : 4 explorations concurrentes, jury à **trois lentilles** (métier /
  design-accessibilité / faisabilité WPF), notation chiffrée (155 / 131 / 130 / 124), concept
  gagnant vérifié route par route contre les permissions réellement semées par `SecuritySeeder`.
- **Barre latérale** : 16 problèmes diagnostiqués **dans le code** à partir d'une seule phrase
  (« elle ne me plaisait pas »), 4 propositions, vetos respectés, coût chiffré à 8,25 jours, et le
  **prix visible assumé par écrit** (« l'accueil perd 260 px, la grille passe de 5 à 4 cartes par
  rangée »).

Annoncer ce qu'on perd, pas seulement ce qu'on gagne, est un signe de maturité.

Le concept d'accueil retenu est le bon : **« qu'est-ce qui attend un geste de ma part,
maintenant ? »**, files de travail comptées par le serveur, trois bandes d'urgence, chaque carte
ouvrant l'écran qui agit. Le catalogue de modules relégué au second plan est également juste.

---

## 3. Ce qui ne va pas

### 3.1 — L'accessibilité est écrite, pas implémentée

| Annotation | Occurrences |
|---|---:|
| `AutomationProperties.Name` | 25 |
| `HeadingLevel` | 9 |
| `LiveSetting` | 5 |
| `HelpText` | 4 |
| **Total** | **≈ 47** |
| Pour… | **991 `DataGrid`** et **71 214 lignes** d'UI |

**Environ une annotation pour 1 500 lignes d'interface.** L'intention existe
(`AutomationLabels.cs`, messages d'état annoncés) mais a été appliquée à ≈ 1 % du produit. Sur un
marché public algérien (EPE, groupes hôteliers publics), l'accessibilité devient contractuelle.

### 3.2 — Aucune validation de formulaire native : le point UX le plus coûteux

| Mécanisme WPF | Occurrences |
|---|---:|
| `IDataErrorInfo` | **0** |
| `INotifyDataErrorInfo` | **0** |
| `Validation.ErrorTemplate` | **0** |

Toute la validation est impérative, en code-behind, et remonte dans un **message d'état global en
haut de l'écran**. Conséquence : sur l'écran RH ou une écriture comptable, un utilisateur qui
remplit vingt champs et se trompe sur un seul reçoit « Le montant est invalide » — **sans savoir
lequel**. Pas de bordure rouge sur le champ fautif, pas de message sous le champ, pas de focus
automatique sur l'erreur.

C'est la différence entre un logiciel qu'on tolère et un logiciel qu'on aime utiliser.

### 3.3 — Rigidité de mise en page et absence de gestion du DPI

- **1 443 largeurs fixes** (`Width="123"`) dans les vues.
- `MinWidth="1100"`, `MinHeight="680"`, fenêtre par défaut **1240 × 760**.
- **Aucun manifeste applicatif**, donc aucune déclaration DPI : WPF est *System DPI aware* par
  défaut, pas *PerMonitorV2*.

Conséquences réelles : sur un poste de réception en **1366 × 768** (très courant en Algérie), la
fenêtre à 760 px de haut ne rentre pas sous la barre des tâches. Sur un portable à **125 %** de
mise à l'échelle, la largeur utile tombe à 1536 et les largeurs fixes commencent à se marcher
dessus. Sur un écran 4K ou un double écran de DPI différents, **l'application sera floue**.

Le manifeste DPI représente dix lignes de XML.

### 3.4 — La dérive de la charte a déjà commencé

C'est le signal le plus important, parce qu'il annonce ce qui se passera sur les 18 modules
restants.

- **Les 7 couleurs en dur sont toutes dans `PmsView.xaml`** (lignes 92 à 104) — l'un des écrans
  les plus récents. `#1D4ED8`, `#15803D`, `#A16207`, `#0F766E`, `#B00020`, `#8A6D3B`, `#C2410C` :
  ce sont des couleurs de type Tailwind, pas des tokens du thème. **Elles ne figurent dans aucune
  des deux palettes : en thème sombre, elles ne changeront pas.**
- **97 `FontSize` ad hoc**, alors que la charte impose les styles nommés. Les deux plus fréquentes
  — `11.5` (32 fois) et `14` (28 fois) — correspondent exactement à `CaptionText` et
  `EmptyStateTitleText`, qui existent déjà. C'est de la re-déclaration pure.

La charte dit « un écart bloque la revue de design ». **Mais rien ne l'applique automatiquement.**
Il existe un garde CI sur le catalogue de modules, aucun sur les couleurs ni sur les tailles de
police. Un test de quinze lignes fermerait le sujet définitivement.

### 3.5 — La profondeur de navigation reste lourde

Chemin vers un écran de comptabilité après la refonte :

> **Barre latérale (domaine) › Écran › sous-onglet › section › formulaire**

`PmsView`, `FiscaliteView` et `AccountingView` portent **8 sous-onglets chacun** ; `CrmView` 6,
`LodgingView` 5. La refonte a supprimé un niveau dans la barre latérale (3 → 2), ce qui est juste,
mais la profondeur a été réintroduite à l'intérieur des écrans.

La recherche universelle (Ctrl+K) compense en partie — c'est la bonne réponse — mais elle indexe
l'arbre de navigation, **pas les sous-onglets**. Le jour où elle indexera aussi les sous-onglets et
les actions (« nouvelle facture », « clôturer la période »), la profondeur cessera d'être un
problème.

### 3.6 — La contradiction la plus frappante : logo bilingue, application monolingue

Le symbole **fusionne le `Q` latin et le `ق` arabe**. L'en-tête de document porte **رقمي سيستم**.
La police déclarée est `Manrope, **Noto Kufi Arabic**, Segoe UI` — une police arabe est **déjà
chargée**.

Et pourtant :

- **0 fichier `.resx`** — tous les libellés en dur dans le XAML ;
- **aucun `FlowDirection="RightToLeft"`** nulle part ;
- la charte écrit noir sur blanc : *« Tout libellé visible est en français accentué »*.

La marque promet le bilinguisme, le produit ne le livre pas. Pour un ERP algérien qui produit des
pièces fiscales, ce n'est pas cosmétique. Et le coût augmente chaque semaine : rétrofiter l'i18n
sur 37 XAML est aujourd'hui un chantier ; sur 60 écrans dans un an, c'est un projet.

### 3.7 — Deux dettes déjà identifiées, confirmées

- **Le thème sombre ne se repeint pas à chaud.** Les écrans déjà ouverts gardent l'ancienne
  apparence jusqu'au redémarrage. La cause technique est exacte (WPF scelle les `Freezable` d'un
  dictionnaire rattaché à l'`Application`) et le contournement honnête (le bandeau le dit plutôt
  que de laisser croire à un bug). Le correctif — passer ≈ 3 000 lignes en `{DynamicResource}` —
  est mécanique mais réel.
- **Zéro test d'interface.** Le projet Desktop n'est référencé par aucun projet de test. Ce
  système de design n'a **aucun filet automatique** : pas de test de contraste, pas de smoke test,
  pas de capture de référence. Il ne tient que par la discipline d'une seule personne.

### 3.8 — Le risque de sur-conception

2 490 lignes de spécification de design, un jury à trois lentilles, des explorations notées sur 60
— pour un produit que **personne n'a encore jamais utilisé une heure**. La refonte de la barre
latérale part d'une phrase : « elle ne me plaisait pas ». Retour légitime, mais c'est le retour du
propriétaire, pas celui d'un réceptionniste. Trois jours d'usage réel remonteront cinq points
qu'aucun de ces documents n'a anticipés.

---

## 4. Notation

| Dimension | Note | Commentaire |
|---|:---:|---|
| Identité de marque | **A** | Professionnelle, bilingue, déclinée complètement |
| Système de couleurs & contraste | **A** | Calculé, justifié, double palette, 82/82 tokens |
| Typographie | **B+** | Échelle cohérente, mais 97 tailles ad hoc |
| Iconographie | **A−** | 22 icônes de domaine dessinées main, jamais d'emoji ni de bitmap |
| Architecture d'information | **B** | Bon concept d'accueil ; 8 sous-onglets, 32 onglets par position |
| Ergonomie de saisie | **C+** | Pas de validation inline — le point faible principal |
| Accessibilité | **C−** | Intention A+, application ≈ 1 % |
| Adaptabilité écran / DPI | **C** | 1 443 largeurs fixes, aucun manifeste DPI |
| Internationalisation | **F** | Zéro, alors que la marque est bilingue |
| Gouvernance du design | **B+** | Charte remarquable, aucun garde automatique, dérive commencée |
| Processus de conception | **A** | Explorations, jury, décisions tracées, pertes assumées |

---

## 5. Synthèse en une phrase

Le design de Raqmi System est sa plus belle surprise — identité professionnelle, système de
couleurs rigoureux au point d'être exemplaire, processus de conception mature. Ce qui lui manque
n'est ni le goût ni la méthode, c'est **l'application** (accessibilité annoncée non livrée,
validation de saisie absente, arabe promis par le logo et absent du produit) et **un garde
automatique** — car une charte que rien ne contrôle se dégrade au rythme exact où le produit
grandit, et les sept couleurs en dur de `PmsView` en sont la première fissure.
