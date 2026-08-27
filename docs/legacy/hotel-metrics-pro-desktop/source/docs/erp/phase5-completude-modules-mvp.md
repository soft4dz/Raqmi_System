# Phase 5 — Complétude modules MVP & chaîne exploitation

**Application :** Raqmi System v0.8.0
**Prérequis :** Phases 1–3 validées (`npm run validate:certification` OK)  
**Objectif :** Passer de ~72 % à ~85 % de complétude ERP exploitation en combinant les 10 lacunes P1 identifiées dans l’audit module par module.

**Référence audit :** matrice complétude (analyse LLM Council / codebase juillet 2026)

---

## Prompt agent Phase 5 (copier-coller)

```markdown
# Mission : Phase 5 — Complétude modules MVP (Raqmi System v0.8.0)

## ⚠️ PRÉREQUIS — NE PAS DÉMARRER SANS VALIDATION

Checklist bloquante :

- [ ] `npm test` passe (212+ tests)
- [ ] `npm run validate:certification` passe
- [ ] Phases 1–3 documentées dans `docs/erp/phase1-conformite-legale.md`, `phase2-*`, `phase3-certification.md`
- [ ] Modules comptabilité, fiscalité, workflow, clôture, créances, cockpit DEC déjà opérationnels

**Si échec → STOP. Rapport NO-GO sans implémentation.**

---

## Contexte

ERP hôtelier Algérie (Electron + React + SQLite offline-first).
Les modules finance/RH/pilotage sont matures (~85–90 %).
Les modules **exploitation satellite** sont en MVP (~35–50 %) :

| Module | Maturité actuelle | Problème principal |
|--------|-------------------|-------------------|
| Achats | ~40 % | Pas de réception livraison → stock |
| Stocks | ~45 % | Pas de lien achats, pas d'inventaire UI |
| Maintenance | ~50 % | Registre équipements sans UI |
| Parking / Plage | ~50 % | Services config OK, **aucun écran paramétrage** |
| Commercial | ~35 % | Onglet partenaires stub, backend `createPartenaire` existe |
| Alertes | ~30 % | Prefs locales `useUiStore` seulement, pas de backend |
| Décisions | ~55 % | Destinataires supportés service, absents UI |
| Contrats hôtel | ~55 % | PortMaster OK, pas de contrats hôtel génériques |
| Créances | ~72 % | Pas de génération/relance auto systématique |
| Hébergement | ~75 % | Pas de folio client, lien police/taxe séjour partiel |

**Objectif Phase 5 :** implémenter les **10 actions P1** ci-dessous. Ne pas refaire Phases 1–4.

---

## Contraintes impératives

- Architecture : `UI React → preload electronAPI → IPC handler → service métier → SQLite → audit`
- Validation IPC via `electron/ipc/validation.ts` sur tous nouveaux handlers
- Réutiliser patterns existants (TanStack Query, `notify`, `unwrapIpc`, composants UI shadcn)
- Répondre en **français**
- Ne pas créer de commits sauf demande explicite
- Minimiser la portée : MVP fonctionnel par lot, pas de sur-ingénierie
- Mettre à jour `src/modules/moduleCatalog.ts` et `docs/erp/CERTIFICATION_ERP_ALGERIE.md` en fin de mission

---

## Lot 1 — Chaîne achats → réception → stocks → TVA achats (P1 #1)

**Fichiers existants :**
- `electron/services/achats.service.ts` (5 fonctions : list/create/valider, pas de livraison)
- `electron/services/stocks.service.ts`
- `electron/services/fiscalite-avancee.service.ts` (registre TVA achats)

### Backend

Migration `062_phase5_achats_stocks_chain.sql` si nécessaire :
- Colonne `qte_recue` sur lignes bon de commande (si absente)
- Statuts BC : `brouillon → valide → envoye → livre → annule`

Ajouter dans `achats.service.ts` :
- `envoyerBon(actorId, id)` — statut envoye
- `livrerBon(actorId, id, input?: { lignes?: { ligneId, qteRecue }[] })` :
  - Met à jour qte_recue par ligne
  - Statut `livre` si toutes lignes reçues
  - **Pour chaque ligne reçue** : appeler `stocks.createMouvement({ type: 'entree', motif: 'Réception BC {numero}' })`
  - Si produit lié (`produit_id` sur ligne ou mapping désignation) — sinon créer mapping optionnel
  - Appeler `fiscalite-avancee` pour alimenter `registre_tva_achats` (HT, TVA, fournisseur, ref BC)
  - Audit log

IPC : `achats:envoyerBon`, `achats:livrerBon` + validation Zod

### UI (`AchatsPage.tsx`)

- Boutons **Envoyer** / **Réceptionner** sur bon validé
- Modal réception : quantités reçues par ligne
- Onglet ou section **Fournisseurs** : CRUD (`createFournisseur` déjà en service, exposer formulaire)
- Lien « Voir mouvement stock » post-réception

### Tests

- `electron/services/phase5-achats-stocks.test.ts` : livraison partielle, entrée stock, ligne TVA achats

---

## Lot 2 — UI paramétrage Parking + Plage (P1 #2)

**Services existants avec config non exposée :**
- `electron/services/parking.service.ts` — `getConfig`, `saveConfig`
- `electron/services/plage.service.ts` — `getPlageConfig`, `savePlageConfig`

### UI

Enrichir `ParkingPage.tsx` :
- Onglet **Paramétrage** : capacité max, tarif/heure, tarif journée, gratuité 1ʳᵉ heure (selon champs service)
- Bouton Enregistrer → `ipcClient.parking.saveConfig`

Enrichir `PlagePage.tsx` :
- Onglet **Paramétrage** : capacité, tarif adulte/enfant, horaires ouverture
- Bouton Enregistrer → `ipcClient.plage.saveConfig`

### Tests

- Test service config round-trip (si pas déjà couvert)

---

## Lot 3 — Commercial : CRUD partenaires (P1 #3)

**Existant :**
- `CommercialPage.tsx` L116-120 : stub « disponible prochainement »
- `commercial.service.ts` : `createPartenaire`, `listPartenaires`, etc.

### UI

Remplacer stub onglet **Partenaires** :
- Liste partenaires (DataTable ou cartes)
- Formulaire création/édition : raison sociale, type (agence/groupe/entreprise), contact, email, téléphone, commission %, notes
- Lier opportunité ↔ partenaire (select sur formulaire opportunité)

### Tests

- CRUD partenaire + lien opportunité

---

## Lot 4 — Moteur alertes & notifications backend (P1 #4)

**Problème :** `NotificationsPage.tsx` → prefs locales `useUiStore` uniquement.

### Backend

Migration `063_phase5_notifications.sql` :
```sql
-- notifications (id, user_id, type, titre, message, lien, lu, created_at)
-- notification_rules (code, module, condition, actif)
-- notification_deliveries (notification_id, canal, statut)
```

Service `electron/services/notifications.service.ts` :
- `createNotification(userId, payload)`
- `listNotifications(userId, unreadOnly?)`
- `markRead(id)`, `markAllRead(userId)`
- `getRules()`, `updateRule(code, actif)`
- **Générateurs automatiques** (appelés depuis services existants) :
  - Facture échue J+30 → notif comptable
  - Clôture unité manquante après 09h30 → notif DEC (compléter scheduler Phase 4 si absent)
  - Stock sous seuil → notif responsable unité
  - Workflow en attente → notif validateur
  - Sauvegarde > 24h → notif admin

IPC : `notifications:*` + validation

### UI

- Refactor `NotificationsPage.tsx` : lire/écrire via IPC (garder prefs UI locales en complément)
- **Cloche navbar** : badge non-lus, dropdown 10 dernières, lien « Tout voir »
- Conserver toggles par type (DEC, factures, stocks, etc.) → `notification_rules`

### Tests

- `phase5-notifications.test.ts`

---

## Lot 5 — Maintenance : registre équipements (P1 #5)

**Existant :** `maintenance.service.ts` — `listEquipements`, `createEquipement` sans UI.

### UI (`MaintenancePage.tsx`)

- Onglet **Équipements** :
  - Liste : nom, catégorie, emplacement, statut, date acquisition
  - Formulaire création/édition
  - Lien « Créer intervention » depuis fiche équipement
- Enrichir formulaire intervention : select équipement, coût estimé/réel, rapport clôture

### Tests

- CRUD équipement + intervention liée

---

## Lot 6 — Décisions : destinataires à la création (P1 #6)

**Existant :** `decisions.service.ts` supporte `destinataireIds`, UI création ne les expose pas.

### UI (`DecisionsPage.tsx`)

- Multi-select utilisateurs/rôles destinataires à la création
- Indicateur lu/non-lu par destinataire sur fiche décision
- Filtre « Mes décisions non lues »

### Backend

Vérifier `listDecisionsForUser(userId)` filtre par destinataire + statut lu.

---

## Lot 7 — Créances : génération & relances auto (P1 #7)

**Existant :** `creances.service.ts`, `/creances`, génération manuelle depuis facture.

### Backend

Enrichir `facturation.service.ts` à la validation facture :
- Si `montantRestant > 0` après échéance ou à validation → `creances.createFromFacture()` auto

Enrichir `creances.service.ts` :
- `runRelancesAutomatiques(actorId)` :
  - Balance âgée : 30j → relance téléphone, 60j → email, 90j → mise en demeure (enregistrement + notification)
- Planificateur : appeler au démarrage + daily (comme alertes)

### UI

- Toggle « Relances automatiques » dans paramètres ou page créances
- Historique relances visible (déjà `global_creance_relances`)

### Tests

- Facture impayée → créance auto
- Relance selon ancienneté

---

## Lot 8 — Hébergement : folio client + liens légaux (P1 #8)

**Existant :** `hebergement*.service.ts`, réservations, check-in/out, `hotel-legal.service.ts`.

### Backend

- Table `hebergement_folios` (reservation_id, lignes charge, total, statut)
- `createFolioFromReservation`, `addFolioLine`, `closeFolioToFacture` (lien facturation)
- Au **check-in** : préremplir fiche police depuis réservation/client
- Calcul taxe séjour auto à check-out (appeler `hotel-legal.service.ts`)

### UI

- Onglet **Folio** sur fiche réservation : consommations, nuitées, extras
- Bouton « Facturer folio » → facture brouillon
- Bouton « Fiche police » préremplie depuis check-in

### Tests

- Check-in → fiche police
- Check-out → taxe séjour + folio clos

---

## Lot 9 — Contrats hôteliers génériques (P1 #9)

**Problème :** contrats = PortMaster seulement ; catalogue statut `socle`.

### Backend

Migration si nécessaire :
- `contrats_hotel` (hotel_id, client_id, type, date_debut, date_fin, montant, statut, document_ged_id)
- Types : convention entreprise, allotement, MICE, prestation restauration

Service `electron/services/contrats-hotel.service.ts` :
- CRUD + alertes échéance J-30
- Lien facturation (tarif convention appliqué)

### UI

- Route `/contrats` ou enrichir `/commercial` onglet Contrats
- Liste, création, détail, alertes expiration
- Lien GED pour pièce contractuelle

IPC : `contratsHotel:*`

---

## Lot 10 — Workflow sur validation facture & bon d'achat (P1 #10)

**Existant :** `workflow.service.ts`, branché partiellement Phase 2/4.

### Backend

Dans `facturation.service.ts` → `validerFacture` :
- Si montant > seuil paramétrable OU client entreprise → exiger `workflow_instance` approuvée avant validation finale
- Sinon validation directe (comportement actuel)

Dans `achats.service.ts` → `validerBon` :
- Créer workflow `achats_validation` si montant TTC > seuil

### UI

- Badge « En attente validation » sur facture/BC
- Lien vers `/workflows` depuis fiche
- Historique workflow dans `FactureDetailFacturationPage` et `AchatsPage`

Réutiliser composant `<WorkflowHistoryPanel entityType entityId />` (créer si absent Phase 4).

### Tests

- Facture > seuil → workflow → approve → validée
- Refus workflow → facture reste soumise

---

## Lot 11 — Synchronisation catalogue & documentation

### `src/modules/moduleCatalog.ts`

- `creances-recouvrement` → `operationnel`, `existingRoute: '/creances'`
- `contrats-conventions` → `operationnel`, `existingRoute: '/contrats'` (nouvelle route)
- Ajouter modules manquants au catalogue :
  - `comptabilite-scf`, `fiscalite-dgi`, `workflows`, `cloture-journaliere`, `cockpit-dec`, `dashboard-pdg`, `conformite-donnees`, `modules-legaux`

### Documentation

Créer `docs/erp/phase5-completude-livraison.md` :
- Matrice avant/après maturité par module
- Fichiers modifiés
- Tests ajoutés

Mettre à jour section complétude dans `docs/erp/CERTIFICATION_ERP_ALGERIE.md`.

---

## Ordre d'exécution recommandé

1. Checklist prérequis (STOP si incomplet)
2. **Lot 4** Notifications (transversal — utile pour les autres lots)
3. **Lot 1** Achats → stocks → TVA (impact supply chain)
4. **Lot 10** Workflow facture/achats
5. **Lot 7** Créances auto
6. **Lot 2** Parking/Plage config
7. **Lot 3** Commercial partenaires
8. **Lot 5** Maintenance équipements
9. **Lot 6** Décisions destinataires
10. **Lot 8** Folio hébergement
11. **Lot 9** Contrats hôtel
12. **Lot 11** Catalogue + doc

---

## Définition of Done Phase 5

- [ ] Réception BC → entrée stock + ligne TVA achats fonctionnelle
- [ ] UI config Parking + Plage opérationnelle
- [ ] CRUD partenaires commercial (plus de stub)
- [ ] Moteur notifications backend + cloche navbar
- [ ] Registre équipements maintenance en UI
- [ ] Destinataires décisions à la création + lu/non-lu
- [ ] Créance auto + relances programmées
- [ ] Folio hébergement + fiche police check-in + taxe séjour check-out
- [ ] Contrats hôtel CRUD + alertes échéance
- [ ] Workflow seuil sur validation facture et BC
- [ ] `moduleCatalog.ts` synchronisé
- [ ] ≥ 15 tests unitaires nouveaux (`phase5-*.test.ts`)
- [ ] `npm test` + `npm run validate:certification` passent
- [ ] `docs/erp/phase5-completude-livraison.md` rédigé

---

## Hors scope Phase 5

- SIFEC production, homologation DGI
- Channel manager / OTA hébergement
- Refactoring IPC global / NestJS
- i18n, mobile, Tauri
- GMAO maintenance avancée (planning préventif = P2)
- Import relevé bancaire (P2 trésorerie)

---

## Format restitution agent

1. Résultat checklist prérequis
2. Table maturité avant/après par module (10 cibles P1)
3. Fichiers créés/modifiés par lot
4. Nouvelles migrations SQL
5. Scénarios de test manuel (achats→stock, folio→facture, relance créance)
6. Écarts restants P2 pour Phase 6 éventuelle
```

---

## Maturité cible post-Phase 5

| Module | Avant | Cible |
|--------|-------|-------|
| Achats | 40 % | **75 %** |
| Stocks | 45 % | **70 %** |
| Maintenance | 50 % | **70 %** |
| Parking / Plage | 50 % | **70 %** |
| Commercial | 35 % | **65 %** |
| Alertes | 30 % | **70 %** |
| Décisions | 55 % | **75 %** |
| Créances | 72 % | **85 %** |
| Hébergement | 75 % | **85 %** |
| Contrats | 55 % | **70 %** |
| **Global ERP exploitation** | **~72 %** | **~85 %** |

---

## Liens

- [CERTIFICATION_ERP_ALGERIE.md](./CERTIFICATION_ERP_ALGERIE.md)
- [phase4-pilote-egt-homologation.md](./phase4-pilote-egt-homologation.md)
- Phase 6 suggérée : complétude P2 (relevé bancaire, GMAO, channel manager, rapports email cron)
