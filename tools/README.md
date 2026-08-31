# Outils de documentation

Ces outils produisent le guide utilisateur de Raqmi System. Ils ne font pas partie du produit
livre : ils ne sont pas dans `RaqmiSystem.sln`, aucun projet de `src/` ne les reference, et
rien de ce qu'ils contiennent n'est deploye chez un client.

| Outil | Role |
|---|---|
| `generate-guide.ps1` | Chaine complete : migrations, seed, API de demonstration, jeu de donnees, captures |
| `demo-seed/` | Jeu de demonstration d'un groupe hotelier algerien fictif, ecrit uniquement via l'API HTTP |
| `RaqmiSystem.DocShots/` | Campagne de captures : ouvre la vraie fenetre WPF et rend chaque module en PNG |

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
