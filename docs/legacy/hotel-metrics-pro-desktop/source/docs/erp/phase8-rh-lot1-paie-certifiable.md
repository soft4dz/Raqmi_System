# Phase 8 — RH Lot 1 : Paie certifiable DZ

## Objectif

Compléter le calcul de pré-paie locale avec heures supplémentaires, retenues d'absence sans solde, et exports déclaratifs enrichis (DADS-U).

## Migration 068

Colonnes ajoutées à `rh_bulletins` :

| Colonne | Description |
|---------|-------------|
| `brut_base` | Salaire brut contractuel |
| `heures_sup` | Heures au-delà de 173,33 h |
| `montant_hs` | Majoration HS (+50 %) |
| `retenue_absence` | Retenue jours sans solde |
| `jours_absence_non_remuneree` | Jours `Sans_solde` approuvés |

## Moteur `calculateBrutPaieMensuel`

- Référence mensuelle : **173,33 h** (40 h/semaine, Loi 90-11)
- Majoration HS : **×1,5** sur le taux horaire
- Retenue absence : `(brut_base / 30) × jours sans solde`
- Brut imposable = base + HS + primes − retenue

## Exports

- **CNAS / DAS** : base imposable = `brut` bulletin (primes déjà incluses)
- **DADS-U** : export nominatif mensuel par salarié (`DADS-U_AAAA.csv`)

## Ordre opérationnel paie

1. Valider pointages et absences du mois
2. Saisir primes variables
3. Générer pré-paie
4. Valider bulletins → clôturer période
5. Exporter CNAS (mensuel) / DADS-U + DAS (annuel)

## Lots suivants

- **Lot 2** : Recrutement ATS (pipeline candidats)
- **Lot 3** : Temps & présence (réconciliation planning/pointage)
- **Lot 4** : GPEC (compétences, évaluations)
