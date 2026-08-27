# Manuel d'utilisation ERP — module par module

Ce dossier documente **chaque module fonctionnel de l'application, écran par écran**, à partir du code réel (`src/pages`, `src/routes/AppRoutes.tsx`, `src/layouts/sidebarModules.ts`, `electron/ipc`, `electron/services`).

Il complète les guides déjà existants dans [`docs/guides-utilisateurs/`](../guides-utilisateurs/README.md), qui sont organisés **par profil utilisateur** (« que dois-je faire aujourd'hui ? »). Ici, l'angle est **par module** (« comment fonctionne cet écran précisément ? ») : chaque fiche détaille les champs, les workflows, les règles métier DZ et les liens avec les autres modules.

## Gabarit d'une fiche module

Chaque fiche `*.md` de ce dossier suit la même structure en 7 sections :

1. **Présentation** — objectif métier du module, périmètre fonctionnel, à qui il s'adresse (renvoi vers le profil concerné dans `docs/guides-utilisateurs/`).
2. **Prérequis & accès** — permissions/rôles nécessaires (fonctions `can...` de `src/shared/permissions.ts`), route(s) d'entrée, modules dont il dépend.
3. **Écrans & champs** — pour chaque écran du module : son rôle, les champs/colonnes principaux, les actions disponibles (boutons, menus), les états/statuts possibles. Basé sur la lecture réelle des composants React (`src/pages/...`) et des types partagés (`src/shared/types/...`).
4. **Workflows standards** — procédures pas-à-pas des cas d'usage nominaux (créer, valider, clôturer, exporter, etc.), y compris les appels IPC concernés (`electron/ipc/*.ipc.ts`) quand ils éclairent le comportement.
5. **Règles métier DZ** — spécificités légales/fiscales algériennes si applicables (TVA, timbre fiscal, SCF, CNAS/CASNOS, obligations réglementaires) ; sinon, indiquer « Aucune règle DZ spécifique à ce module ».
6. **Interconnexions** — impact des actions de ce module sur d'autres modules (ex. une vente POS décrémente les stocks et alimente la recette journalière), avec liens vers les fiches concernées.
7. **Dépannage** — messages d'erreur ou blocages fréquents, causes probables, premiers réflexes ; points de contrôle utiles pour l'audit interne.

Format des fiches : Markdown, titres `##` pour les 7 sections, chemins de fichiers cités entre backticks pour traçabilité (ex. `src/pages/stocks/StocksPage.tsx`).

## Sommaire

### Pilotage & contrôle
| Fiche | Module | Route(s) |
|---|---|---|
| [dashboard-global.md](dashboard-global.md) | Dashboard global | `/dashboard` |
| [dashboard-pdg.md](dashboard-pdg.md) | Dashboard PDG | `/dashboard/pdg` |
| [dec-cockpit.md](dec-cockpit.md) | Cockpit DEC | `/dec/cockpit` |
| [rapports-exports.md](rapports-exports.md) | Rapports & exports | `/rapports` |
| [workflows.md](workflows.md) | Workflows | `/workflows` |
| [checklists.md](checklists.md) | Checklists contrôle interne | `/controle/checklists` |
| [anomalies.md](anomalies.md) | Journal des anomalies | `/anomalies` |
| [reclamations.md](reclamations.md) | Réclamations clients | `/reclamations` |
| [decisions-instructions.md](decisions-instructions.md) | Décisions & instructions | `/decisions` |

### Exploitation
| Fiche | Module | Route(s) |
|---|---|---|
| [hebergement-occupation.md](hebergement-occupation.md) | Hébergement & occupation | `/hebergement` |
| [tarifs-conventions.md](tarifs-conventions.md) | Tarifs & conventions | `/tarifs` |
| [recettes-journalieres.md](recettes-journalieres.md) | CA journalier (ERP) | `/recettes/journalieres`, `/recettes/historique`, `/recettes/validation`, `/recettes/cloture` |
| [clients.md](clients.md) | Clients | `/clients` |
| [facturation.md](facturation.md) | Facturation | `/facturation` |

### Commercial, conformité & documents
| Fiche | Module | Route(s) |
|---|---|---|
| [commercial-partenariats.md](commercial-partenariats.md) | Commercial & partenariats | `/commercial` |
| [conformite-hoteliere.md](conformite-hoteliere.md) | Conformité hôtelière | `/hotel-legal` |
| [conformite-donnees-personnelles.md](conformite-donnees-personnelles.md) | Données personnelles (loi 18-07) | `/conformite/donnees-personnelles/*` |
| [modules-legaux.md](modules-legaux.md) | Modules légaux (immobilisations, CASNOS, inventaire) | `/conformite/modules-legaux/*` |
| [ged.md](ged.md) | Gestion documentaire (GED) | `/ged` |
| [ged-archivage-legal.md](ged-archivage-legal.md) | Archivage légal GED | `/ged/archivage-legal` |

### Finances & comptabilité
| Fiche | Module | Route(s) |
|---|---|---|
| [rapprochements.md](rapprochements.md) | Rapprochements | `/finance/rapprochements` |
| [creances-recouvrement.md](creances-recouvrement.md) | Créances & recouvrement | `/creances` |
| [budget-previsions.md](budget-previsions.md) | Objectifs & saisie mensuelle | `/objectifs`, `/recettes/mensuelles` |
| [encaissements-tresorerie.md](encaissements-tresorerie.md) | Encaissements & trésorerie | `/encaissements/*` |
| [comptabilite-scf.md](comptabilite-scf.md) | Comptabilité SCF | `/comptabilite/*` |
| [fiscalite-dgi.md](fiscalite-dgi.md) | Fiscalité DGI | `/fiscalite/*` |

### Opérations
| Fiche | Module | Route(s) |
|---|---|---|
| [stocks-consommations.md](stocks-consommations.md) | Stocks & consommations | `/stocks` |
| [production-fiches-techniques.md](production-fiches-techniques.md) | Production & fiches techniques | `/cuisine` |
| [pos-restauration.md](pos-restauration.md) | Points de vente (POS) | `/pos` |
| [achats-approvisionnements.md](achats-approvisionnements.md) | Achats & approvisionnements | `/achats` |
| [maintenance.md](maintenance.md) | Maintenance & interventions | `/maintenance` |
| [parking.md](parking.md) | Parking | `/parking` |
| [plage-piscine.md](plage-piscine.md) | Plage & piscine | `/plage` |

### RH & productivité
| Fiche | Module | Route(s) |
|---|---|---|
| [rh-productivite.md](rh-productivite.md) | RH & productivité — hub, organisation, fiches de poste | `/rh`, `/rh/organisation/egt`, `/rh/fiches-poste` |
| [rh-paie-declarations.md](rh-paie-declarations.md) | Paie DZ, bulletins, déclarations, clôture | `/rh/paie/*`, `/rh/paie/cloture` |
| [rh-recrutement-pointeuses.md](rh-recrutement-pointeuses.md) | Recrutement (ATS), pointeuses & badgeuses | `/rh/recrutement*`, `/rh/temps/pointeuse*` |

### PortMaster
| Fiche | Module | Route(s) |
|---|---|---|
| [portmaster.md](portmaster.md) | PortMaster — accueil, référentiel, bateaux, emplacements | `/portmaster`, `/portmaster/dashboard`, `/portmaster/referentiel`, `/portmaster/bateaux`, `/portmaster/emplacements`, `/portmaster/mouvements` |
| [portmaster-facturation.md](portmaster-facturation.md) | PortMaster — contrats, clients, facturation, recouvrement | `/portmaster/contrats`, `/portmaster/clients`, `/portmaster/factures`, `/portmaster/tarifs`, `/portmaster/validations`, `/portmaster/recouvrement` |

### Administration & système
| Fiche | Module | Route(s) |
|---|---|---|
| [administration-utilisateurs.md](administration-utilisateurs.md) | Utilisateurs, hôtels/unités, rôles, rubriques | `/admin/users`, `/admin/hotels`, `/admin/roles`, `/admin/rubriques` |
| [parametrage-global.md](parametrage-global.md) | Paramètres, interface & thème, sécurité & accès | `/settings`, `/settings/interface`, `/settings/securite` |
| [synchronisation-multi-postes.md](synchronisation-multi-postes.md) | Synchronisation multi-postes | `/system/sync` |
| [journalisation-tracabilite.md](journalisation-tracabilite.md) | Journal d'audit & traçabilité | `/audit/logs` |
| [sauvegarde-restauration.md](sauvegarde-restauration.md) | Sauvegarde, base de données, santé système | `/settings/backup`, `/settings/database`, `/settings/system-health` |
| [alertes-notifications.md](alertes-notifications.md) | Alertes & notifications | `/settings/notifications` |
| [modules-activation.md](modules-activation.md) | Activation des modules | `/settings/modules` |

## Règle de mise à jour

Toute évolution fonctionnelle d'un module (nouvel écran, nouveau champ, nouveau workflow) doit être répercutée dans la fiche correspondante, en cohérence avec [`docs/guides-utilisateurs/README.md`](../guides-utilisateurs/README.md).
