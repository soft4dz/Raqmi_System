# Outils

Ces outils ne font pas partie du produit livre : ils ne sont pas dans `RaqmiSystem.sln`, aucun
projet de `src/` ne les reference, et rien de ce qu'ils contiennent n'est deploye chez un client.

| Outil | Role |
|---|---|
| `check-module-readiness.ps1` | Garde de readiness : lit le code (sans le compiler) et verifie que chaque module Disponible est cable, que les 22 domaines sont coherents et que chaque ecran prouve le niveau qu'il declare |
| `readiness/screens.json` | Preuves lisibles par machine des 30 ecrans (un par onglet `x:Name`) : ordres, permission, niveau declare, fichiers de preuve |
| `generate-guide.ps1` | Chaine complete : migrations, seed, API de demonstration, jeu de donnees, captures |
| `demo-seed/` | Jeu de demonstration d'un groupe hotelier algerien fictif, ecrit uniquement via l'API HTTP |
| `RaqmiSystem.DocShots/` | Campagne de captures : ouvre la vraie fenetre WPF et rend chaque module en PNG |

## Garde de readiness

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/check-module-readiness.ps1
```

Le script fonctionne sous Windows PowerShell 5.1 et PowerShell 7 (`pwsh`, utilise par la CI). Il ne
modifie rien et ne compile rien : il lit `ModuleCatalog.cs`, `MainWindow.xaml`, tous les partiels
`MainWindow*.cs`, `PermissionCatalog.cs`, `FunctionalArchitectureCatalog.cs`, les endpoints API cites
par `screens.json`, et verifie l'existence de chaque fichier de preuve. Le detail des dix controles,
des neuf criteres et des quatre niveaux est dans
[`docs/stabilization/module-readiness.md`](../docs/stabilization/module-readiness.md).

| Parametre | Effet |
|---|---|
| `-RepositoryRoot` | Racine du depot (par defaut : le parent de `tools/`) |
| `-ScreensPath` | Autre fichier de preuves (utile pour tester une variante sans toucher au depot) |
| `-AsOf AAAA-MM-JJ` | Date de reference pour la periode de grace documentation (par defaut : aujourd'hui) |
| `-MarkdownSummaryPath` | Ecrit le tableau en Markdown dans ce fichier ; sinon dans `GITHUB_STEP_SUMMARY` si la variable existe |

Sortie : `Module readiness gate: 31/31 ...`, `Catalogue fonctionnel: 22/22 ...`, un tableau par ecran
(onglet, ecran, ordres, domaine cible, permission, niveau declare, niveau prouve, preuves manquantes),
un resume par niveau et l'etat de la grace. Code de sortie `0` si tout est coherent, `1` sinon, avec
la liste complete des echecs prefixes `ECHEC:` (les avertissements, prefixes `AVERTISSEMENT:`, ne font
pas echouer).

**Ajouter ou faire evoluer un ecran.** Quand un module passe `Disponible` (hors gel) ou change de
permission/onglet, ajouter ou corriger sa fiche dans `readiness/screens.json` : cle = `x:Name` de
l'onglet, `orders` et `permission` identiques au catalogue, `declared` = niveau revendique, et une
preuve par critere (`domain`, `application`, `api`, `postgresql`, `desktop`, `tests`, `documentation`,
`smoke`). Une preuve est un chemin (ou une liste) relatif a la racine, `{ "status": "n/a",
"reason": "..." }` quand le critere ne s'applique pas, ou `null` quand elle manque. Un chemin
inexistant fait echouer le garde ; un `null` abaisse le niveau prouve, et le garde echoue si
`declared` le depasse. Le niveau `ProductionReady` exige en plus `productionReady.postgresqlCi`,
`productionReady.e2e` et un `smoke` joue : aucun ecran ne l'atteint aujourd'hui.

**Grace documentation.** `documentationGrace` liste, avec une date limite, les ecrans historiques
sans fiche `docs/modules/*.md`. Passe cette date ils retombent en Technical Preview et le garde
echoue ; un ecran qui recoit sa fiche doit etre retire de la liste. `-AsOf 2027-01-01` permet de
verifier ce comportement des maintenant.

# Outils de documentation

Ces outils produisent le guide utilisateur de Raqmi System.

## Pourquoi une base dediee

Le guide doit montrer des ecrans pleins. Une base vide donne 28 captures de tableaux vides,
qui n'apprennent rien et donnent une fausse idee du produit. Le jeu de demonstration cree
donc des clients, des factures, des ecritures comptables et des bulletins de paie : il n'a
rien a faire dans une base de travail, et encore moins dans une base de production.

La base par defaut est `raqmi_demo`. Elle doit exister avant le premier lancement, et le role
applicatif `raqmi` n'a pas le droit `CREATEDB` : la creation se fait une seule fois avec le
superutilisateur.

```powershell
psql -U postgres -h localhost -c "CREATE DATABASE raqmi_demo OWNER raqmi"
```

## Lancement

```powershell
dotnet build RaqmiSystem.sln -c Release
dotnet build tools/RaqmiSystem.DocShots/RaqmiSystem.DocShots.csproj -c Release
.\tools\generate-guide.ps1
```

La chaine ecrit les PNG et un `manifest.json` dans `docs/guide/captures/`. L'API de
demonstration ecoute sur le port 5190 : l'instance de travail sur 5180 n'est ni arretee ni
modifiee, et la base `raqmi_system` n'est jamais ouverte en ecriture.

Options utiles :

| Parametre | Effet |
|---|---|
| `-SkipSeed` | Recapture sans regenerer les donnees (iteration sur le rendu) |
| `-SkipCapture` | Regenere seulement les donnees |
| `-Database` | Vise une autre base de demonstration |
| `-OutputDirectory` | Change le dossier de sortie des PNG |

## Choix techniques

**Le jeu de demonstration passe par l'API, jamais par SQL.** Les regles metier s'appliquent
donc reellement : une facture emise n'est plus modifiable, un bon de commande ne prend son
numero qu'a l'approbation, un stock se valorise au PMP, le journal d'audit se remplit tout
seul avec de vrais acteurs. Un `INSERT` direct aurait pu produire des etats que l'application
refuse, et le guide aurait montre un produit qui n'existe pas.

**Les captures viennent de l'arbre visuel WPF, pas du bureau.** `RenderTargetBitmap` rend la
fenetre telle que WPF la dessine : le resultat est identique quelle que soit la resolution du
poste, ne capte ni fenetre parasite ni curseur, et peut etre rendu a 1,5x pour rester net a
l'impression. Une capture d'ecran classique aurait dependu de l'ecran de la personne qui
lance la chaine.

**La liste des ecrans a capturer est deduite de `ModuleCatalog`**, pas ecrite a la main : un
module qui passe de `Planifie` a `Disponible` entre dans le guide a la campagne suivante sans
qu'on ait a y penser.

**Les identifiants memorises du poste sont sauvegardes puis restaures** autour de la campagne.
Elle se connecte avec un compte de demonstration : il n'y a aucune raison qu'elle remplace le
compte enregistre par l'utilisateur dans `%APPDATA%\RaqmiSystem\desktop-settings.json`.
