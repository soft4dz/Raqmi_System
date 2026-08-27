# Phase 8 — RH Lot 2 : ATS Recrutement

## Objectif

Pipeline candidats complet avec offres d'emploi, étapes Kanban, entretiens et historique.

## Migration 069

| Table | Rôle |
|-------|------|
| `rh_offres_emploi` | Offres liées aux postes (brouillon → publiée → pourvue) |
| `rh_recrutements` + | Colonnes `offre_id`, `etape`, `source`, `score` |
| `rh_recrutement_entretiens` | Entretiens par candidature |
| `rh_recrutement_historique` | Traçabilité des changements d'étape |

## Pipeline (étapes)

1. **Candidature** — réception dossier
2. **Présélection** — tri initial
3. **Entretien RH** — premier contact
4. **Entretien métier** — validation compétences
5. **Proposition** — offre salariale
6. **Embauche** — création employé + compte (existant)
7. **Refusé** — clôture négative

Transitions : étape suivante ou saut +1 ; refus possible à tout moment.

## Interface

Onglet **Talents → Recrutements** :

- **Pipeline** : vue Kanban filtrable par offre
- **Offres** : gestion des postes ouverts
- **Liste** : vue tabulaire
- Fiche candidat : entretiens, historique, actions avancer/refuser/embaucher

## API IPC

- `rh:ats:offres:*` — CRUD offres
- `rh:ats:pipeline` — données Kanban
- `rh:ats:candidatures:*` — création et avancement
- `rh:ats:entretiens:*` — planification entretiens
- `rh:ats:historique` — journal candidature

## Lot suivant

**Lot 3** — Temps & présence : réconciliation planning / pointage / paie, alertes H+15.
