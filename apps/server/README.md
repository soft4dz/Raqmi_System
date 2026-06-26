# Raqmi System Server

Serveur métier installé chez le client ou hébergé dans le cloud.

## Responsabilités

- API métier.
- PostgreSQL central.
- Gestion des fichiers.
- Contrôle de licence côté serveur.
- Gestion utilisateurs, rôles et permissions.
- Audit et traçabilité.
- Sauvegardes.
- Synchronisation cloud optionnelle.

## Base recommandée

PostgreSQL est la base principale recommandée pour le mode client/serveur.

## Stockage fichiers

Drivers prévus :

- `local` : stockage sur serveur local ;
- `s3` : stockage cloud compatible S3 ;
- `hybrid` : local + upload cloud planifié.
