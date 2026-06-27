# Architecture installable desktop

## Décision

Raqmi System doit être livré comme un logiciel installable, pas comme une simple application web.

Cela signifie :

- installateur Windows pour les postes clients ;
- raccourci bureau et menu démarrer ;
- configuration de l'adresse serveur ;
- contrôle licence côté serveur ;
- interface desktop basée sur Electron ou Tauri ;
- serveur local ou cloud séparé.

## Composants installables

### Raqmi System Client

Installé chez les utilisateurs finaux.

Fonctions :

- connexion au serveur local ou cloud ;
- affichage des modules autorisés ;
- saisie métier ;
- consultation des rapports ;
- upload de documents ;
- cache local optionnel.

### Raqmi License Manager

Installé uniquement chez l'éditeur.

Fonctions :

- créer les clients ;
- créer et modifier les licences ;
- activer les modules ;
- limiter utilisateurs, sites et stockage ;
- suspendre ou renouveler une licence ;
- générer une licence offline.

### Raqmi System Server

Installé chez le client ou hébergé dans le cloud.

Fonctions :

- API métier ;
- PostgreSQL ;
- stockage fichiers ;
- audit ;
- vérification licence ;
- sauvegardes ;
- synchronisation cloud optionnelle.

## Schéma

```text
Raqmi System Client installé
→ Raqmi System Server local ou cloud
→ PostgreSQL
→ Stockage fichiers local ou cloud
→ Vérification licence

Raqmi License Manager installé côté éditeur
→ crée / modifie les licences
→ contrôle les activations
→ génère licences offline
```

## Pourquoi pas application web pure

Un ERP vendu aux clients doit pouvoir être installé, contrôlé et configuré. Une application web pure impose souvent un hébergement central et ne convient pas toujours aux clients qui veulent un serveur local.

L'interface peut être développée en React, mais emballée dans Electron/Tauri pour produire un vrai logiciel installable.
