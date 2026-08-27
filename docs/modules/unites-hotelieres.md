# Module Unites hotelieres

## Objectif

Le module Unites hotelieres fournit le referentiel operationnel minimal pour demarrer Raqmi System avec plusieurs hotels, residences ou activites annexes.

## Donnees gerees

| Champ | Description |
|---|---|
| Code | Identifiant court et stable de l unite, normalise en majuscules |
| Nom | Nom lisible par les utilisateurs |
| Type | Hotel, Residence, BeachClub, Marina ou Other |
| Ordre d affichage | Classement dans les ecrans et rapports |
| Actif | Permet de masquer une unite sans supprimer l historique |

## API

| Methode | Route | Permission | Usage |
|---|---|---|---|
| GET | /api/v1/organization/hotel-units | units.read | Lister les unites actives |
| GET | /api/v1/organization/hotel-units?includeInactive=true | units.read | Lister toutes les unites |
| GET | /api/v1/organization/hotel-units/{code} | units.read | Lire une unite |
| POST | /api/v1/organization/hotel-units | units.write | Creer une unite |
| PUT | /api/v1/organization/hotel-units/{code} | units.write | Modifier nom, type et ordre |
| POST | /api/v1/organization/hotel-units/{code}/activate | units.write | Reactiver une unite |
| POST | /api/v1/organization/hotel-units/{code}/deactivate | units.write | Desactiver une unite |

## Exemple de creation

~~~json
{
  "code": "EL-MANAR",
  "name": "Hotel El Manar",
  "unitType": "Hotel",
  "displayOrder": 10
}
~~~

## Regles

- Le code est unique.
- Le code est stocke en majuscules.
- Une unite inactive ne peut pas recevoir une nouvelle recette journaliere.
- La suppression physique n est pas exposee pour garder l historique comptable et d audit.
