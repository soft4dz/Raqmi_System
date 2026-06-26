# Raqmi System

**Raqmi System** est une plateforme ERP modulaire et commercialisable, conçue pour fonctionner en mode **client/serveur**, avec une gestion de licences indépendante, une base de données centrale et un stockage documentaire local ou cloud.

Le produit n'est pas limité à une seule entreprise. Il doit pouvoir être vendu à plusieurs clients : hôtels, groupes touristiques, ports de plaisance, résidences, sociétés de services et structures multi-sites.

## Vision produit

```text
Raqmi System
= ERP multi-client
+ modules activables par licence
+ serveur local ou cloud
+ base PostgreSQL centrale
+ stockage fichiers local/cloud
+ client desktop/web
+ portail éditeur pour licences
```

## Architecture cible

```text
apps/client
→ application utilisée par les clients finaux

apps/server
→ API métier, base de données, stockage fichiers, contrôle licence

apps/license-manager
→ portail privé éditeur pour créer, modifier et renouveler les licences

packages/shared
→ types, constantes, modules, rôles, contrats API

packages/licensing
→ règles licence, packs, signature, validation online/offline

packages/database
→ schémas, migrations, seed et conventions de base de données
```

## Modes de déploiement

### 1. Serveur local

```text
Postes utilisateurs
→ Raqmi System Client
→ Serveur local client
→ PostgreSQL local
→ Stockage fichiers local
→ Synchronisation cloud optionnelle
```

### 2. Serveur cloud

```text
Postes utilisateurs
→ Raqmi System Client
→ API cloud Raqmi / serveur client cloud
→ PostgreSQL cloud
→ Stockage cloud
```

### 3. Mode hybride

```text
Serveur local
→ base et fichiers locaux
→ sauvegarde / upload cloud planifié
→ vérification licence périodique
```

## Modules licence

Chaque licence peut activer ou bloquer des modules précis :

- Administration & utilisateurs
- Paramétrage global
- Unités / sites
- Recettes journalières
- Trésorerie
- Facturation
- Créances & recouvrement
- Contrats & conventions
- RH & paie
- Stocks
- Achats
- Maintenance
- GED
- Parking
- Plage & piscine
- PortMaster
- Qualité & réclamations
- Checklists de contrôle
- Rapports & exports
- Dashboards directionnels
- Synchronisation

## Licence produit

La gestion des licences est séparée du logiciel client.

```text
Raqmi License Manager
→ crée la licence
→ définit les modules autorisés
→ signe la licence
→ contrôle les activations

Raqmi System Server
→ vérifie la licence
→ expose seulement les modules autorisés

Raqmi System Client
→ affiche les modules selon les droits + licence
```

## Statut

Ce dépôt contient le **nouveau socle produit** de Raqmi System. Il servira de base pour séparer proprement :

1. le produit vendu aux clients ;
2. le serveur métier ;
3. le portail de gestion des licences ;
4. les règles communes de licensing et modules.
