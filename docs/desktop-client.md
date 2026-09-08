# Client Desktop WPF

## Objectif

Le client Desktop WPF consomme l API Raqmi System pour demarrer les premiers usages metier cote exploitation.

## Mon Espace — l'onglet 0

L'onglet 0 s'appelle « Mon Espace » (domaine 01 de la cartographie cible) et porte deux sections,
dans le contenu de l'onglet : aucune balise d'onglet n'est ajoutée à `MainTabs`, dont l'ordre des 31
entrées reste figé et vérifié par `tools/check-module-readiness.ps1`.

| Section | Contenu | Vue |
|---|---|---|
| **Mon travail** (par défaut) | les files de travail que le serveur compte, en trois bandes d'urgence | `Views/WorkQueuesView` |
| **Catalogue des modules** | les 50 cartes, leurs filtres, leur recherche et leurs cadenas | `Views/ModuleCatalogView` |

### Ce que « Mon travail » affiche

Un bandeau (salutation, date, établissement, unité du poste, date métier, synthèse, `Actualiser`),
puis trois bandes — **En retard**, **Aujourd'hui**, **À surveiller** — de cartes de files de travail,
puis les derniers écrans ouverts sur ce poste et la carte « Où en est le produit ? ».

Les règles qui tiennent l'écran :

- **Composition par permissions seules.** `HomeComposer.Compose(clés du jeton, unité du poste connue)`
  est une fonction pure de `RaqmiSystem.Application/Navigation`, testée sans WPF. Une file n'apparaît
  que si le profil détient sa clé de **lecture** ; sa clé d'**action** donne le verbe du bouton, et son
  absence donne le mode *Suivi* (bouton « Voir », pastille « Suivi »). Les clés cibles
  (`domaine.ressource.action`) et historiques sont acceptées à égalité, via
  `PermissionRegistry.AcceptedClaims` — comme l'API.
- **Aucun chiffre calculé ici.** Les compteurs et les montants sont des champs renvoyés par le serveur ;
  un compte de lignes n'est jamais additionné, et seuls les agrégats que le serveur expose
  (`PendingValidationAmount`, `OutstandingBalance`, `Total.Over90`, `GrandTotal`…) portent un montant.
- **Aucun seuil client.** Une carte est « En retard » parce que le registre le dit ou parce que le
  serveur a répondu `IsLate` / `IsOverdue`.
- **Un appel par source, une `RunAsync` par appel** (charte § 3.1), de la plus légère à la plus lourde.
  Une source en échec bascule *ses* cartes en « Indisponible » sans arrêter les suivantes ; un encart
  agrégé nomme les écrans concernés et `F5` relance tout.
- **Rien qui n'existe pas côté serveur.** Tâches transverses, notifications, messagerie, agenda,
  favoris, documents, demandes et délégations restent des nœuds « Planifié » de l'arbre : visibles avec
  leur badge dans le catalogue, jamais présentés comme une fonction.

### Quand il se charge

À la connexion, sur `F5` (`RefreshHomeButton`), et au retour sur l'onglet 0 **si la dernière lecture
date de plus de cinq minutes** — la cadence du battement de poste. Aucun `Timer` : le client est
monothread et ne fait pas d'appel que personne n'a demandé.

### Réglages de poste

`DesktopSettings` (`%APPDATA%\RaqmiSystem\desktop-settings.json`) gagne deux entrées **par poste**,
comme l'apparence et la densité — jamais par compte :

- `StationUnitCode` : l'unité à laquelle ce poste est rattaché. Le réglage s'écrit dans **un seul
  endroit**, `Paramétrage global › Poste de travail` (liste des unités si `units.read`, code saisi
  sinon) ; Mon Espace l'affiche et y renvoie, il ne l'écrit pas. Sans unité, aucune file unitaire
  n'est composée et un encart le dit. C'est un confort de poste, **jamais un périmètre de sécurité** :
  le serveur reste seul juge de ce que le jeton donne le droit de lire. Le code part tel quel dans les
  appels, et toutes les routes ne le traitent pas de la même façon : celles qui l'**exigent** (date
  métier, front office, housekeeping) refusent un code inconnu et la carte affiche « Indisponible »
  avec le message du serveur ; celles qui ne font que **filtrer** dessus (recettes, encaissements,
  événements) répondraient zéro. D'où la liste dès que `units.read` est détenue, et l'avertissement de
  l'écran de paramétrage quand elle ne l'est pas.
- `RecentTabs` : les six derniers onglets ouverts sur ce poste. Sur un comptoir partagé ce sont les
  écrans du poste et non ceux de la personne — le libellé « (ce poste) » le dit. Ce ne sont pas des
  favoris par compte : il n'en existe pas côté serveur.

### Raccourcis

`Alt+Origine` revient à Mon Espace **et** à la section « Mon travail ». Sur Mon Espace, `Ctrl+K` bascule
sur la section Catalogue et donne le focus à sa recherche ; sur un écran de module, il donne le focus à
la recherche de la barre latérale. `F5` actualise les files. Spécification complète :
[`design/accueil/refonte-accueil.md`](design/accueil/refonte-accueil.md).

## La barre latérale

Un panneau de **260 px**, collé au bord gauche sous l'en-tête, pleine hauteur, séparé du contenu par un
filet ; il **ne disparaît jamais**, Mon Espace compris (décision du 08/09/2026, spécification
[`design/navigation/refonte-barre-laterale.md`](design/navigation/refonte-barre-laterale.md), maquette
[`design/navigation/maquette-barre-laterale.html`](design/navigation/maquette-barre-laterale.html)).
Elle présente l'arbre **déjà élagué** par `NavigationTreeBuilder` (`RaqmiSystem.Application`) : seuls
les écrans que le jeton autorise y figurent, aucune règle de permission ni de maturité n'est évaluée
dans le client.

### Trois strates

| Strate | Contenu | Défile ? |
|---|---|---|
| **Mon Espace** | rangée du domaine 01 (retour à l'onglet 0, état actif quand on y est), puis ses écrans ouvrables (« Workflows & validations » si `approvals.read`) ; le domaine 01 n'apparaît pas une seconde fois dans la liste | non |
| **Recherche** | « Rechercher un écran… » : nom, description, famille, numéro d'ordre et libellé court, sans accent ni casse ; `Échap` efface, `Entrée` ouvre le premier résultat ; sans résultat, un bouton « Ouvrir le catalogue » | non |
| **Domaines 02 → 21** puis, en pied, **22 Administration Système** épinglée | un en-tête par domaine (icône, libellé court, chevron) et, déplié, ses écrans **à plat** — deux niveaux, Domaine › Écran ; le libellé de module ne subsiste que comme séparateur d'un module qui regroupe au moins deux écrans, le sous-module vit dans le fil d'Ariane. Le pied n'existe que pour les profils qui détiennent un écran du domaine 22 | oui (la liste seule) |

Le vocabulaire est celui des cartes de l'accueil et du fil d'Ariane : `ShortLabel` est une contraction
du nom officiel portée par le catalogue (« Admin & Socle ERP », « Revenue Management », « Groupes &
MICE »), jamais un synonyme ; le nom complet et le numéro de domaine restent dans l'info-bulle, le nom
d'automatisation et le fil d'Ariane. L'écran affiché porte un filet, un fond et une graisse ; son domaine
porte son icône en accent (et un point s'il est replié).

### Clavier

`Tab` fait un arrêt par strate (la liste des domaines n'en compte qu'un) ; dans la liste, `↑` / `↓`
passent d'une rangée à l'autre, `→` / `←` déplient et replient un domaine, `Origine` / `Fin` vont aux
extrémités, `Échap` ramène au champ de recherche ; `Espace` ou `Entrée` sur un domaine le bascule sans
naviguer, `Entrée` sur un écran l'ouvre (`NavigateToModule`, seul chemin de navigation, gardé par
`CanOpenModule`) ; `Ctrl+Page haut / bas` passent au module précédent / suivant et la barre suit. Le
focus clavier est un anneau de 2 px, distinct du survol.

### Mémorisation par poste

Les domaines dépliés sont enregistrés dans `DesktopSettings.SidebarExpandedDomains`
(`%APPDATA%\RaqmiSystem\desktop-settings.json`), **par poste** comme l'apparence et la densité : sur un
comptoir partagé, l'état laissé par l'équipe du matin s'applique au soir. L'écriture est différée de
500 ms et n'a jamais lieu pendant une recherche (le dépliage forcé par le filtre n'est pas un choix ;
la fin de la recherche écrit une fois l'état restauré) ; au démarrage, un identifiant inconnu est
ignoré, un domaine sans écran ouvrable pour le profil reste masqué (son état déplié est inerte), puis
le domaine de l'écran courant est ouvert en plus, sans replier les autres. La densité Compact
(Paramétrage global › Poste de travail) ramène les rangées à 32 px et le séparateur de module à 22 px.

### Info-bulle de permission

En régime permanent, un écran non autorisé n'est **pas dans la barre**. Une rangée désactivée n'existe
que si la permission est retirée en cours de session, après le chargement de l'arbre : elle n'est pas
focalisable (contrôle désactivé, les flèches la sautent), mais son motif reste lisible à la souris
(`ToolTipService.ShowOnDisabled`), en mode balayage du lecteur d'écran (`HelpText`) et sur la carte du
catalogue, avec la même phrase partout — résultats de recherche du catalogue et cartes de files
comprises : « Accès non autorisé pour votre profil — permission requise : Lire la comptabilite »,
libellé lu dans `PermissionCatalog` (sans accent, tel que le catalogue l'écrit), jamais la clé
technique.

## Ecrans disponibles

| Ecran | API consommee | Usage |
|---|---|---|
| Connexion | POST /api/v1/auth/login | Recuperer un JWT et ouvrir une session desktop |
| Unites hotelieres | GET /api/v1/organization/hotel-units | Afficher le referentiel des unites actives ou inactives |
| Saisie recette journaliere | POST /api/v1/revenue/daily | Creer une recette en brouillon |
| Saisie recette journaliere | POST /api/v1/revenue/daily/{id}/submit | Creer puis soumettre immediatement au controle |
| Recettes de la journee | GET /api/v1/revenue/daily?from=...&to=... | Afficher les saisies de la date selectionnee |

## Demarrage local

1. Demarrer PostgreSQL.
2. Appliquer les scripts SQL dans database/postgres.
3. Lancer l API sur son port par defaut.

~~~bash
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~

4. Lancer le projet Desktop.

~~~bash
dotnet run --project src/RaqmiSystem.Desktop/RaqmiSystem.Desktop.csproj
~~~

## Notes fonctionnelles

- L URL API par defaut est http://localhost:5180.
- Les montants acceptent la culture locale ou le format invariant.
- Une recette creee via le bouton "Creer brouillon" reste modifiable cote API.
- Une recette creee via "Creer + soumettre" passe directement en Submitted pour controle.
