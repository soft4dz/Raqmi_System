# Phase 9 — Stabilisation production

Ce document fixe les corrections prioritaires avant un déploiement réel de Raqmi System.

## Objectif

Passer d'une application riche en modules à une application exploitable quotidiennement par les directions, les contrôleurs et les unités, avec moins de risques de panne, de données incohérentes ou de pertes d'information.

## Corrections intégrées dans cette branche

- Ajout d'un workflow CI GitHub Actions : installation, rebuild natif, tests et build.
- Ajout d'un composant `ModuleErrorBoundary` pour éviter qu'une erreur d'un module bloque toute l'application.
- Ajout d'une couche de validation IPC réutilisable dans `electron/ipc/validation.ts`.
- Ajout du code d'erreur `VALIDATION_ERROR` dans les réponses IPC.
- Ajout de tests unitaires pour les helpers de validation IPC.

## Règles de validation IPC à appliquer module par module

Chaque handler IPC qui reçoit des données du renderer doit valider :

- identifiants : entier strictement positif ;
- montants : nombre fini supérieur ou égal à 0 ;
- dates : format `YYYY-MM-DD` et date réelle ;
- textes : trim, longueur maximale, champ obligatoire si nécessaire ;
- statuts : valeurs fermées par enum ;
- listes : tableau attendu avec longueur minimale si nécessaire.

Modules prioritaires :

1. Recettes journalières et mensuelles.
2. Facturation et paiements.
3. Trésorerie et caisse.
4. RH, paie, absences et contrats.
5. Achats et stocks.
6. Parking et plage.
7. GED.
8. PortMaster.

## Workflows métier à verrouiller

### Recettes

Statuts recommandés :

- `brouillon` : saisie modifiable ;
- `soumis` : transmis pour contrôle ;
- `valide` : verrouillé ;
- `refuse` : retour motivé ;
- `cloture` : inclus dans la clôture mensuelle, modification interdite sauf profil habilité.

Contrôles attendus :

- motif obligatoire pour modification ou suppression ;
- alerte si non-saisie avant 09h30 ;
- justification obligatoire si écart entre recette journalière et encaissement ;
- journal d'audit complet.

### Facturation

Statuts recommandés :

- brouillon ;
- proforma ;
- validée ;
- envoyée ;
- paiement partiel ;
- payée ;
- annulée ;
- avoir émis.

Contrôles attendus :

- numérotation unique ;
- verrouillage après validation ;
- lien obligatoire vers client, convention, réservation ou contrat ;
- suivi paiement et recouvrement.

### RH et paie

Contrôles attendus :

- tests de calcul paie DZ ;
- validation des périodes ;
- justification des absences ;
- historique des contrats ;
- verrouillage après validation mensuelle.

## Sauvegarde et restauration

Avant production, documenter et tester :

- sauvegarde automatique quotidienne ;
- sauvegarde manuelle avant migration ;
- restauration complète sur poste vierge ;
- contrôle d'intégrité SQLite ;
- conservation minimum de 30 jours ;
- export des logs d'erreur.

## Définition des statuts modules

Chaque module doit avoir un statut affiché clairement :

- `Production` : testé, documenté, utilisé ;
- `Pilote` : utilisable sur une unité limitée ;
- `MVP` : fonctions principales présentes, corrections attendues ;
- `En développement` : non exploitable officiellement ;
- `Prévu` : route ou placeholder uniquement.

## Checklist avant fusion dans main

- `npm ci` passe.
- `npm test` passe.
- `npm run build` passe.
- Le workflow CI passe sur GitHub Actions.
- Aucun module critique n'est ajouté sans validation IPC.
- Les modules argent/paie disposent de tests.
- Une procédure de sauvegarde/restauration est testée.
- Les statuts des modules sont visibles et sincères.

## Priorités suivantes

1. Brancher `electron/ipc/validation.ts` sur tous les handlers critiques.
2. Ajouter des tests pour recettes, facturation, RH/paie et backup.
3. Ajouter l'installateur Windows dans le workflow release.
4. Ajouter un écran de suivi des alertes : retards de saisie, factures échues, contrats expirés, sauvegardes non exécutées.
5. Documenter les procédures utilisateur par rôle.
