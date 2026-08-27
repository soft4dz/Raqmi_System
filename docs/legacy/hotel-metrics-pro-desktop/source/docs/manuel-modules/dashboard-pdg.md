# Dashboard PDG

## Présentation

Vue consolidée multi-hôtels réservée à la direction générale : une série de KPI de synthèse (finance, trésorerie, contrôle, qualité) accompagnée d'un tableau de performance par hôtel, avec deux exports dédiés (CSV rapide et rapport mensuel Excel pour le conseil d'administration).

Composant : `src/pages/dashboard/DashboardPdgPage.tsx`. Service backend : `electron/services/dashboard-pdg.service.ts`.

Public cible : PDG / Direction générale — voir `docs/guides-utilisateurs/02-pdg.md`.

## Prérequis & accès

- Route : `/dashboard/pdg` (« Dashboard PDG » du module « Pilotage »).
- Contrôle serveur strict : `getDashboardPdg` lève une erreur (« Dashboard PDG réservé à la direction. ») si `isGlobalAdminRole(actor.roleCode)` est faux — c'est-à-dire si le rôle n'est ni `SUPERADMIN` ni `ADMIN_DEC`. Contrairement au Dashboard global, il n'y a **pas** d'exception pour `PDG`, `COMPTABILITE` ou `AUDIT_INTERNE` au niveau service : seuls les rôles admin globaux passent réellement.
- Le menu latéral ne masque pas explicitement cette entrée par rôle (`sidebarModules.ts` ne fixe pas de `visible` dessus) : elle est visible pour tous, mais l'appel IPC échoue côté serveur si le rôle n'est pas admin global.
- Export mensuel réservé de la même façon (`exportPdgReportMensuelXlsx` revérifie `isGlobalAdminRole`).

## Écrans & champs

Écran unique :

1. **En-tête** : titre « Tableau de bord PDG », sous-titre « Vue consolidée multi-hôtels », boutons « Rapport mensuel CA » (Excel) et « Export CSV ».
2. **Grille de KPI** (`PdgKpi[]`), chaque carte affichant `domaine`, `libelle` et valeur formatée selon `unite` (`%`, `DZD` ou nombre brut), avec une bordure colorée selon `level` (`normal`/`warning`/`critical`) :
   - `CA_JOUR` — CA du jour (domaine finance).
   - `CA_MOIS` — CA du mois (domaine finance).
   - `OBJECTIF_REALISE` — taux objectif vs réalisé, `critical` si < 70 %, `warning` si < 90 %.
   - `CREANCES_OUVERTES` — total des créances ouvertes/partielles, `critical` si > 1 000 000 DA, `warning` si > 300 000 DA.
   - `ENCAISSEMENTS_JOUR` — encaissements confirmés du jour (domaine trésorerie).
   - `ANOMALIES_OUVERTES` — nombre d'anomalies ouvertes/en cours, `critical` si > 10, `warning` si > 0.
   - `RECLAMATIONS_OUVERTES` — réclamations non clôturées/résolues, `warning` si > 5.
3. **Tableau « Performance par hôtel »** : colonnes Hôtel, CA mois, Occupation (champ toujours à `0` — non calculé côté service), Créances.

## Workflows standards

1. **Consultation** : chargement automatique via React Query (`queryKey: ['dashboard-pdg']`) → `ipcClient.dashboardPdg.get()` (canal `dashboard:pdg:get`).
2. **Export CSV** : bouton « Export CSV » → `ipcClient.dashboardPdg.exportCsv()` (canal `dashboard:pdg:exportCsv`) ; génère un CSV en mémoire (KPI + synthèse par hôtel) et journalise l'action (`writeAuditLog`, module `dashboard_pdg`). Le CSV n'est pas automatiquement écrit sur disque par ce bouton — la génération est retournée en chaîne (le point de sortie fichier dépend de l'implémentation `export` associée côté renderer).
3. **Rapport mensuel Excel** : bouton « Rapport mensuel CA » → `ipcClient.dashboardPdg.exportMensuel()` (canal `dashboard:pdg:exportMensuel`), ouvre une boîte de dialogue « Enregistrer sous » (`Electron.dialog.showSaveDialog`) puis génère un classeur à 3 feuilles : *Synthèse CA* (période, CA mensuel consolidé, objectif, taux de réalisation, KPI), *Par unité* (CA mois et créances par hôtel) et *CA journalier* (détail jour par jour). Annulable par l'utilisateur (« Export annulé. »).

## Règles métier DZ

Aucune règle DZ spécifique à ce module — le rapport agrège des montants en DA/DZD déjà calculés par les modules Recettes, Créances et Encaissements, qui portent eux les règles fiscales.

## Interconnexions

- **CA journalier (ERP)** et **Objectifs & saisie mensuelle** : source du CA et du taux de réalisation objectif.
- **Créances & recouvrement** (`docs/manuel-modules/creances-recouvrement.md`) : source de `CREANCES_OUVERTES` (table `global_creances`, statuts `ouverte`/`partielle`).
- **Encaissements & trésorerie** : source de `ENCAISSEMENTS_JOUR`.
- **Journal des anomalies** (`docs/manuel-modules/anomalies.md`) : source de `ANOMALIES_OUVERTES`.
- **Réclamations clients** (`docs/manuel-modules/reclamations.md`) : source de `RECLAMATIONS_OUVERTES`.
- Complémentaire au **Dashboard global** (`docs/manuel-modules/dashboard-global.md`), qui offre une vue plus détaillée et filtrable mais avec un contrôle d'accès moins strict.

## Dépannage

- **Erreur « Dashboard PDG réservé à la direction. »** : le compte connecté n'a pas le rôle `SUPERADMIN` ou `ADMIN_DEC` — un rôle `PDG` seul ne suffit pas au niveau service, malgré son nom.
- **Colonne « Occupation » toujours à 0** : comportement actuel du service (`occupation: 0` codé en dur) — ne pas interpréter comme une anomalie de données.
- **Export mensuel « annulé »** : l'utilisateur a fermé la boîte de dialogue d'enregistrement sans choisir de fichier.
- **KPI « Créances ouvertes » élevé** : vérifier les factures impayées dans `docs/manuel-modules/creances-recouvrement.md` avant d'escalader.
