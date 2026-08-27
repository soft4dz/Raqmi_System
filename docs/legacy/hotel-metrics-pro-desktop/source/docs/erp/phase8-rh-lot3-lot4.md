# Phase 8 — RH Lot 3 : Temps & Réconciliation

## Objectif

Réconcilier planning, pointages et paie avec alertes H+15.

## Migration 070

- `rh_reconciliations_jour` — agrégat journalier par employé
- `rh_temps_alertes` — alertes ouvertes/ traitées

## Alertes

| Type | Déclencheur |
|------|-------------|
| `retard_h15` | Entrée ≥ 15 min après heure planning |
| `absence_non_pointee` | Planning sans pointage |
| `pointage_sans_planning` | Pointage orphelin |
| `ecart_heures` | Écart ≥ 0,5 h prévu/pointé |
| `depassement_horaire` | Pointé > prévu + 2 h |

## Workflow

1. Fin de semaine / fin de mois → **Lancer réconciliation**
2. Traiter alertes ouvertes
3. Consulter **Synthèse paie** (prêt paie si 0 jour alerte)
4. Générer pré-paie (Lot 1)

## Interface

Temps & Présence → **Réconciliation**

# Phase 8 — RH Lot 4 : GPEC

## Objectif

Gestion prévisionnelle des emplois et compétences avec campagnes d'évaluation.

## Migration 071

- `rh_employe_competences` — niveaux actuels par salarié
- `rh_campagnes_evaluation` — campagnes annuelles/semestrielles
- `rh_campagne_evaluations` — grille employé × compétence

## Workflow campagne

1. Créer campagne (brouillon)
2. **Lancer** → génère lignes depuis matrice poste/compétences
3. Saisir niveaux observés (1–5)
4. **Valider** → met à jour `rh_employe_competences`
5. **Clôturer** campagne

## Matrice GPEC

Compare niveau actuel vs niveau requis du poste — couverture en %.

## Interface

Talents → **GPEC & Évaluations**
