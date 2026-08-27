# Fondation ERP — 10 axes d'amélioration

Ce document décrit les 10 axes d'amélioration posés dans la migration `055_erp_10_axes_foundation.sql` et le catalogue TypeScript `src/shared/erpImprovementCatalog.ts`.

L'objectif n'est pas d'empiler de nouveaux écrans sans logique métier. L'objectif est de transformer Raqmi System en ERP interne cohérent pour l'EGT Sidi Fredj : données communes, workflow commun, clôture journalière, pilotage DEC/PDG et traçabilité.

---

## 1. Cockpit DEC

### Socle livré

- Table `dec_cockpit_alerts`.
- Table `dec_cockpit_widgets`.
- Widgets standards : CA jour, retards saisie 09h30, occupation, encaissements, anomalies, réclamations, maintenance urgente, présence RH, créances, décisions.

### À implémenter côté application

- Page cible : `/dec/cockpit`.
- Service : `electron/services/dec-cockpit.service.ts`.
- IPC : `dec:cockpit:get`, `dec:alerts:list`, `dec:alerts:close`.
- Génération automatique d'alerte si une unité n'a pas transmis son CA avant 09h30.
- Cartes par unité avec niveaux : normal, warning, critical.

---

## 2. Clôture journalière par unité

### Socle livré

- Table `daily_closures`.
- Table `daily_closure_items`.
- Statuts : brouillon, soumis, validé unité, validé DEC, refusé, clôturé.

### À implémenter côté application

- Page cible : `/recettes/cloture`.
- Préremplissage automatique depuis les recettes et les encaissements.
- Validation directeur d'unité puis validation DEC.
- Blocage des modifications après clôture.
- Génération automatique d'écart si CA déclaré différent des encaissements + créances.

---

## 3. Créances globales

### Socle livré

- Table `global_creances`.
- Table `global_creance_relances`.
- Statuts : ouverte, partielle, réglée, litige, irrécouvrable, annulée.
- Niveaux de risque : faible, normal, élevé, critique.

### À implémenter côté application

- Page cible : `/creances`.
- Génération d'une créance depuis facture impayée.
- Intégration des créances hébergement, entreprises, agences, sponsoring et PortMaster.
- Balance âgée par client, unité et ancienneté.
- Relances : téléphone, email, courrier, mise en demeure.

---

## 4. Moteur transversal de workflow

### Socle livré

- Table `erp_standard_statuses`.
- Table `workflow_instances`.
- Table `workflow_history`.

### À implémenter côté application

- Service : `electron/services/workflow.service.ts`.
- IPC : `workflow:create`, `workflow:submit`, `workflow:approve`, `workflow:reject`, `workflow:history`.
- Modules à brancher progressivement : recettes, facturation, achats, RH, maintenance, décisions et PortMaster.
- Historique visible dans chaque fiche métier.

---

## 5. Organisation EGT et effectifs cibles

### Socle livré

- Référentiel directions, départements et postes EGT Sidi Fredj via la migration `054_egt_sidi_fredj_rh_referentiel.sql`.
- Table `rh_effectifs_cibles_egt`.

### À implémenter côté application

- Page cible : `/rh/organisation/egt`.
- Organigramme général.
- Effectif cible par direction, unité et poste.
- Effectif réel depuis `rh_employes` et `rh_affectations`.
- Écart cible/réel.
- Export organigramme et état des effectifs.

---

## 6. Fiches de poste et compétences

### Socle livré

- Table `rh_fiches_poste`.
- Liaison avec `rh_postes`, `rh_directions` et `rh_departements`.
- Versionning simple.

### À implémenter côté application

- Page cible : `/rh/fiches-poste`.
- Éditeur fiche de poste.
- Export PDF.
- Liaison avec recrutement, évaluation, formation et compétences.
- Modèles de fiches de poste pour les postes EGT.

---

## 7. Dashboard PDG et rapports standards

### Socle livré

- Table `dashboard_kpi_definitions`.
- Table `standard_report_definitions`.
- KPI standards : CA jour, CA mois, objectif/réalisé, occupation, créances, encaissements, anomalies, réclamations, absentéisme, interventions urgentes.
- Rapports standards : CA quotidien, rapport mensuel CA pour CA, occupation, créances, RH, qualité, maintenance.

### À implémenter côté application

- Page cible : `/dashboard/pdg`.
- Vue consolidée par unité.
- Cartes KPI filtrables par période.
- Exports PDF/Excel.
- Rapport mensuel prêt pour Conseil d'Administration.

---

## 8. Rapprochement recettes / encaissements

### Socle livré

- Table `finance_reconciliations`.
- Champs : CA déclaré, espèces, TPE, virements, chèques, créances, total rapproché, écart.
- Statuts : à contrôler, équilibré, écart justifié, écart non justifié, validé.

### À implémenter côté application

- Page cible : `/finance/rapprochements`.
- Alimentation depuis recettes journalières et trésorerie.
- Création automatique d'anomalie en cas d'écart non justifié.
- Liaison avec la clôture journalière.

---

## 9. Checklists DEC, qualité, hygiène et maintenance

### Socle livré

- Tables `control_checklist_templates`, `control_checklist_items`, `control_checklist_runs`, `control_checklist_results`.
- Modèles initiaux : contrôle DEC CA journalier, qualité chambres, hygiène restauration, maintenance préventive, sécurité accès.

### À implémenter côté application

- Page cible : `/controle/checklists`.
- Exécution de checklist par unité.
- Preuve obligatoire selon criticité.
- Plan d'action automatique si non conforme.
- Taux de clôture par unité et par domaine.

---

## 10. Sécurisation IPC, sauvegarde, sync et tests

### Socle livré

- Table `backup_policies`.
- Table `sync_conflict_log`.
- Catalogue de l'axe dans `erpImprovementCatalog.ts`.

### À implémenter côté application

- Validation stricte des payloads IPC avec Zod ou équivalent.
- Tests migrations SQL.
- Tests workflows critiques.
- Écran santé système : sauvegardes, conflits sync, erreurs critiques.
- Alerte si aucune sauvegarde récente ou conflit sync ouvert.

---

# Ordre d'exécution recommandé

1. Cockpit DEC.
2. Clôture journalière.
3. Rapprochement financier.
4. Créances globales.
5. Workflow transversal.
6. Organisation EGT.
7. Fiches de poste.
8. Dashboard PDG.
9. Checklists contrôle.
10. Sécurisation IPC/tests/sauvegarde/sync.

---

# Règle de développement

Chaque nouvel écran doit respecter cette chaîne :

```text
UI React → preload electronAPI → IPC handler → service métier → SQLite → audit/workflow si action sensible
```

Aucune validation métier importante ne doit rester uniquement dans le renderer. Le renderer affiche, le service décide. Sinon on fabrique une passoire avec des icônes modernes, ce qui n'aide personne.
