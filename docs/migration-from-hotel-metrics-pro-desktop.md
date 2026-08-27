# Reprise depuis Hotel Metrics Pro Desktop

Ce document definit comment utiliser l'ancien depot comme base fonctionnelle pour construire la nouvelle version C#/.NET de Raqmi System.

- Depot source : soft4dz/Hotel_Metrics_Pro_Desktop
- Commit source analyse : c3a5795864f44363464a41ece95c169f4ca04bcf
- Depot cible : soft4dz/Raqmi_System
- Cible technique : ASP.NET Core, PostgreSQL, WPF

## Principe

On ne reprend pas l'ancien code tel quel. On reprend la connaissance metier : modules, workflows, roles, ecrans, regles de controle, documentation et scenarios de test.

## A reprendre

- Catalogue des 49 modules.
- Manuels module par module.
- Guides par profil utilisateur.
- Regles metier hotelieres, finance, RH, controle, PortMaster et conformite.
- Design system Raqmi System.
- Roadmap ERP et axes d'amelioration.

## A ne pas reprendre directement

- Ancienne architecture Electron/IPC comme architecture cible.
- SQLite comme base centrale.
- Identifiants admin par defaut.
- Secrets, cles API ou mecanismes de licence faibles.
- Simulations presentees comme integrations certifiees.

## Ordre de migration recommande

1. Socle securite : utilisateurs, roles, permissions, audit, JWT, PostgreSQL.
2. Socle organisation : unites hotelieres, periodes, parametres generaux.
3. Pilote metier : recettes journalieres, validation, dashboard direction.
4. Finance core : encaissements, facturation, creances, rapprochements.
5. Exploitation : PMS, tarifs, housekeeping, POS, stocks, achats.
6. Controle et conformite : workflows, anomalies, checklists, audit, DGI/SIFEC.
7. Modules avances : RH, paie, PortMaster, GED, integrations materielles.

## Regle de transformation

Chaque module repris doit produire au minimum :

- entites domaine;
- DTO et contrats applicatifs;
- schema PostgreSQL ou migration EF;
- endpoints API securises;
- permissions serveur;
- audit des actions sensibles;
- ecran client WPF;
- tests unitaires et tests d'integration.
