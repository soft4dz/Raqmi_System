# Module Ressources humaines & paie

## Objectif

Le module RH couvre le socle des ressources humaines et le cycle complet de la paie algérienne :
référentiel des postes, dossiers collaborateurs, contrats de travail, temps de présence, absences,
puis pré-paie mensuelle, bulletins, validation et clôture verrouillante de la période.

Le périmètre fonctionnel est repris de `Hotel_Metrics_Pro_Desktop` (voir
[migration-from-hotel-metrics-pro-desktop.md](../migration-from-hotel-metrics-pro-desktop.md)) et
retranscrit selon la règle de transformation du dépôt : entités domaine, contrats applicatifs,
migration PostgreSQL, endpoints sécurisés, permissions serveur, audit et écran WPF.

## Données gérées

| Table (schéma `hr`) | Rôle |
|---|---|
| `departments` | Départements, référentiel partagé par tout le groupe |
| `positions` | Postes rattachés à un département, avec un salaire brut plancher |
| `employees` | Dossier collaborateur : identité, unité, poste, NIN, NSS, RIB, badge, enfants à charge |
| `employment_contracts` | Contrats CDI/CDD/saisonnier/stage, salaire brut contractuel, horaire hebdomadaire |
| `time_entries` | Heures travaillées par collaborateur et par jour, en brouillon ou validées |
| `absences` | Absences sur une plage de dates, avec circuit d'approbation |
| `payroll_parameter_sets` + `payroll_tax_brackets` | Paramètres légaux versionnés par période d'effet |
| `payroll_bonuses` | Primes variables du mois |
| `payslips` | Bulletins calculés, ligne par ligne |
| `payroll_periods` | État mensuel de la paie et verrou de clôture |

## Le moteur de paie

`AlgerianPayrollEngine` est une fonction pure `(faits, paramètres) -> bulletin`, sans base ni
horloge : un bulletin peut être contesté des années plus tard, son calcul doit donc être
reproductible à partir de ses seules entrées.

Ordre des opérations, qui est la partie fixée par la loi :

1. brut = base contractuelle + heures supplémentaires + primes − retenue d'absence sans solde ;
2. la cotisation salariale CNAS est retenue sur ce brut ;
3. la base IRG est le brut **moins** cette cotisation, moins l'abattement ;
4. le barème progressif s'applique à cette base ;
5. net = brut − cotisation − IRG.

Les charges patronales sont calculées sur le même brut et ne touchent jamais le net.

Trois identités sont vérifiables à la main sur le bulletin imprimé, et testées sur chaque scénario :

```
Brut imposable = Base + Heures sup + Primes - Retenue absence
Net à payer    = Brut imposable - CNAS salariale - IRG
Coût employeur = Brut imposable + CNAS patronale + charges parafiscales
```

### Paramètres légaux : des données, pas des constantes

Aucun taux, abattement, tranche ou SMIG n'est compilé dans le moteur. Ils vivent dans
`payroll_parameter_sets`, versionnés par période d'effet, et la paie résout « le jeu le plus récent
prenant effet à ou avant la période calculée ». Deux conséquences directes :

- une loi de finances est une saisie de données auditée, pas une livraison de code ;
- recalculer un mois ancien applique les taux qui s'appliquaient **alors**.

C'est la correction du principal défaut de maintenance du système d'origine, où ces valeurs étaient
des constantes du code.

Le jeu livré par `PayrollParameterSet.CreateStatutoryDefault` reprend fidèlement ce que calculait
l'ancien moteur : 173,33 h de référence mensuelle (semaine de 40 h), heures supplémentaires
majorées de 50 %, retenue d'absence sur un mois de 30 jours, CNAS 9 % salariale et 26 % patronale,
parafiscales patronales de 1,25 % (accident du travail), 1,5 % (chômage) et 1 % (formation),
abattement IRG de 40 000 DZD plus 1 000 par enfant à charge, barème 23 / 27 / 33 %, SMIG à
20 000 DZD.

> **À confirmer avant la première paie réelle.** Ces valeurs sont un portage fidèle de ce que
> calculait le système précédent, ce qui n'est pas la même affirmation que « elles sont à jour ».
> Elles doivent être vérifiées contre la loi de finances en vigueur — et c'est précisément pour
> que cette vérification soit une saisie et non un correctif que les paramètres sont des données.

## Le verrou de clôture

Le cycle du mois est : `Draft` (ouverte) → `Validated` (tous les bulletins contrôlés) → `Closed`
(verrouillée). La clôture est **irréversible** et refuse ensuite toute écriture susceptible de
modifier ce qui a déjà été déclaré :

- génération de pré-paie, validation de bulletin, prime : refusées ;
- pointage sur un jour du mois clôturé : refusé ;
- absence **sans solde** chevauchant le mois clôturé : refusée.

Une absence rémunérée (maladie, maternité) reste enregistrable sur un mois clôturé : elle ne change
aucun chiffre du bulletin, et refuser de la consigner corromprait le dossier RH pour protéger un
montant qui n'en dépend pas.

Une correction sur un mois clôturé passe par une régularisation sur une période ouverte, exactement
comme dans le processus papier.

## Idempotence de la pré-paie

Relancer la pré-paie recalcule les bulletins en **brouillon** et laisse intacts les bulletins
**validés**, qui sont comptés séparément dans `skippedValidated`. Corriger un pointage puis
relancer est donc l'opération normale, sans risque de réécrire un bulletin déjà signé.

## Permissions

| Clé | Portée |
|---|---|
| `hr.read` | Consulter le référentiel, les collaborateurs, contrats, pointages et absences |
| `hr.write` | Gérer les départements, postes, dossiers, contrats, pointages et absences |
| `hr.payroll` | Paramètres légaux, primes, génération de la pré-paie, validation des bulletins |
| `hr.payroll.close` | Valider puis clôturer une période — l'acte irréversible a sa propre clé |

Le rôle `hr.manager` porte les quatre clés et rien d'autre de l'ERP : les données personnelles de
la loi 18-07 et les montants de paie ne doivent pas voyager avec un profil d'exploitation. La
direction reçoit `hr.read` seul.

## Données personnelles (loi 18-07)

- La projection de liste (`GET /api/v1/hr/employees`) ne porte **aucun** identifiant légal.
  NIN, NSS et RIB ne sont servis que par la fiche détaillée.
- La lecture d'une fiche détaillée **écrit une entrée d'audit** nommant qui a consulté quel
  dossier : exposer ces données est en soi un acte sensible.
- Les détails d'audit ne recopient jamais les identifiants légaux — un journal qui recopie des
  données personnelles devient un second stock non protégé de ces données.
- Aucune donnée biométrique n'est stockée : le champ `badge_id` est un identifiant opaque.

## API

| Méthode | Route | Permission |
|---|---|---|
| GET / POST | `/api/v1/hr/departments` | `hr.read` / `hr.write` |
| PUT, POST `/{code}/activate\|deactivate` | `/api/v1/hr/departments/{code}` | `hr.write` |
| GET / POST | `/api/v1/hr/positions` | `hr.read` / `hr.write` |
| GET | `/api/v1/hr/employees` | `hr.read` |
| GET | `/api/v1/hr/employees/{id}` | `hr.read` (lecture auditée) |
| POST / PUT | `/api/v1/hr/employees` | `hr.write` |
| POST | `/api/v1/hr/employees/{id}/suspend\|reactivate\|terminate` | `hr.write` |
| GET / POST | `/api/v1/hr/employees/{id}/contracts` | `hr.read` / `hr.write` |
| PUT, POST `/{contractId}/end` | `/api/v1/hr/employees/{id}/contracts` | `hr.write` |
| GET / POST | `/api/v1/hr/time-entries` | `hr.read` / `hr.write` |
| POST | `/api/v1/hr/time-entries/{id}/validate` | `hr.write` |
| GET / POST | `/api/v1/hr/absences` | `hr.read` / `hr.write` |
| POST | `/api/v1/hr/absences/{id}/approve\|reject\|cancel` | `hr.write` |
| GET / POST | `/api/v1/hr/payroll/parameters` | `hr.read` / `hr.payroll` |
| GET | `/api/v1/hr/payroll/periods`, `/{period}` | `hr.read` |
| GET / POST / DELETE | `/api/v1/hr/payroll/periods/{period}/bonuses` | `hr.read` / `hr.payroll` |
| POST | `/api/v1/hr/payroll/periods/{period}/generate` | `hr.payroll` |
| GET | `/api/v1/hr/payroll/periods/{period}/payslips` | `hr.read` |
| POST | `/api/v1/hr/payroll/periods/{period}/payslips/{id}/validate` | `hr.payroll` |
| POST | `/api/v1/hr/payroll/periods/{period}/validate` | `hr.payroll.close` |
| POST | `/api/v1/hr/payroll/periods/{period}/close` | `hr.payroll.close` |

La période s'écrit toujours `AAAA-MM`. Un format invalide est une erreur de validation, jamais un
résultat vide.

## Règles

- Un collaborateur n'a qu'**un seul contrat actif** à la fois (index unique filtré
  `ux_hr_contracts_active_per_employee`) : la pré-paie y lit le salaire de référence.
- Un badge identifie **un seul** collaborateur (`ux_hr_employees_badge_id`).
- Un pointage par collaborateur et par jour (`ux_hr_time_entries_employee_date`) : deux lignes
  seraient additionnées et doubleraient les heures supplémentaires.
- Seules les heures **validées** alimentent la paie ; seules les absences **approuvées** et **sans
  solde** réduisent le salaire.
- Un salarié parti en cours de mois reste payé pour ce mois : son dernier bulletin est la base du
  solde de tout compte.
- Le brut ne descend jamais sous zéro, même quand les retenues dépassent la base.
- Un contrat sous le plancher de son poste est refusé ; un brut sous le SMIG est signalé sur le
  bulletin sans altérer le calcul.

## Écran

Onglet **RH & paie** (index 22, module 21 du catalogue) : annuaire des collaborateurs à gauche,
cycle de paie du mois à droite (indicateurs de la période, bulletins ligne par ligne, points de
contrôle de la dernière pré-paie). La clôture est le seul bouton `DangerButton` de l'écran et
demande une confirmation explicite.

## Hors périmètre de cette vague

Ces fonctions du système d'origine ne sont pas reprises ici et restent à développer :
pointeuses et badgeuses (synchronisation ZKTeco, logs bruts, rapprochement des badges — module
21.2, toujours `Planifié`), ATS et recrutement, GPEC et campagnes d'évaluation, formations,
réconciliation planning/pointage avec alertes H+15, portail salarié, GED du dossier collaborateur,
passerelle DLG PC PAIE, exports déclaratifs (CNAS, DAS, DADS-U, ANEM, virements), bulletin PDF et
solde de tout compte.

Les exports déclaratifs se prêtent bien au module Rapports automatiques
([ReportCatalog](../../src/RaqmiSystem.Domain/Reporting/ReportCatalog.cs)), dont les exécutions
renvoient des données structurées exportables en CSV.
