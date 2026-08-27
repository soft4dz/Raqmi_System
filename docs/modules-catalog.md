# Catalogue des modules

Ce catalogue reprend la structure fonctionnelle de l'ancien depot `soft4dz/Hotel_Metrics_Pro_Desktop`, fichier source `src/modules/moduleCatalog.ts`.

- Source : soft4dz/Hotel_Metrics_Pro_Desktop
- Branche source : main
- Commit source : c3a5795864f44363464a41ece95c169f4ca04bcf
- Total modules : 49
- Statuts source : operationnel=43, socle=6

Important : le statut ci-dessous correspond a l'ancien projet Electron/SQLite. Dans le nouveau depot .NET, ces modules servent de reference de reprise, pas de preuve qu'ils sont deja reimplementes.

## Repartition par groupe

| Groupe | Nombre |
|---|---:|
| Socle | 3 |
| Finance | 9 |
| Exploitation | 13 |
| Juridique & commercial | 2 |
| Ressources humaines | 2 |
| Contrôle | 5 |
| Conformité & légal | 4 |
| Pilotage | 6 |
| Spécifique | 1 |
| Système documentaire | 1 |
| Système | 3 |

## Liste complete des modules

| Ordre | Groupe | Module | Statut source | Priorite nouvelle version | Route source | Capacites principales |
|---:|---|---|---|---|---|---|
| 1 | Socle | Administration & utilisateurs | operationnel | P0 - Socle/pilote | /admin/users | Comptes utilisateurs; Rôles et permissions; Périmètres par unité |
| 2 | Socle | Paramétrage global | operationnel | P0 - Socle/pilote | /settings | Préférences générales; Interface et thème; Santé du système |
| 3 | Socle | Unités hôtelières | operationnel | P0 - Socle/pilote | /admin/hotels | Référentiel des unités; Paramètres par établissement; Affectations utilisateurs |
| 4 | Finance | CA journalier (ERP) | operationnel | P0 - Socle/pilote | /recettes/journalieres | Saisie quotidienne; Validation unité et DEC; Consolidation du chiffre d’affaires |
| 4.5 | Finance | Clôture journalière & Night Audit | socle | P1 - Core ERP | /recettes/cloture | Clôture journalière; Contrôles Night Audit; Date métier hôtelière |
| 5 | Finance | Encaissements & trésorerie | operationnel | P1 - Core ERP | /encaissements | Encaissements; Comptes bancaires; Ordres de paiement; Prévisions de trésorerie; Rapprochement bancaire |
| 5.2 | Finance | Comptabilité SCF | operationnel | P1 - Core ERP | /comptabilite | Plan comptable SCF; Journaux et écritures; Balance; Exercices; Lettrage |
| 5.4 | Finance | Fiscalité DGI & SIFEC | socle | P2 - Extension | /fiscalite | TVA ventes et achats; Déclarations fiscales; Retenues à la source; Liasse fiscale; Connecteur SIFEC |
| 6 | Finance | Budget & prévisions | operationnel | P1 - Core ERP | /objectifs | Objectifs; Budgets mensuels; Réalisé et écarts |
| 8 | Finance | Facturation | operationnel | P1 - Core ERP | /facturation | Factures clients; Avoirs; Registre des ventes |
| 9 | Finance | Créances & recouvrement | operationnel | P1 - Core ERP | /creances | Balance âgée; Relances; Suivi du risque client |
| 9.2 | Finance | Clients | operationnel | P1 - Core ERP | /clients | Fichier clients; Coordonnées; Historique commercial |
| 10 | Exploitation | Hébergement & occupation | operationnel | P1 - Core ERP | /hebergement | PMS avancé; Réservations et folios; Channel Manager; Booking Engine; Yield management |
| 10.2 | Exploitation | Housekeeping & chambres | operationnel | P2 - Extension | /housekeeping | Planning des équipes; Inspection des chambres; Minibar; Lingerie; Objets trouvés |
| 10.4 | Exploitation | CRM & expérience client | socle | P2 - Extension | /crm | Vue client 360°; Segmentation; Fidélité; Campagnes; NPS; Portail et pré-check-in |
| 10.6 | Exploitation | Groupes & MICE | socle | P2 - Extension | /mice | Rooming lists; Allotements; Événements et salles; Devis; BEO; Facturation événementielle |
| 11 | Exploitation | Stocks & consommations | operationnel | P2 - Extension | /stocks | Magasins multiples; Lots et péremption; Transferts; Inventaires physiques; Codes-barres; Valorisation |
| 11.5 | Exploitation | Cuisine, production & qualité | socle | P2 - Extension | /cuisine | Fiches techniques; HACCP; Températures; Allergènes; Gaspillage; Menu engineering; Traçabilité alimentaire |
| 11.6 | Exploitation | Points de vente (POS) | operationnel | P2 - Extension | /pos | Plan de salle; Tickets et couverts; Partage de note; Multi-paiement; Transfert au folio; KDS |
| 12 | Exploitation | Achats & approvisionnements | operationnel | P2 - Extension | /achats | Fournisseurs; Demandes d’achat; Consultations et devis; Bons de commande; Réceptions; Factures fournisseurs |
| 12.5 | Exploitation | Appels d'offres | operationnel | P2 - Extension | /appels-offres | Dossiers multi-lots; Documents et cahier des charges; Ouverture des plis; Grille d'évaluation; Attribution |
| 13 | Exploitation | Maintenance & interventions | operationnel | P2 - Extension | /maintenance | Équipements; Ordres de travail; Maintenance préventive; SLA; Pièces détachées; Garanties et contrats |
| 13.5 | Exploitation | Intégrations matérielles | socle | P2 - Extension | /integrations-materielles | Serrures électroniques; PBX et IPTV; TPE CIB/Edahabia/SATIM; Scanners; Imprimantes fiscales |
| 14.5 | Exploitation | Tarifs & conventions | operationnel | P1 - Core ERP | /tarifs | Plans tarifaires; Grilles; Promotions; Conventions clients; Yield management |
| 18 | Exploitation | Qualité & réclamations clients | operationnel | P2 - Extension | /reclamations | Réclamations; Traitement et délais; Analyse des causes |
| 20 | Juridique & commercial | Contrats & conventions | operationnel | P2 - Extension | /contrats | Contrats clients; Conventions; Allotements; Échéances |
| 20.2 | Juridique & commercial | Commercial & partenariats | operationnel | P2 - Extension | /commercial | Prospection; Partenariats; Suivi commercial |
| 21 | Ressources humaines | RH & productivité | operationnel | P2 - Extension | /rh | Collaborateurs; Temps et présence; Pré-paie; Talents; Formation; GPEC |
| 21.2 | Ressources humaines | Pointeuses & badgeuses | operationnel | P2 - Extension | /rh/temps/pointeuse | Pointeuses; Import des pointages; Réconciliation |
| 22 | Contrôle | Audit & contrôle interne | operationnel | P0 - Socle/pilote | /audit/logs | Consultation des traces; Contrôles internes; Piste d’audit |
| 22.2 | Contrôle | Workflows & validations | operationnel | P1 - Core ERP | /workflows | Circuits d’approbation; Procédures de validation; Historique des décisions |
| 22.4 | Contrôle | Checklists de contrôle | operationnel | P2 - Extension | /controle/checklists | Modèles de contrôle; Exécution des checklists; Suivi des écarts |
| 22.6 | Contrôle | Journal des anomalies | operationnel | P2 - Extension | /anomalies | Déclaration des anomalies; Affectation; Suivi des corrections |
| 22.8 | Contrôle | Décisions & instructions | operationnel | P2 - Extension | /decisions | Instructions de direction; Échéances; Suivi d’exécution |
| 23 | Conformité & légal | Conformité hôtelière | operationnel | P2 - Extension | /hotel-legal | Fiches police; Taxe de séjour; Rapports tourisme |
| 23.2 | Conformité & légal | Protection des données | operationnel | P2 - Extension | /conformite/donnees-personnelles | Registre des traitements; Consentements; Demandes de droits; Incidents; Conservation |
| 23.4 | Conformité & légal | Modules légaux | operationnel | P2 - Extension | /conformite/modules-legaux | Immobilisations; CASNOS; Inventaire légal |
| 23.6 | Conformité & légal | Veille juridique & réglementaire | operationnel | P2 - Extension | /veille-reglementaire | Répertoire des textes; Suivi de mise en conformité; Rappels d'échéance; Documents attachés |
| 24 | Pilotage | Tableaux de bord directionnels | operationnel | P0 - Socle/pilote | /dashboard | Dashboard global; Indicateurs consolidés; Filtres par période et unité |
| 24.2 | Pilotage | Dashboard PDG | operationnel | P0 - Socle/pilote | /dashboard/pdg | Vision groupe; Consolidation des unités; Alertes de direction |
| 24.4 | Pilotage | Cockpit DEC | operationnel | P0 - Socle/pilote | /dec/cockpit | Pilotage exploitation; Contrôles quotidiens; Actions et alertes |
| 25 | Pilotage | Rapports automatiques | operationnel | P1 - Core ERP | /rapports | Rapports configurables; Exports PDF et Excel; Historique des exécutions |
| 25.2 | Pilotage | Alertes & notifications | operationnel | P2 - Extension | /settings/notifications | Notifications internes; Règles d’alerte; Préférences utilisateurs |
| 25.4 | Pilotage | Comparatif inter-unités | operationnel | P2 - Extension | /dashboard | Classement des unités; Comparaisons N/N-1; Écarts aux objectifs |
| 26 | Spécifique | PortMaster | operationnel | P2 - Extension | /portmaster | Bateaux et clients; Emplacements; Contrats; Mouvements; Facturation et recouvrement |
| 27 | Système documentaire | Gestion documentaire | operationnel | P2 - Extension | /ged | GED; Versions; OCR et signature; Archivage légal |
| 28 | Système | Sauvegarde & restauration | operationnel | P1 - Core ERP | /settings/backup | Sauvegardes locales; Restauration; Politiques de rétention |
| 29 | Système | Synchronisation multi-postes | operationnel | P1 - Core ERP | /system/sync | File de synchronisation; État des postes; Résolution des erreurs |
| 30 | Système | Journalisation & traçabilité | operationnel | P0 - Socle/pilote | /audit/logs | Journal d’audit; Traçabilité des opérations; Recherche et export |

## Lecture recommandee

- `P0 - Socle/pilote` : a developper en premier pour obtenir une version serveur-client exploitable.
- `P1 - Core ERP` : a implementer apres validation du socle securite, des unites et des recettes.
- `P2 - Extension` : a reprendre progressivement apres le pilote metier.

## Suites metier cible

La navigation historique etait organisee autour de grandes suites : Pilotage, Exploitation, Finance, Operations, Controle interne, Qualite, Commercial/GED, RH, PortMaster, Administration et Systeme. La nouvelle version .NET doit garder cette logique, mais avec des autorisations serveur et une base PostgreSQL centrale.
