# Modules prioritaires

Ce fichier organise les 49 modules repris depuis l'ancien depot en lots de transformation pour la nouvelle version .NET.

La priorite ne veut pas dire que les autres modules sont inutiles. Elle indique simplement l'ordre le plus rationnel pour obtenir une version serveur-client fiable.

## P0 - Socle et pilote

| Module | Groupe | Objectif |
|---|---|---|
| Administration & utilisateurs | Socle | Comptes utilisateurs; Rôles et permissions; Périmètres par unité |
| Paramétrage global | Socle | Préférences générales; Interface et thème; Santé du système |
| Unités hôtelières | Socle | Référentiel des unités; Paramètres par établissement; Affectations utilisateurs |
| CA journalier (ERP) | Finance | Saisie quotidienne; Validation unité et DEC; Consolidation du chiffre d’affaires |
| Audit & contrôle interne | Contrôle | Consultation des traces; Contrôles internes; Piste d’audit |
| Tableaux de bord directionnels | Pilotage | Dashboard global; Indicateurs consolidés; Filtres par période et unité |
| Dashboard PDG | Pilotage | Vision groupe; Consolidation des unités; Alertes de direction |
| Cockpit DEC | Pilotage | Pilotage exploitation; Contrôles quotidiens; Actions et alertes |
| Journalisation & traçabilité | Système | Journal d’audit; Traçabilité des opérations; Recherche et export |

## P1 - Core ERP

| Module | Groupe | Objectif |
|---|---|---|
| Clôture journalière & Night Audit | Finance | Clôture journalière; Contrôles Night Audit; Date métier hôtelière |
| Encaissements & trésorerie | Finance | Encaissements; Comptes bancaires; Ordres de paiement |
| Comptabilité SCF | Finance | Plan comptable SCF; Journaux et écritures; Balance |
| Budget & prévisions | Finance | Objectifs; Budgets mensuels; Réalisé et écarts |
| Facturation | Finance | Factures clients; Avoirs; Registre des ventes |
| Créances & recouvrement | Finance | Balance âgée; Relances; Suivi du risque client |
| Clients | Finance | Fichier clients; Coordonnées; Historique commercial |
| Hébergement & occupation | Exploitation | PMS avancé; Réservations et folios; Channel Manager |
| Tarifs & conventions | Exploitation | Plans tarifaires; Grilles; Promotions |
| Workflows & validations | Contrôle | Circuits d’approbation; Procédures de validation; Historique des décisions |
| Rapports automatiques | Pilotage | Rapports configurables; Exports PDF et Excel; Historique des exécutions |
| Sauvegarde & restauration | Système | Sauvegardes locales; Restauration; Politiques de rétention |
| Synchronisation multi-postes | Système | File de synchronisation; État des postes; Résolution des erreurs |

## P2 - Extensions metier

| Module | Groupe | Objectif |
|---|---|---|
| Fiscalité DGI & SIFEC | Finance | TVA ventes et achats; Déclarations fiscales; Retenues à la source |
| Housekeeping & chambres | Exploitation | Planning des équipes; Inspection des chambres; Minibar |
| CRM & expérience client | Exploitation | Vue client 360°; Segmentation; Fidélité |
| Groupes & MICE | Exploitation | Rooming lists; Allotements; Événements et salles |
| Stocks & consommations | Exploitation | Magasins multiples; Lots et péremption; Transferts |
| Cuisine, production & qualité | Exploitation | Fiches techniques; HACCP; Températures |
| Points de vente (POS) | Exploitation | Plan de salle; Tickets et couverts; Partage de note |
| Achats & approvisionnements | Exploitation | Fournisseurs; Demandes d’achat; Consultations et devis |
| Appels d'offres | Exploitation | Dossiers multi-lots; Documents et cahier des charges; Ouverture des plis |
| Maintenance & interventions | Exploitation | Équipements; Ordres de travail; Maintenance préventive |
| Intégrations matérielles | Exploitation | Serrures électroniques; PBX et IPTV; TPE CIB/Edahabia/SATIM |
| Qualité & réclamations clients | Exploitation | Réclamations; Traitement et délais; Analyse des causes |
| Contrats & conventions | Juridique & commercial | Contrats clients; Conventions; Allotements |
| Commercial & partenariats | Juridique & commercial | Prospection; Partenariats; Suivi commercial |
| RH & productivité | Ressources humaines | Collaborateurs; Temps et présence; Pré-paie |
| Pointeuses & badgeuses | Ressources humaines | Pointeuses; Import des pointages; Réconciliation |
| Checklists de contrôle | Contrôle | Modèles de contrôle; Exécution des checklists; Suivi des écarts |
| Journal des anomalies | Contrôle | Déclaration des anomalies; Affectation; Suivi des corrections |
| Décisions & instructions | Contrôle | Instructions de direction; Échéances; Suivi d’exécution |
| Conformité hôtelière | Conformité & légal | Fiches police; Taxe de séjour; Rapports tourisme |
| Protection des données | Conformité & légal | Registre des traitements; Consentements; Demandes de droits |
| Modules légaux | Conformité & légal | Immobilisations; CASNOS; Inventaire légal |
| Veille juridique & réglementaire | Conformité & légal | Répertoire des textes; Suivi de mise en conformité; Rappels d'échéance |
| Alertes & notifications | Pilotage | Notifications internes; Règles d’alerte; Préférences utilisateurs |
| Comparatif inter-unités | Pilotage | Classement des unités; Comparaisons N/N-1; Écarts aux objectifs |
| PortMaster | Spécifique | Bateaux et clients; Emplacements; Contrats |
| Gestion documentaire | Système documentaire | GED; Versions; OCR et signature |

Voir le catalogue complet : `docs/modules-catalog.md`.
