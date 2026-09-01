# Comptabilité SCF — cœur transactionnel

Le module 5.2 porte le livre comptable de Raqmi System. Le plan est paramétrable et peut être initialisé avec un socle SCF algérien via `POST /api/v1/accounting/scf/seed`.

## Invariants

- Une écriture comptabilisée contient au moins deux lignes et `somme(débit) = somme(crédit)`. La règle est contrôlée par le domaine et répétée par une contrainte PostgreSQL.
- Une écriture comptabilisée est immuable. Elle n'est ni modifiée ni supprimée ; la correction est une contre-passation comptabilisée liée à la source.
- Une période clôturée refuse toute nouvelle saisie, comptabilisation et contre-passation. Un exercice ne peut être clôturé qu'après toutes ses périodes.
- Une période contenant des brouillons ne peut pas être clôturée.
- Le numéro définitif est attribué à la comptabilisation, par journal et exercice (`JOURNAL-EXERCICE-000001`). La séquence porte un jeton de concurrence et une unicité DB.
- Les balances et le grand livre ne lisent que les écritures comptabilisées.
- Le lettrage porte uniquement sur des lignes comptabilisées, accepte des allocations partielles et qualifie le résultat `Partial` ou `Complete`.

## API et droits

Les routes existantes couvrent comptes, journaux, brouillons, comptabilisation, balance et contre-passation. Les routes ajoutées couvrent exercices/périodes, tiers, lettrage, grand livre et initialisation SCF.

| Permission | Geste |
|---|---|
| `accounting.read` | consulter référentiels, écritures, balances et grand livre |
| `accounting.write` | administrer les brouillons et les tiers |
| `accounting.post` | comptabiliser/valider |
| `accounting.reconcile` | lettrer |
| `accounting.close` | clôturer périodes et exercices |
| `accounting.reverse` | contrepasser |
| `accounting.admin` | initialiser et administrer le référentiel SCF |

## Persistance

La migration `AccountingScfCore` ajoute `fiscal_years`, `periods`, `parties`, `journal_sequences`, `reconciliations`, `reconciliation_allocations`, le numéro définitif des pièces et la contrainte d'équilibre des écritures comptabilisées.

## Limites connues

Le seed livré est un socle SCF minimal destiné à être enrichi par import/paramétrage. Il ne prétend pas remplacer la nomenclature détaillée validée par le commissaire aux comptes de chaque établissement. Les écritures d'ouverture automatiques et les états fiscaux ne font pas partie de ce lot.
