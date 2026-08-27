# Phase 1 — Conformité légale ERP algérien

Document de restitution — Raqmi System v0.8.0 (Raqmi System)

## Périmètre livré

### Lot 1 — Comptabilité générale SCF
- Migration `057_phase1_conformite_legale.sql` : tables `comptes`, `journaux`, `exercices_comptables`, `ecritures_comptables`, `lignes_ecriture`
- Seed plan comptable SCF simplifié hôtellerie (classes 1–7)
- Service `electron/services/comptabilite.service.ts`
- IPC `comptabilite:*`, preload, types `src/shared/types/comptabilite.ts`
- UI `/comptabilite/*` (hub, plan, saisie OD, journaux, balance, exercices)
- Intégration auto : validation facture → écriture VE ; confirmation encaissement → écriture BQ/CA

### Lot 2 — Facturation conforme
- Colonnes factures : `type_document`, `serie`, `exercice`, `facture_origine_id`, `verrouillee`, métadonnées validation
- Tables `factures_numerotation`, `factures_registre`, `factures_fiscales_metadata`
- Numérotation légale `FAC-AAAA-00001` / `AV-AAAA-00001` à la validation (sans trou)
- Statuts étendus : proforma, envoyée, payée partielle, avoir émis
- Module avoir lié à facture d'origine
- Registre des factures + export CSV
- Préparation SIFEC (NIF, hash document, QR placeholder, horodatage)
- UI `/facturation/registre`

### Lot 3 — Fiscalité DGI
- Tables `registre_tva_ventes/achats`, `declarations_tva`, `retenues_source`, `liasse_fiscale_lignes`
- Service `electron/services/fiscalite-dz.service.ts`
- IPC `fiscalite:*`, UI `/fiscalite/*`
- Alimentation registre TVA ventes à la validation facture/avoir
- Calcul déclaration TVA mensuelle, retenue à la source 15 %, liasse G50 simplifiée + export CSV

### Lot 4 — Paie DZ complète
- Table `rh_paie_clotures`, `rh_paie_params` (taux parafiscaux paramétrables)
- Moteur paie enrichi : accident travail, chômage, formation pro, coût employeur
- Verrouillage mensuel brouillon → validé → clôturé
- Export CNAS colonnes officielles documentées, DAS enrichi
- UI `/rh/paie/cloture`

## Schéma des tables ajoutées

```
exercices_comptables ──< ecritures_comptables ──< lignes_ecriture >── comptes
                              │
                              └── journaux

factures ──< factures_registre
         ──< factures_fiscales_metadata
         ──< registre_tva_ventes

factures_numerotation (serie, exercice, dernier_numero)

declarations_tva | retenues_source | liasse_fiscale_lignes | registre_tva_achats

rh_paie_clotures | rh_paie_params
```

## Flux métier implémentés

```
[Facture brouillon BRO-*]
        │ soumettre
        ▼
[Facture soumise/proforma]
        │ valider
        ├── Numéro légal FAC/AV-AAAA-NNNNN
        ├── Registre factures + metadata SIFEC
        ├── Écriture VE (411 / 707 / 445710)
        └── Ligne registre TVA ventes
        │
        ▼
[Paiement partiel/total] → statut payee_partielle / payee

[Avoir sur facture validée]
        │ valider avoir
        └── Écriture inverse + registre TVA type avoir

[Encaissement confirmé]
        └── Écriture BQ ou CA (512/530 ↔ 411)

[Paie mensuelle]
        brouillon → valider période → clôturer (verrouillage)
```

## Tests

- `electron/services/rh-paie-dz-engine.test.ts` — IRG, CNAS, parafiscales, SMIG
- `electron/services/phase1-conformite.test.ts` — numérotation, équilibre écriture, hash SIFEC
- Correction test SidebarNav « Raqmi System »

Lancer : `npm test`

## Écarts restants vs conformité légale complète (Phase 2)

| Domaine | Reste à faire |
|---------|----------------|
| SIFEC | API DGI production, signature électronique, télédéclaration |
| Facturation | Proforma distinct, envoi email, timbre fiscal avancé |
| Comptabilité | Clôture automatique résultat, amortissements, immobilisations |
| Fiscalité | Liasse G50 complète, G4/G29 détaillés, TVA achats auto depuis module achats |
| Paie | Fichiers CNAS binaires officiels, DADS/U, mutuelle |
| Légal | Fiche police, taxe séjour, Loi 18-07 ANPDP |

## Plan de tests manuels métier

### Comptable
1. Créer facture brouillon → soumettre → valider : vérifier numéro FAC-2026-00001, registre, balance (411, 707, 445710)
2. Saisir encaissement espèces → confirmer : écriture CA visible dans journaux
3. Émettre avoir sur facture → valider : écriture inverse + registre TVA
4. Balance générale sur exercice 2026 avec filtres période
5. Calculer déclaration TVA du mois courant
6. Clôturer exercice (après validation de toutes les écritures brouillon)

### RH / Paie
1. Générer pré-paie mensuelle → valider bulletins → valider période → clôturer
2. Tenter modification après clôture (doit échouer)
3. Export CNAS et DAS — vérifier colonnes parafiscales
4. Vérifier taux parafiscaux dans `rh_paie_params`

## Fichiers principaux

| Fichier | Rôle |
|---------|------|
| `electron/database/migrations/057_phase1_conformite_legale.sql` | Schéma + seed |
| `electron/services/comptabilite.service.ts` | Comptabilité SCF |
| `electron/services/fiscalite-dz.service.ts` | Fiscalité DGI |
| `electron/services/facturation.service.ts` | Facturation conforme |
| `electron/services/rh-paie-cloture.service.ts` | Clôture paie |
| `electron/services/rh-paie-dz-engine.ts` | Moteur paie + parafiscales |
| `electron/ipc/comptabilite.ipc.ts` | IPC comptabilité |
| `electron/ipc/fiscalite-dz.ipc.ts` | IPC fiscalité |
