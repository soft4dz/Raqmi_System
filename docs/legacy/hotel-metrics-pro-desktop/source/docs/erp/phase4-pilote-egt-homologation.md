# Phase 4 — Pilote EGT Sidi Fredj & homologation externe

**Application :** Raqmi System v0.8.0
**Prérequis :** Phases 1, 2 et 3 validées + dossier `CERTIFICATION_ERP_ALGERIE.md`  
**Objectif :** Déployer en pilote contrôlé, obtenir les visas métier externes, combler les écarts résiduels, préparer la mise en production généralisée EGT.

---

## Prompt agent Phase 4 (copier-coller)

```markdown
# Mission : Phase 4 — Pilote EGT Sidi Fredj & homologation externe (Raqmi System v0.8.0)

## ⚠️ PRÉREQUIS OBLIGATOIRES — NE PAS DÉMARRER SANS VALIDATION PHASES 1–3

Exécuter cette checklist bloquante avant tout code :

### Phase 1
- [ ] `docs/erp/phase1-conformite-legale.md` présent
- [ ] Comptabilité, facturation conforme, fiscalité DGI, paie clôturable opérationnels
- [ ] `electron/services/phase1-conformite.test.ts` passe

### Phase 2
- [ ] `docs/erp/phase2-controle-hotellerie-archivage.md` présent
- [ ] Workflow, clôture, rapprochement, créances, cockpit DEC, PDG, checklists, hôtel légal, GED archivage
- [ ] `electron/services/phase2-controle.test.ts` passe

### Phase 3
- [ ] `docs/erp/phase3-certification.md` présent
- [ ] RGPD, SIFEC sandbox, fiscalité avancée, modules légaux, licence, packaging
- [ ] `npm run validate:certification` passe (212+ tests)

### Dossier certification
- [ ] `docs/erp/CERTIFICATION_ERP_ALGERIE.md` présent et à jour

**Si un point manque → STOP. Rapport NO-GO sans implémentation.**

---

## Contexte

ERP desktop Electron+SQLite pour EGT Sidi Fredj (hôtels + marina PortMaster).
Phases 1–3 livrées (~78/100 conformité réglementaire).
Phase 4 = **pilote terrain + homologation + finition réglementaire**, pas de refonte.

Référence écarts : section 6 de `docs/erp/CERTIFICATION_ERP_ALGERIE.md`

---

## Contraintes

- Architecture : UI → IPC → service → SQLite → audit/workflow
- Ne pas refaire Phases 1–3 sauf corrections bugs pilote
- Répondre en français
- Commits uniquement si demandé
- Toute modification réglementaire → mettre à jour `CERTIFICATION_ERP_ALGERIE.md`

---

## Lot 1 — Infrastructure pilote EGT

### 1.1 Script déploiement pilote

Créer `scripts/deploy-pilote-egt.mjs` :
- Vérifier Windows 10/11 x64, 8 Go RAM min
- Copier installateur ou lancer `npm run dist:installer`
- Seed données EGT Sidi Fredj (unités, directions RH déjà en 054)
- Générer comptes pilote par profil (DEC, comptable, RH, réception, PDG)
- Checklist post-install (migrations ≥ 061, licence, backup)

### 1.2 Configuration EGT

Créer `electron/database/seeds/egt-pilote-config.sql` ou service :
- NIF / RC / raison sociale EGT
- Wilaya Tipaza (Sidi Fredj)
- Taux taxe de séjour par unité
- Taux timbre fiscal
- Agence CNAS
- Paramètres SIFEC sandbox

### 1.3 Page admin pilote

Route `/admin/pilote-egt` (SUPERADMIN) :
- Statut checklist déploiement
- Export configuration JSON pour support
- Lien vers PV tests manuels (section 5 du dossier certification)

---

## Lot 2 — Planificateur alertes & notifications DEC

Combler limite L6 (alerte 09h30 passive).

Créer `electron/services/scheduler-dec.service.ts` :
- Au démarrage Electron + toutes les 15 min : `checkClosureDeadlineAlerts()`
- Notification OS Windows si unité non clôturée après 09h30
- Enregistrement alerte DEC même sans ouverture cockpit
- Paramètre : `/settings/notifications` → activer rappels DEC

Tests : `electron/services/scheduler-dec.test.ts`

---

## Lot 3 — Workflow transversal complet

Combler limite L5.

Brancher `workflow.service.ts` sur :
- `facturation.service.ts` → validation facture (`workflow:submit` avant valider)
- `achats.service.ts` → validation bon de commande
- `recettes.service.ts` → soumission recette (si pas déjà)
- Historique workflow visible dans fiche facture / BC / recette

IPC : réutiliser existant. UI : composant `<WorkflowHistoryPanel entityType entityId />`.

Tests : workflow facture bout-en-bout.

---

## Lot 4 — SIFEC connecteur production (abstraction)

**Ne pas hardcoder API DGI sans specs officielles.**

Enrichir `sifec-connector.service.ts` :
- Interface `SifecConnector` : `prepare()`, `transmit()`, `getStatus()`, `cancel()`
- Implémentations : `SifecMockConnector` (existant), `SifecHttpConnector` (stub configurable)
- Config production : URL, certificat client, NIF, mode test/prod
- File d’attente offline `sifec_transmission_queue` avec retry exponentiel
- Page `/fiscalite/sifec/config` : test connexion + journal transmissions

Doc : `docs/erp/SIFEC_INTEGRATION.md` (procédure activation quand DGI fournit specs).

Tests : mock HTTP, retry queue.

---

## Lot 5 — Exports réglementaires format cabinet

Enrichir exports pour homologation expert-comptable :

| Export | Fichier cible | Format |
|--------|---------------|--------|
| CNAS mensuel | `rh-declarations-export.service.ts` | CSV colonnes documentées + template commenté portail |
| DAS annuelle | idem | Enrichir rubriques |
| Liasse G50 | `fiscalite-avancee.service.ts` | Export Excel multi-feuilles G4/G29/G50 |
| Registre police | `hotel-legal.service.ts` | PDF registre journalier officiel |
| Balance + GL | `comptabilite.service.ts` | Export Excel balance + grand livre |

Chaque export → archivage GED auto (`ged-archivage.service.ts`).

---

## Lot 6 — PV tests manuels digitalisés

Créer module léger de recette métier :

Tables :
- `certification_test_runs` (profil, date, opérateur)
- `certification_test_results` (scenario_id, statut, notes, pièce_jointe_ged_id)

UI : `/admin/certification-tests` (admin + profils habilités)
- Reprend scénarios C1–A6 de `CERTIFICATION_ERP_ALGERIE.md` section 5
- Statut : à faire / OK / échec / N/A
- Export PDF PV signé (nom + date + synthèse)

Service : `electron/services/certification-pv.service.ts`

---

## Lot 7 — Extension audit IPC Phase 1 & 2

Étendre `electron/ipc/ipc-security-audit.ts` :
- Passer de « Phase 3 bloquante » à « Phase 1+2+3 bloquante » pour handlers :
  - recettes, facturation, tresorerie, comptabilite, fiscalite, cloture, reconciliation
- CI : `npm run audit:ipc` échoue si couverture < 100 % sur ces fichiers

Corriger handlers manquants.

---

## Lot 8 — Synchronisation catalogue modules

Mettre à jour `src/modules/moduleCatalog.ts` :
- `creances-recouvrement` → `operationnel` (route `/creances`)
- `contrats-conventions` → `operationnel` si PortMaster contrats OK
- Ajouter modules conformité : comptabilité, fiscalité, RGPD, modules légaux, cockpit DEC

Mettre à jour `erpImprovementCatalog.ts` : axes 1–10 → statut `operationnel`.

---

## Lot 9 — Guides utilisateurs & formation EGT

Créer / compléter :
- `docs/guides-utilisateurs/12-comptabilite-fiscalite.md`
- `docs/guides-utilisateurs/13-conformite-rgpd.md`
- `docs/guides-utilisateurs/14-cloture-controle-dec.md`
- `docs/guides-utilisateurs/15-pilote-egt-sidi-fredj.md` (procédure 30 jours)

Inclure : qui fait quoi, horaires clôture, escalade anomalies.

---

## Lot 10 — Rapport de clôture pilote

À la fin du pilote (30 jours), produire automatiquement :

Service `electron/services/pilote-report.service.ts` :
- KPI : taux clôture journalière, écarts rapprochement, factures SIFEC, incidents RGPD
- Export PDF « Rapport pilote EGT » pour PDG

Mettre à jour `CERTIFICATION_ERP_ALGERIE.md` :
- Section 9 Approbations remplie
- Score conformité révisé post-pilote
- Liste écarts clos / ouverts

---

## Ordre d'exécution

1. Checklist Phases 1–3 (STOP si incomplet)
2. Lot 7 Audit IPC (sécurise le reste)
3. Lot 2 Planificateur DEC
4. Lot 3 Workflow complet
5. Lot 8 Catalogue modules
6. Lot 1 Déploiement pilote
7. Lot 5 Exports cabinet
8. Lot 6 PV tests digitalisés
9. Lot 4 SIFEC production stub
10. Lot 9 Guides formation
11. Lot 10 Rapport pilote (structure prête avant pilote terrain)

---

## Définition of Done Phase 4

- [ ] Checklists Phases 1–3 validées
- [ ] Planificateur alertes 09h30 actif au démarrage
- [ ] Workflow sur validation facture + achats
- [ ] Audit IPC Phase 1+2+3 à 100 % en CI
- [ ] Script deploy pilote EGT fonctionnel
- [ ] PV tests manuels digitalisés (12+ scénarios)
- [ ] Exports cabinet Excel/PDF enrichis
- [ ] SIFEC connecteur HTTP stub + doc intégration
- [ ] Guides utilisateurs 12–15 rédigés
- [ ] `CERTIFICATION_ERP_ALGERIE.md` mis à jour
- [ ] `npm run validate:certification` passe
- [ ] Doc `docs/erp/phase4-pilote-egt-livraison.md` rédigée

---

## Hors scope Phase 4

- Homologation officielle DGI / ANPDP (organisationnel, hors code)
- Migration NestJS / Tauri / Electron 42
- i18n
- Module recrutement complet
- Déploiement multi-sites sync cloud obligatoire

---

## Format restitution agent

1. Résultat checklists Phases 1–3
2. Fichiers créés/modifiés
3. Matrice écarts L1–L8 (CERTIFICATION) : clos / partiel / ouvert
4. Procédure déploiement pilote EGT (étapes + comptes)
5. Plan formation 30 jours
6. Recommandation GO/NO-GO production généralisée EGT
```

---

## Calendrier pilote EGT suggéré (30 jours)

| Semaine | Unités | Focus | Livrable |
|---------|--------|-------|----------|
| S1 | 1 hôtel pilote | Recettes, clôture, rapprochement | PV tests D1–D4 |
| S2 | + marina PortMaster | Facturation, créances | PV tests C1–C3 |
| S3 | Toutes unités DEC | Cockpit, PDG, checklists | PV tests DEC + PDG |
| S4 | Toutes | Comptabilité, fiscalité, paie, RGPD | PV tests C4–R5, A1–A6 |
| Clôture | Direction | Bilan pilote | Rapport Lot 10 + signatures section 9 |

---

## Rôles EGT pilote

| Profil | Compte type | Modules critiques |
|--------|-------------|-------------------|
| PDG | `pdg@egt.local` | Dashboard PDG, rapports |
| DEC | `dec@egt.local` | Cockpit DEC, clôture, workflows |
| Directeur unité | `du.hotel@egt.local` | Recettes, clôture unité |
| Comptable | `compta@egt.local` | Comptabilité, fiscalité, facturation |
| RH | `rh@egt.local` | Paie, conformité, registres |
| Réception | `reception@egt.local` | Hébergement, fiche police |
| Admin | `admin@egt.local` | Paramétrage, santé système, RGPD |

---

## Liens

- Dossier certification : [CERTIFICATION_ERP_ALGERIE.md](./CERTIFICATION_ERP_ALGERIE.md)
- Phase 1 : [phase1-conformite-legale.md](./phase1-conformite-legale.md)
- Phase 2 : [phase2-controle-hotellerie-archivage.md](./phase2-controle-hotellerie-archivage.md)
- Phase 3 : [phase3-certification.md](./phase3-certification.md)
