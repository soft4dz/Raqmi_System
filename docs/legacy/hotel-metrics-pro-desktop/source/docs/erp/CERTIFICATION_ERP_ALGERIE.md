# Dossier de certification ERP — Raqmi System (Algérie)

**Application :** Raqmi System
**Version :** 0.8.0  
**Date du dossier :** 24 juillet 2026  
**Périmètre :** EGT Sidi Fredj — pilotage hôtelier multi-unités, marina PortMaster, RH algérienne, conformité légale Phases 1–3  
**Statut global :** **Certifiable techniquement — pilote EGT autorisé sous réserve validation métier externe**

> Ce document constitue un **dossier technique interne**. Il ne remplace pas une homologation officielle DGI, CNAS, inspection du travail ou avis d’expert-comptable agréé.

---

## 1. Résumé exécutif

Raqmi System est un ERP desktop **offline-first** (Electron + SQLite) couvrant :

- Exploitation hôtelière et marina (~30 modules métier)
- Comptabilité générale SCF simplifiée hôtellerie
- Fiscalité DGI (TVA, retenue à la source, liasse G50)
- Paie algérienne (CNAS, IRG, SMIG, clôture mensuelle)
- Contrôle interne DEC (clôture journalière, rapprochements, créances, cockpit)
- Hôtellerie légale (fiche police, taxe de séjour, rapports tourisme)
- Protection des données (Loi 18-07 / ANPDP)
- SIFEC sandbox, immobilisations, CASNOS, inventaire légal
- Packaging Windows NSIS + licence offline

**Score de maturité conformité réglementaire estimé : 78/100**

| Validation automatique | Résultat |
|------------------------|----------|
| `npm test` | 212 tests passent |
| `npm run audit:ipc` | Phase 3 à 100 % |
| `npm run validate:certification` | OK |

---

## 2. Matrice de conformité réglementaire

Légende statuts :

| Symbole | Signification |
|---------|---------------|
| ✅ | Implémenté et testé automatiquement |
| 🟡 | Implémenté — validation métier / homologation externe requise |
| ⚠️ | Partiel ou sandbox |
| ❌ | Non couvert |

### 2.1 Fiscalité et comptabilité (DGI / SCF)

| Exigence | Base réglementaire | Preuve technique | Statut |
|----------|-------------------|------------------|--------|
| Plan comptable SCF hôtellerie | SCF Algérie | `057_phase1_conformite_legale.sql`, `/comptabilite/plan` | ✅ |
| Journaux comptables (VE, BQ, CA, OD) | SCF | `comptabilite.service.ts`, `/comptabilite/journaux` | ✅ |
| Balance générale | SCF | `/comptabilite/balance` | ✅ |
| Clôture exercice comptable | SCF | `/comptabilite/exercices` | 🟡 |
| Écriture auto facture validée | SCF + CTCA | `facturation.service.ts` → VE (411/707/445710) | ✅ |
| Écriture auto encaissement | SCF | `tresorerie.service.ts` → BQ/CA | ✅ |
| Numérotation factures FAC-AAAA-NNNNN | Code des impôts | `factures_numerotation`, `phase1-conformite.test.ts` | ✅ |
| Avoirs AV-AAAA-NNNNN | Code des impôts | Module avoir + écriture inverse | ✅ |
| Registre des factures | Art. 45 CTCA (conservation) | `/facturation/registre`, `factures_registre` | ✅ |
| TVA 9 % / 19 % sur lignes | CTCA | `facturation.service.ts` (défaut 19 %) | ✅ |
| Timbre fiscal paramétrable | CTCA | `facturation-pdf.service.ts`, setting `invoice_timbre_amount` | 🟡 |
| Registre TVA ventes | CTCA | `/fiscalite/registre-tva`, alimentation à validation | ✅ |
| Registre TVA achats | CTCA | `/fiscalite/tva-achats`, import bons | 🟡 |
| Déclaration TVA mensuelle | DGI | `/fiscalite/declaration-tva` | 🟡 |
| Retenue à la source 15 % | IBS Algérie | `/fiscalite/retenue-source` | 🟡 |
| Liasse fiscale G50 simplifiée | DGI | `/fiscalite/liasse` | 🟡 |
| Liasse G50/G4/G29 avancée | DGI | `fiscalite-avancee.service.ts`, bouton liasse avancée | 🟡 |
| Télédéclaration TVA (export G50) | DGI | `/fiscalite/teledeclarations` | ⚠️ export + suivi, pas d’API DGI live |
| SIFEC facturation électronique | DGI / SIFEC | `/fiscalite/sifec/*`, sandbox | ⚠️ |
| Immobilisations & amortissements | SCF classe 2 | `/conformite/modules-legaux/immobilisations` | 🟡 |
| Inventaire physique annuel | SCF + Code de commerce | `/conformite/modules-legaux/inventaire` | 🟡 |

### 2.2 Social et paie (CNAS / Code du travail)

| Exigence | Base réglementaire | Preuve technique | Statut |
|----------|-------------------|------------------|--------|
| Calcul CNAS salarié 9 % / patronale 26 % | CNAS | `rh-paie-dz-engine.ts`, tests | ✅ |
| Calcul IRG (barème progressif) | Code des impôts directs | `rh-paie-dz-engine.test.ts` | 🟡 |
| SMIG paramétrable (20 000 DZD) | Code du travail | `SMIG_DZD` dans moteur paie | 🟡 |
| Cotisations parafiscales (AT, chômage, formation) | Législation sociale | `rh_paie_params`, moteur enrichi | 🟡 |
| Clôture paie mensuelle | Bonnes pratiques + audit | `/rh/paie/cloture`, `rh_paie_clotures` | ✅ |
| Bulletins de paie PDF | Code du travail | Module RH paie | 🟡 |
| Export CNAS mensuel CSV | CNAS | `rh-declarations-export.service.ts` | ⚠️ format interne, pas portail binaire |
| Export DAS annuelle | DGI | Export DAS CSV enrichi | ⚠️ |
| Export ANEM embauches | ANEM | `/rh` → export ANEM | 🟡 |
| Registre du personnel | Code du travail | `RegistresLegauxTab`, registre personnel | ✅ |
| Registre congés | Loi 90-11 | Registre congés RH | ✅ |
| Accidents du travail + déclaration CNAS | Code du travail | Registre accidents | 🟡 |
| Visites médicales | Code du travail | Registre visites | 🟡 |
| Conformité ANEM 48h, NSS, NIN, SMIG | Code du travail | `rh-conformite-dz.service.ts` | ✅ |
| STC / rupture contrat | Code du travail | `rh-rupture-contrat.service.ts` (indicatif) | 🟡 |
| CASNOS (TNS) | CASNOS | `/conformite/modules-legaux/casnos` | 🟡 |

### 2.3 Hôtellerie et tourisme

| Exigence | Base réglementaire | Preuve technique | Statut |
|----------|-------------------|------------------|--------|
| Fiche de police / registre clients | Réglementation hôtelière (Intérieur) | `/hotel-legal`, `hotel-legal.service.ts` | 🟡 |
| Taxe de séjour | Collectivités / wilaya | `/hotel-legal`, calcul par nuitées | 🟡 |
| Rapports statistiques tourisme | ONS / Ministère Tourisme | Export CSV/PDF tourisme | ⚠️ format interne |
| PMS hébergement (réservations, occupation) | Exploitation | `/hebergement` | ✅ |
| Lien fiche police ↔ réservation | Traçabilité | Service hôtellerie légale | 🟡 |

### 2.4 Protection des données (Loi 18-07)

| Exigence | Base réglementaire | Preuve technique | Statut |
|----------|-------------------|------------------|--------|
| Registre des traitements | Art. 30 Loi 18-07 | `/conformite/donnees-personnelles/traitements` | ✅ |
| Consentements | Art. 7 Loi 18-07 | `/conformite/donnees-personnelles/consentements` | 🟡 |
| Exercice des droits (accès, rectification…) | Art. 7–12 Loi 18-07 | `/conformite/donnees-personnelles/demandes` | 🟡 |
| Registre incidents / notification ANPDP | Art. 41 Loi 18-07 | `/conformite/donnees-personnelles/incidents` | 🟡 |
| Politique de conservation | Loi 18-07 + GED | `/conformite/donnees-personnelles/conservation` | ✅ |
| DPO désigné côté EGT | Loi 18-07 | **Organisationnel — hors logiciel** | ❌ |

### 2.5 Contrôle interne et gouvernance

| Exigence | Preuve technique | Statut |
|----------|------------------|--------|
| Workflow transversal | `/workflows`, `workflow.service.ts` | 🟡 partiel sur validations métier |
| Clôture journalière unité | `/recettes/cloture`, verrouillage dates | ✅ |
| Alerte retard saisie 09h30 | `dec-cockpit.service.ts`, widget DEC | 🟡 déclenchement à ouverture cockpit |
| Rapprochement recettes/encaissements | `/finance/rapprochements` | ✅ |
| Créances globales + balance âgée | `/creances` | ✅ |
| Cockpit DEC | `/dec/cockpit` | ✅ |
| Dashboard PDG + rapport mensuel Excel | `/dashboard/pdg` | ✅ |
| Checklists qualité/hygiène/maintenance | `/controle/checklists` | ✅ |
| Journal d’audit | `/audit/logs` | ✅ |
| Archivage légal GED 10 ans + SHA-256 | `/ged/archivage-legal` | ✅ |
| Santé système | `/settings/system-health` | ✅ |

### 2.6 Industrialisation et déploiement

| Exigence | Preuve technique | Statut |
|----------|------------------|--------|
| Tests automatisés (212) | `npm test`, `phase*.test.ts` | ✅ |
| Audit IPC Phase 3 | `npm run audit:ipc` | ✅ |
| CI GitHub Actions | `.github/workflows/ci.yml` | ✅ |
| Installateur NSIS Windows x64 | `npm run dist:installer` | ✅ |
| Licence offline RS-* | `license.service.ts`, Paramètres | ✅ |
| Sauvegarde / restauration | `/settings/backup` | 🟡 procédure à tester sur site |
| Sync multi-postes | `/system/sync` | 🟡 optionnel |

---

## 3. Preuves par module (fichiers clés)

### Phase 1 — Conformité légale

| Module | Service | Migration | Tests |
|--------|---------|-----------|-------|
| Comptabilité SCF | `electron/services/comptabilite.service.ts` | `057_phase1_conformite_legale.sql` | `phase1-conformite.test.ts` |
| Facturation conforme | `electron/services/facturation.service.ts` | idem + registre | idem |
| Fiscalité DGI | `electron/services/fiscalite-dz.service.ts` | idem | idem |
| Paie DZ | `electron/services/rh-paie-dz-engine.ts` | `rh_paie_clotures` | `rh-paie-dz-engine.test.ts` |

Doc détaillée : `docs/erp/phase1-conformite-legale.md`

### Phase 2 — Contrôle & hôtellerie

| Module | Service | Migration |
|--------|---------|-----------|
| Workflow | `workflow.service.ts` | `055_erp_10_axes_foundation.sql` |
| Clôture journalière | `daily-closure.service.ts` | idem + `058_phase2_*` |
| Rapprochement | `finance-reconciliation.service.ts` | idem |
| Créances | `creances.service.ts` | idem |
| Cockpit DEC | `dec-cockpit.service.ts` | idem |
| Dashboard PDG | `dashboard-pdg.service.ts` | idem |
| Hôtellerie légale | `hotel-legal.service.ts` | `058_phase2_*` |
| GED archivage | `ged-archivage.service.ts` | idem |
| Santé système | `system-health.service.ts` | idem |

Doc détaillée : `docs/erp/phase2-controle-hotellerie-archivage.md`  
Tests : `electron/services/phase2-controle.test.ts`

### Phase 3 — Certification

| Module | Service | Migration | Tests |
|--------|---------|-----------|-------|
| RGPD Loi 18-07 | `rgpd-anpdp.service.ts` | `059_phase3_rgpd_loi1807.sql` | `phase3-rgpd.test.ts` |
| SIFEC | `sifec-connector.service.ts` | `060_phase3_fiscalite_sifec.sql` | `phase3-sifec.test.ts` |
| Fiscalité avancée | `fiscalite-avancee.service.ts` | idem | idem |
| Immobilisations | `immobilisations.service.ts` | `061_phase3_modules_legaux.sql` | `phase3-modules-legaux.test.ts` |
| CASNOS | `casnos.service.ts` | idem | idem |
| Inventaire légal | `inventaire-legal.service.ts` | idem | idem |
| Licence | `license.service.ts` | — | `phase3-packaging.test.ts` |

Doc détaillée : `docs/erp/phase3-certification.md`

---

## 4. Flux de contrôle DEC (chaîne critique)

```text
Recettes journalières (09h00)
        │
        ▼
Encaissements du jour
        │
        ├──► Rapprochement (/finance/rapprochements)
        │         │
        │         └── écart non justifié ──► Anomalie + alerte DEC
        │
        ▼
Clôture journalière unité (/recettes/cloture)
        │
        ├── verrouillage recettes + encaissements
        │
        ▼
Validation DEC (workflow)
        │
        ▼
Cockpit DEC (/dec/cockpit) — widget retard 09h30
        │
        ▼
Dashboard PDG (/dashboard/pdg) — consolidation mensuelle
```

---

## 5. Plan de tests manuels par profil

### 5.1 Comptable / fiscaliste

| # | Scénario | Résultat attendu | PV |
|---|----------|------------------|-----|
| C1 | Valider facture FAC-2026-00001 | Registre + écriture VE + TVA ventes | ☐ |
| C2 | Émettre avoir sur facture | Écriture inverse + registre TVA type avoir | ☐ |
| C3 | Confirmer encaissement espèces | Écriture CA dans journaux | ☐ |
| C4 | Balance générale exercice 2026 | Équilibre débit = crédit | ☐ |
| C5 | Déclaration TVA mois courant | Montants cohérents registre | ☐ |
| C6 | Retenue source sur paiement fournisseur | Enregistrement 15 % | ☐ |
| C7 | Export liasse G50 | CSV exploitable par cabinet | ☐ |
| C8 | Préparer transmission SIFEC sandbox | Payload + QR sur PDF | ☐ |

**Signataire :** _________________ Date : _________

### 5.2 Responsable RH / paie

| # | Scénario | Résultat attendu | PV |
|---|----------|------------------|-----|
| R1 | Pré-paie → valider → clôturer période | Modification bloquée après clôture | ☐ |
| R2 | Export CNAS mensuel | Colonnes parafiscales présentes | ☐ |
| R3 | Export DAS annuelle | Totaux cohérents bulletins | ☐ |
| R4 | Tableau conformité (NSS, ANEM, SMIG) | Alertes correctes | ☐ |
| R5 | Registre personnel + congés | Export PDF/CSV | ☐ |

**Signataire :** _________________ Date : _________

### 5.3 Contrôleur DEC / directeur unité

| # | Scénario | Résultat attendu | PV |
|---|----------|------------------|-----|
| D1 | Saisie recettes avant 09h30 | Pas d’alerte retard | ☐ |
| D2 | Absence clôture après 09h30 | Alerte cockpit DEC | ☐ |
| D3 | Rapprochement avec écart | Anomalie créée | ☐ |
| D4 | Clôture journalière complète | Dates verrouillées | ☐ |
| D5 | Checklist qualité chambres | Plan d’action si non-conforme | ☐ |

**Signataire :** _________________ Date : _________

### 5.4 Réception / hébergement

| # | Scénario | Résultat attendu | PV |
|---|----------|------------------|-----|
| H1 | Check-in réservation → fiche police | Données identité complètes | ☐ |
| H2 | Calcul taxe de séjour période | Montant cohérent nuitées | ☐ |
| H3 | Export registre police journalier | PDF conforme usage interne | ☐ |

**Signataire :** _________________ Date : _________

### 5.5 Admin système / DPO

| # | Scénario | Résultat attendu | PV |
|---|----------|------------------|-----|
| A1 | Registre traitements RGPD | 4 traitements seed visibles | ☐ |
| A2 | Demande d’accès employé | Workflow + échéance J+30 | ☐ |
| A3 | Archivage facture validée GED | Hash SHA-256, rétention 10 ans | ☐ |
| A4 | Tentative suppression doc archivé | Refus + audit | ☐ |
| A5 | Santé système | Licence OK, migrations ≥ 061 | ☐ |
| A6 | Backup manuel + restauration test | Base intacte post-restore | ☐ |

**Signataire :** _________________ Date : _________

---

## 6. Limites connues et réserves

| # | Limite | Impact | Mesure compensatoire |
|---|--------|--------|----------------------|
| L1 | SIFEC en sandbox uniquement | Factures non télédéclarées DGI | PDF + registre interne jusqu’à connecteur prod |
| L2 | Exports CNAS/DAS CSV internes | Dépôt portail manuel | Validation format par cabinet paie |
| L3 | Calculs paie/STC « indicatifs » | Risque contentieux | Visa expert-comptable mensuel |
| L4 | Liasse G50 non exhaustive | Risque contrôle fiscal | Compléter avec cabinet fiscal |
| L5 | Workflow non branché partout | Traçabilité incomplète | Phase 4 — voir `phase4-pilote-egt-homologation.md` |
| L6 | Alerte 09h30 passive | Retard non détecté si cockpit fermé | Planificateur background Phase 4 |
| L7 | Pas de DPO formalisé dans l’app | Loi 18-07 organisationnelle | Désigner DPO EGT + registre ANPDP |
| L8 | IPC legacy sans validation Zod | Risque sécurité modéré | Étendre audit IPC Phase 1/2 |

---

## 7. Commandes de vérification release

```powershell
cd Hotel_Metrics_Pro_Desktop
npm run validate:certification   # tests + audit IPC + packaging + tsc
npm run dist:installer           # build installateur NSIS
npm run generate:license PRO 2027-12-31
```

---

## 8. Documents associés

| Document | Contenu |
|----------|---------|
| `docs/erp/phase1-conformite-legale.md` | Détail Phase 1 |
| `docs/erp/phase2-controle-hotellerie-archivage.md` | Détail Phase 2 |
| `docs/erp/phase3-certification.md` | Détail Phase 3 |
| `docs/erp/phase4-pilote-egt-homologation.md` | Prompt pilote EGT + homologation |
| `docs/erp/phase5-completude-modules-mvp.md` | Prompt complétude modules MVP (72 % → 85 %) |
| `docs/erp/phase5-completude-livraison.md` | Rapport livraison Phase 5 (223 tests, ~85 % exploitation) |
| `docs/erp/phase6-automatisation-production-rh.md` | Production cuisine + pointeuses RH (événements métier) |
| `docs/erp/phase6-livraison.md` | Rapport livraison Phase 6 (~88 % exploitation) |
| `docs/STABILISATION_PRODUCTION.md` | Checklist production |
| `docs/PROCEDURE_SAUVEGARDE_RESTAURATION.md` | Backup / restore |
| `docs/guides-utilisateurs/` | Guides par profil |

---

## 9. Approbations (à compléter)

| Rôle | Nom | Signature | Date | Avis |
|------|-----|-----------|------|------|
| Direction EGT / PDG | | | | ☐ Pilote autorisé ☐ Réserves ☐ Refus |
| Contrôle DEC | | | | |
| Expert-comptable | | | | |
| Responsable RH | | | | |
| RSSI / DPO | | | | |
| Éditeur Raqmi System | | | | |

---

**Raqmi System** — Dossier certification ERP Algérie v0.8.0
