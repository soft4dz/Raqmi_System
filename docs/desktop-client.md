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

`Alt+Origine` revient à Mon Espace **et** à la section « Mon travail ». `Ctrl+K` bascule sur la section
Catalogue et donne le focus à sa recherche. `F5` actualise les files. Spécification complète :
[`design/accueil/refonte-accueil.md`](design/accueil/refonte-accueil.md).

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
