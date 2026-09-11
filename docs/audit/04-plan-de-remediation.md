# 04 — Plan de remédiation

**Base :** audit du 11 septembre 2026 sur `b412b8c`.
**Nature :** document opérationnel. Chaque chantier porte un identifiant, une priorité, un effort
indicatif et des **critères d'acceptation vérifiables**.

> **Avertissement sur les efforts.** L'audit n'a pas pu compiler le projet (pas de SDK .NET dans
> l'environnement d'analyse). Les efforts sont des **ordres de grandeur**, pas des engagements.
> Le premier geste de toute exécution de ce plan est de lancer `dotnet build` et `dotnet test`
> pour établir le point de départ réel.

## Ordre d'exécution

```
Lot A (gardes)  →  Lot B (déblocage)  →  Lot C (ergonomie)  →  Lot D (robustesse)
                                                              Lot P : décisions, en parallèle
```

**Le lot A passe en premier et n'est pas négociable** : ce sont les gardes qui empêchent les
corrections suivantes de se redégrader. Corriger les 7 couleurs en dur sans poser le test qui les
interdit revient à les recorriger dans trois semaines.

---

## Lot A — Gardes automatiques et fondations

*Effort total indicatif : ≈ 2 jours. Priorité : immédiate.*

### A-01 · Déclarer la conscience du DPI *(≈ 30 min)*

**Problème.** `src/RaqmiSystem.Desktop/` n'a aucun manifeste applicatif. WPF est donc *System DPI
aware* : sur un écran 4K, ou entre deux écrans de DPI différents, l'application est floue.

**Travail.** Ajouter un `app.manifest` déclarant `PerMonitorV2` (avec repli `PerMonitor`, puis
`true`) et le référencer via `<ApplicationManifest>` dans `RaqmiSystem.Desktop.csproj`.

**Acceptation.** Le manifeste existe, est référencé, le projet Desktop compile en Release.

---

### A-02 · Poser le garde CI de la charte UI *(≈ 3 h)*

**Problème.** La charte dit « un écart bloque la revue de design », mais rien ne l'applique. Un
garde CI existe pour le catalogue de modules, aucun pour le design.

**Travail.** Créer un projet de test (ou une classe de test) qui parcourt tous les `.xaml` de
`src/RaqmiSystem.Desktop/` (hors `Themes/`) et échoue si :

1. un attribut `Background`, `Foreground`, `Fill`, `Stroke` ou `BorderBrush` porte une valeur
   hexadécimale littérale ;
2. un attribut `FontSize` porte une valeur numérique littérale.

Le test doit **nommer le fichier et la ligne** de chaque écart, et porter une liste d'exceptions
explicites, vide au départ.

**Acceptation.** Le test échoue sur la base actuelle (7 + 97 écarts), puis passe une fois A-03
fait. Il est exécuté par le job `build-core` de `.github/workflows/dotnet.yml`.

---

### A-03 · Résorber les écarts existants de la charte *(≈ 4 h)*

**Problème.**
- **7 couleurs en dur**, toutes dans `src/RaqmiSystem.Desktop/Views/PmsView.xaml` lignes 92-104 :
  `#1D4ED8`, `#15803D`, `#A16207`, `#0F766E`, `#B00020`, `#8A6D3B`, `#C2410C`. Elles ne figurent
  dans **aucune** des deux palettes : **en thème sombre, elles ne changent pas.**
- **97 `FontSize` littéraux**, dont `11.5` (32 fois) et `14` (28 fois), qui correspondent
  exactement aux styles existants `CaptionText` et `EmptyStateTitleText`.

**Travail.**
1. Pour les couleurs de `PmsView` : ce sont des couleurs de statut d'un tape chart. Créer les
   tokens correspondants dans `RaqmiTheme.xaml`, leur donner une valeur dans **les deux** palettes
   (`ThemePalette.Sombre` incluse), en **vérifiant le contraste** selon la règle de la charte
   (4,5:1 pour du texte, 3:1 pour un trait), puis référencer ces tokens depuis la vue.
2. Pour les tailles : remplacer chaque `FontSize` littéral par le style nommé équivalent. Si une
   taille n'a pas d'équivalent, l'ajouter au thème plutôt que de la laisser dans la vue.

**Acceptation.** A-02 passe au vert. `ThemeManager.VerifierCouverture` ne lève aucune assertion.

---

### A-04 · Durcir les réglages de compilation *(≈ 2 h, à surveiller)*

**Problème.** `Directory.Build.props` porte `<LangVersion>preview</LangVersion>` et
`<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`. Sur un produit destiné à durer, une
fonctionnalité de langage preview retirée casse le build sans qu'une ligne ait été touchée ; et
avec ≈ 206 000 lignes générées, les avertissements du compilateur sont la seule relecture
automatique disponible.

**Travail.**
1. Remplacer `preview` par la version de langage **publiée** correspondant au SDK (`latest` ou le
   numéro exact). Si une construction du code dépend réellement d'une fonctionnalité preview, le
   dire dans le rapport final plutôt que de revenir en arrière silencieusement.
2. Passer `TreatWarningsAsErrors` à `true`. **Attendu : un volume d'avertissements important.**
   Les corriger réellement ; ne supprimer une règle (`NoWarn`) qu'avec un commentaire justifiant
   pourquoi, fichier par fichier — jamais globalement.

**Acceptation.** `dotnet build RaqmiSystem.sln -c Release` passe sans avertissement, avec
`TreatWarningsAsErrors=true` et une `LangVersion` publiée.

---

### A-05 · Limiter le débit sur l'authentification *(≈ 1 h)*

**Problème.** Il existe un verrouillage de compte (15 min, `User.cs:138`) mais **aucun rate
limiting par IP**. Un tiers connaissant les identifiants de connexion peut verrouiller en boucle
tous les comptes de la réception depuis l'extérieur : le verrouillage devient l'arme.

**Travail.** Activer `AddRateLimiter` dans `src/RaqmiSystem.Api/Program.cs` avec une politique par
adresse IP sur `/api/v1/auth/login` et `/api/v1/auth/refresh` (fenêtre glissante, valeurs à
justifier dans un commentaire). Répondre `429` avec un corps `ErrorResponse` cohérent avec le
reste de l'API.

**Acceptation.** Un test d'intégration prouve qu'au-delà du seuil, la même IP reçoit `429`, et
qu'une IP différente n'est pas affectée.

---

### A-06 · Restreindre `AllowedHosts` en production *(≈ 15 min)*

**Problème.** `src/RaqmiSystem.Api/appsettings.Production.json` porte `"AllowedHosts": "*"`.

**Travail.** Le rendre configurable par variable d'environnement `RAQMI_` (comme le reste de la
configuration), avec une valeur par défaut restrictive et la documentation correspondante dans
`.env.example` et `docs/deployment.md`.

**Acceptation.** `AllowedHosts` n'est plus `*` dans le fichier de production ; `.env.example`
documente la variable.

---

## Lot B — Ce qui débloque la suite

*Effort total indicatif : ≈ 3 jours. Priorité : haute.*

### B-01 · Générer la spécification OpenAPI *(≈ 1 jour)*

**Problème.** 469 routes, **aucune** spécification générée. Conséquence : impossible de bâtir un
second client (web, mobile), aucune intégration tierce, aucun contrat testable, aucune
documentation d'API. **C'est le chantier qui débloque le plus pour le moins d'effort.**

**Travail.**
1. Ajouter `Microsoft.AspNetCore.OpenApi` (version alignée sur le reste de
   `Directory.Packages.props`) et l'exposer.
2. Annoter les groupes de routes avec leurs codes de retour réels (`Produces`,
   `ProducesProblem`) — au moins pour les familles les plus utilisées.
3. Exposer l'interface de consultation **uniquement hors production**.

**Acceptation.** `GET /openapi/v1.json` (ou équivalent) renvoie un document valide couvrant les
469 routes. Un test d'intégration vérifie que le document se génère sans erreur.

---

### B-02 · Smoke test du client WPF *(≈ 1 jour)*

**Problème.** 71 214 lignes d'interface, **aucun test**. Le projet Desktop n'est référencé par
aucun projet de test.

**Travail.** Créer `tests/RaqmiSystem.Desktop.Tests/` (ciblant `net10.0-windows`, exécuté par le
job `build-desktop` sur `windows-latest`) qui : instancie `MainWindow`, parcourt les **32
`TabItem`**, force le chargement de chaque vue, et échoue si l'une lève une exception.

Ce test ne vérifie pas le comportement métier ; il vérifie qu'**aucun écran ne plante à
l'ouverture** — ce que rien ne garantit aujourd'hui.

**Acceptation.** Le test existe, tourne en CI sur `windows-latest`, et passe pour les 32 onglets.

---

### B-03 · Traiter le code parqué *(≈ 1 h)*

**Problème.** `staging/wave-e2/` et `staging/wave-hr/` contiennent ≈ 500 lignes hors solution :
non compilées, non testées, non référencées.

**Travail.** Pour chaque dossier : soit l'intégrer réellement (projet, tests, migration si
nécessaire), soit le supprimer. Si la décision revient au propriétaire, le dire dans le rapport
plutôt que de trancher seul.

**Acceptation.** `staging/` n'existe plus, ou son contenu est compilé et testé.

---

## Lot C — Ergonomie

*Effort total indicatif : ≈ 5 jours. Priorité : haute (C-01 en particulier).*

### C-01 · Validation de saisie inline *(≈ 3 jours)*

**Problème.** Aucun mécanisme de validation WPF n'est utilisé : `IDataErrorInfo` 0,
`INotifyDataErrorInfo` 0, `Validation.ErrorTemplate` 0. Toute erreur remonte dans un **message
d'état global en haut de l'écran**. Sur un formulaire à vingt champs (RH, écriture comptable),
l'utilisateur apprend qu'« un montant est invalide » **sans savoir lequel**.

**Travail.**
1. Poser dans `RaqmiTheme.xaml` un `Validation.ErrorTemplate` conforme à la charte : bordure
   `DangerBrush`, message sous le champ en `CaptionText`, contraste vérifié dans les deux thèmes.
2. Fournir un mécanisme réutilisable pour qu'une vue marque un champ en erreur et y place le
   focus, sans réécrire son code-behind.
3. **L'appliquer d'abord à trois écrans** — ceux qui ont les formulaires les plus longs
   (`HumanResourcesView`, `AccountingView`, `InvoicesView`) — avant de généraliser.
4. Consigner la règle dans `docs/charte-ui-desktop.md` (§ 3) pour que les écrans suivants la
   suivent.

**Acceptation.** Sur les trois écrans retenus, une saisie invalide marque **le champ fautif**, y
place le focus et affiche le message sous le champ, dans les deux thèmes. La charte est à jour.

---

### C-02 · Accessibilité ciblée *(≈ 1,5 jour)*

**Problème.** ≈ 47 annotations d'accessibilité pour 991 `DataGrid` et 71 214 lignes : ≈ 1 % du
produit, alors que la charte et les documents de design en parlent abondamment.

**Travail.** Ne **pas** viser l'exhaustivité. Couvrir complètement **les cinq écrans les plus
utilisés** (accueil, PMS front office, CA journalier, facturation, trésorerie) :
`AutomationProperties.Name` sur chaque bouton d'action et chaque champ, `HeadingLevel` sur les
titres de section, `LiveSetting` sur les messages d'état, `LabeledBy` reliant libellé et champ.

**Acceptation.** Les cinq écrans sont annotés ; la méthode retenue est consignée dans la charte
pour que les écrans suivants l'appliquent.

---

### C-03 · Étendre la recherche universelle aux sous-onglets *(≈ 0,5 jour)*

**Problème.** `PmsView`, `FiscaliteView` et `AccountingView` portent **8 sous-onglets** chacun. La
recherche universelle indexe l'arbre de navigation, **pas** les sous-onglets : « registre TVA
achats » ne se trouve pas, il faut savoir que c'est le 2ᵉ onglet de Fiscalité.

**Travail.** Étendre l'index de la recherche aux sous-onglets déclarés, en réutilisant le
mécanisme existant (et sans casser la règle « le raccourci n'ouvre jamais ce que le clic n'ouvre
pas » : un sous-onglet inaccessible par permission ne doit pas apparaître).

**Acceptation.** Une recherche sur un libellé de sous-onglet l'ouvre directement ; un sous-onglet
non autorisé n'apparaît pas dans les résultats.

---

## Lot D — Robustesse

*Effort total indicatif : ≈ 5 jours. Priorité : moyenne, mais avant toute mise en service.*

### D-01 · Borner les listes non paginées *(≈ 3 jours)*

**Problème.** **253** `ToArrayAsync` / `ToListAsync` dans `Infrastructure` contre **13** `Take()`.
L'audit est correctement paginé ; pas les réservations, les mouvements de stock, les écritures
comptables ni les folios. Invisible six mois, inévitable à trois ans.

**Travail.**
1. Établir l'inventaire des listes dont le volume **croît avec le temps** (par opposition aux
   référentiels bornés : unités, comptes, types de chambre — qui n'ont pas besoin de pagination).
2. Les paginer sur le modèle **déjà en place** dans `AuditEndpoints.cs` (page, pageSize, bornes
   validées, réponse enveloppée) — ne pas inventer une seconde convention.
3. Répercuter sur `RaqmiApiClient.*` et les vues concernées.

**Acceptation.** Toute route listant une entité à volume croissant accepte la pagination avec la
même convention que l'audit. Un test par famille le prouve. La liste des routes volontairement
laissées non paginées est justifiée dans le rapport.

---

### D-02 · Cinq tests E2E du parcours central *(≈ 2 jours)*

**Problème.** 1 092 tests, **aucun E2E**. Les tests prouvent que le code fait ce que le code a dit
qu'il ferait, pas qu'un parcours complet fonctionne.

**Travail.** Écrire **cinq** tests de bout en bout — pas cinquante — sur le parcours qui fait
vivre le produit, contre du vrai PostgreSQL (collection `Postgres` existante) :

1. réservation → check-in → charge folio → check-out → facture → PDF archivé ;
2. saisie de recette journalière → soumission → validation → clôture de la journée ;
3. bon de commande → approbation → réception → entrée en stock → valorisation PMP ;
4. écriture comptable → comptabilisation → balance → extourne ;
5. période de paie → génération des bulletins → validation → clôture.

**Acceptation.** Les cinq tests tournent dans le job `postgres-integration` et passent.

---

## Lot P — Décisions produit *(à trancher par le propriétaire, pas à coder)*

Ces quatre points **ne sont pas des chantiers techniques**. Une session d'exécution ne doit pas les
trancher seule : elle doit les documenter et demander l'arbitrage.

### P-01 · Conformité hôtelière algérienne
**Zéro module livré sur quatre** dans le groupe Conformité. La **fiche de police** et la **taxe de
séjour** sont des obligations légales immédiates : sans elles, un hôtel ne peut pas utiliser ce
logiciel comme système unique. C'est, fonctionnellement, le prochain chantier avant tout nouveau
module.

### P-02 · Arabe et internationalisation
Le logo fusionne `Q` et `ق`, l'en-tête de document porte رقمي سيستم, la police `Noto Kufi Arabic`
est déjà chargée — et le produit contient **0 `.resx`** et aucun `FlowDirection` RTL.
**Décision à prendre :** soit l'arabe sort du périmètre et la promesse de marque est ajustée, soit
l'infrastructure i18n est posée **avant le prochain écran**. Continuer à promettre le bilinguisme
tout en codant du français en dur est la seule option qui empire chaque semaine.

### P-03 · Couche cliente et gel du WPF
71 214 lignes de WPF, 0 ViewModel, 0 test, Windows uniquement.
**Recommandation de l'audit** (détaillée au document 01 § 6.4) : ne pas réécrire, **geler la
croissance du WPF**, et sortir les nouveaux écrans en web (React/TypeScript) après B-01.

### P-04 · Les 18 modules planifiés
38 % du catalogue ouvre sur du vide. C'est le seul endroit où le produit contredit son vendeur en
démonstration. **Options :** les masquer, les dater, ou les afficher en « à venir » assumé.

---

## Tableau récapitulatif

| ID | Chantier | Lot | Effort | Priorité |
|---|---|:---:|---:|:---:|
| A-01 | Manifeste DPI `PerMonitorV2` | A | 30 min | 🔴 |
| A-02 | Garde CI de la charte UI | A | 3 h | 🔴 |
| A-03 | Résorber 7 couleurs + 97 tailles | A | 4 h | 🔴 |
| A-04 | `LangVersion` publiée + warnings en erreurs | A | 2 h + | 🔴 |
| A-05 | Rate limiting sur l'authentification | A | 1 h | 🔴 |
| A-06 | `AllowedHosts` en production | A | 15 min | 🔴 |
| B-01 | Spécification OpenAPI | B | 1 j | 🟠 |
| B-02 | Smoke test WPF des 32 onglets | B | 1 j | 🟠 |
| B-03 | Traiter `staging/` | B | 1 h | 🟠 |
| C-01 | Validation de saisie inline | C | 3 j | 🟠 |
| C-02 | Accessibilité sur 5 écrans | C | 1,5 j | 🟡 |
| C-03 | Recherche sur les sous-onglets | C | 0,5 j | 🟡 |
| D-01 | Pagination des listes | D | 3 j | 🟡 |
| D-02 | 5 tests E2E | D | 2 j | 🟡 |
| P-01 | Conformité hôtelière | P | — | 🔴 décision |
| P-02 | Arabe / i18n | P | — | 🔴 décision |
| P-03 | Couche cliente web | P | — | 🟠 décision |
| P-04 | Modules planifiés | P | — | 🟡 décision |

**Effort technique cumulé (lots A à D) : ≈ 15 jours-personne**, hors A-04 dont le volume dépend du
nombre réel d'avertissements — inconnu tant que le projet n'a pas été compilé.
