# Déploiement serveur local

## Objectif

Permettre à un client d'utiliser Raqmi System sur son propre réseau local, avec une base de données et un stockage fichiers centralisés.

## Architecture

```text
Poste utilisateur 1 ─┐
Poste utilisateur 2 ─┼→ Raqmi System Server → PostgreSQL
Poste utilisateur 3 ─┘                    → Stockage fichiers local
                                           → Sauvegarde locale/cloud
                                           → Vérification licence
```

## Composants serveur

- API Raqmi System Server ;
- PostgreSQL ;
- dossier de stockage fichiers ;
- moteur de sauvegarde ;
- service de vérification licence ;
- journal d'audit ;
- tâche d'upload cloud optionnelle.

## Variables d'environnement minimales

```env
DATABASE_URL=postgresql://raqmi:raqmi_password@localhost:5432/raqmi_system
FILE_STORAGE_DRIVER=local
FILE_STORAGE_LOCAL_PATH=/var/lib/raqmi-system/storage
LICENSE_MODE=offline
LICENSE_FILE_PATH=/etc/raqmi-system/license.license
```

## Stockage fichiers

Les fichiers sont enregistrés dans un répertoire serveur et référencés en base de données.

Exemples :

```text
/storage/tenants/{tenantId}/ged
/storage/tenants/{tenantId}/invoices
/storage/tenants/{tenantId}/contracts
/storage/tenants/{tenantId}/rh
/storage/tenants/{tenantId}/maintenance
```

## Sauvegardes

À prévoir :

- sauvegarde quotidienne PostgreSQL ;
- sauvegarde quotidienne du stockage fichiers ;
- conservation paramétrable ;
- restauration testée sur poste vierge ;
- upload cloud optionnel.

## Sécurité minimale

- accès serveur limité au réseau client ;
- HTTPS interne recommandé ;
- comptes utilisateurs nominatifs ;
- permissions par rôle ;
- contrôle de licence côté API ;
- journalisation des actions sensibles ;
- sauvegarde chiffrée si export hors site.
