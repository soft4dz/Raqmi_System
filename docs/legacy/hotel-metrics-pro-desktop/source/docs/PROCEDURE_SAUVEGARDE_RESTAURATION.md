# Procédure de sauvegarde et restauration

## Principe

La base SQLite locale est le coeur de l'application. Une sauvegarde non testée est une décoration, comme un extincteur vide accroché au mur pour rassurer les visiteurs.

## Fréquence recommandée

- Sauvegarde automatique quotidienne.
- Sauvegarde manuelle avant chaque mise à jour.
- Sauvegarde mensuelle archivée séparément.
- Conservation minimale : 30 jours.

## Avant sauvegarde

1. Fermer les opérations de saisie en cours.
2. Vérifier que l'application n'est pas en cours d'import legacy.
3. Lancer un contrôle d'intégrité SQLite si disponible.
4. Vérifier l'espace disque.

## Contenu minimal d'une sauvegarde

- Base SQLite principale.
- Fichiers GED si stockés localement.
- Paramètres application.
- Logs applicatifs.
- Version de l'application.

## Nommage recommandé

Format :

```text
hotel_metrics_YYYY-MM-DD_HH-mm_version.db
```

Exemple :

```text
hotel_metrics_2026-06-19_09-30_v0.8.0.db
```

## Test de restauration

À faire au moins une fois par mois :

1. Copier la sauvegarde sur un poste de test.
2. Installer la même version de l'application.
3. Restaurer la base.
4. Ouvrir l'application.
5. Vérifier les modules suivants :
   - connexion ;
   - recettes ;
   - facturation ;
   - RH ;
   - PortMaster ;
   - GED ;
   - rapports.
6. Comparer les totaux clés avec l'environnement d'origine.

## Rapport de restauration

Chaque test doit produire un mini PV :

- date du test ;
- version application ;
- fichier restauré ;
- poste utilisé ;
- résultat ;
- anomalies constatées ;
- décision : validé ou à corriger.

## Règle de sécurité

Aucune mise à jour en production ne doit être exécutée sans sauvegarde manuelle récente et testée.
