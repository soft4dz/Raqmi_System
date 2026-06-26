# Architecture générale Raqmi System

## Objectif

Raqmi System doit être un ERP multi-client, modulaire et vendable. Il doit fonctionner pour une seule unité, un groupe multi-sites, un hôtel, un port de plaisance ou une structure mixte.

## Composants

```text
┌──────────────────────────────┐
│ Raqmi License Manager         │
│ Portail privé éditeur         │
│ Clients, licences, packs      │
└──────────────┬───────────────┘
               │ vérification online / licence signée
               ▼
┌──────────────────────────────┐
│ Raqmi System Server           │
│ API métier + licence          │
│ PostgreSQL + stockage fichiers│
└──────────────┬───────────────┘
               │ API interne
               ▼
┌──────────────────────────────┐
│ Raqmi System Client           │
│ Interface utilisateur         │
│ Modules selon licence + rôle  │
└──────────────────────────────┘
```

## Rôle de chaque partie

### Raqmi System Client

Application utilisée par les employés du client final.

Responsabilités :

- authentification utilisateur ;
- affichage des modules autorisés ;
- saisie métier ;
- consultation des rapports ;
- upload des fichiers vers le serveur ;
- cache local optionnel.

### Raqmi System Server

Serveur installé chez le client ou hébergé dans le cloud.

Responsabilités :

- API métier ;
- base PostgreSQL ;
- gestion des fichiers ;
- contrôle de licence ;
- droits utilisateurs ;
- audit ;
- sauvegardes ;
- synchronisation cloud optionnelle.

### Raqmi License Manager

Application séparée réservée à l'éditeur du logiciel.

Responsabilités :

- créer les clients ;
- créer les licences ;
- modifier les modules autorisés ;
- renouveler ou suspendre les licences ;
- signer les licences offline ;
- suivre les activations ;
- contrôler les installations.

## Modes de déploiement

### Local on-premise

```text
Client desktop/web
→ réseau local
→ serveur client
→ PostgreSQL local
→ stockage local
```

### Cloud

```text
Client desktop/web
→ internet sécurisé
→ API cloud
→ PostgreSQL cloud
→ stockage cloud
```

### Hybride

```text
Serveur local
→ exploitation quotidienne locale
→ upload cloud des fichiers et sauvegardes
→ vérification licence périodique
```

## Principe fondamental

Le client final ne doit jamais pouvoir créer ou modifier une licence. Il peut seulement importer une licence signée ou vérifier sa licence auprès de Raqmi License Manager.
