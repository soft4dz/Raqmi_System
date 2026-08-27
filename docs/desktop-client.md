# Client Desktop WPF

## Objectif

Le client Desktop WPF consomme l API Raqmi System pour demarrer les premiers usages metier cote exploitation.

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
