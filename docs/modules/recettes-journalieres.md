# Module Recettes journalieres

## Objectif

Le module Recettes journalieres rend exploitable la saisie quotidienne des revenus par unite hoteliere. Il s'appuie sur PostgreSQL, l authentification JWT, les permissions existantes et le journal d audit.

## Donnees gerees

| Champ | Description |
|---|---|
| Date d exploitation | Date de la recette |
| Unite hoteliere | Code de l unite concernee |
| Hebergement | Montant hebergement |
| Restauration | Montant food |
| Boissons | Montant beverage |
| Autres recettes | Montant other |
| Notes | Commentaire de saisie |
| Statut | Draft, Submitted, Validated ou Rejected |
| Trace workflow | Utilisateur et date de soumission/validation/rejet |

## Cycle de vie

~~~mermaid
stateDiagram-v2
    [*] --> Draft
    Draft --> Submitted: submit
    Submitted --> Validated: validate
    Submitted --> Rejected: reject
    Rejected --> Draft: update
~~~

## API

| Methode | Route | Permission | Usage |
|---|---|---|---|
| GET | /api/v1/revenue/daily | revenue.read | Lister les recettes |
| GET | /api/v1/revenue/daily/summary | revenue.read | Consolider les recettes |
| GET | /api/v1/revenue/daily/{id} | revenue.read | Lire une saisie |
| POST | /api/v1/revenue/daily | revenue.write | Creer une saisie en Draft |
| PUT | /api/v1/revenue/daily/{id} | revenue.write | Modifier une saisie Draft ou Rejected |
| POST | /api/v1/revenue/daily/{id}/submit | revenue.write | Soumettre au controle |
| POST | /api/v1/revenue/daily/{id}/validate | revenue.validate | Valider |
| POST | /api/v1/revenue/daily/{id}/reject | revenue.validate | Rejeter avec motif |

## Filtres

Les routes de liste et de synthese acceptent:

| Filtre | Exemple |
|---|---|
| from | 2026-01-01 |
| to | 2026-01-31 |
| hotelUnitCode | EL-MANAR |
| status | Draft, Submitted, Validated ou Rejected |

## Exemple de creation

~~~json
{
  "businessDate": "2026-01-31",
  "hotelUnitCode": "EL-MANAR",
  "accommodation": 1200000,
  "food": 340000,
  "beverage": 110000,
  "other": 80000,
  "notes": "Journee normale"
}
~~~

## Regles

- Une seule recette est autorisee par couple date + unite.
- Les montants negatifs sont refuses.
- Une recette Submitted ne peut plus etre modifiee par la saisie.
- Une recette Validated devient verrouillee.
- Une recette Rejected peut etre corrigee; la correction la remet en Draft.
- Chaque creation, correction et changement de statut cree une entree d audit.
