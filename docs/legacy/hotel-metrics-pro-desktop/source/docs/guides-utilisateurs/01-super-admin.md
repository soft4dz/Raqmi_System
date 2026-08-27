# Guide utilisateur — Super administrateur

## Rôle

Le super administrateur gère les paramètres globaux de l'application, les utilisateurs, les rôles, les modules, la base de données, les sauvegardes et les accès sensibles.

## Accès principaux

- Administration des utilisateurs.
- Gestion des rôles et permissions.
- Paramétrage des hôtels, rubriques et référentiels.
- Activation et suivi des modules.
- Sauvegarde et restauration.
- Consultation des journaux d'audit.
- Paramètres système, interface, sécurité et base de données.

## Tâches quotidiennes

1. Vérifier les alertes système.
2. Contrôler les échecs de connexion suspects.
3. Vérifier l'état de la sauvegarde.
4. Consulter les journaux d'audit en cas d'anomalie.
5. Traiter les demandes de création ou modification d'utilisateurs.

## Procédures principales

### Créer un utilisateur

1. Aller dans `Administration > Utilisateurs`.
2. Cliquer sur `Nouvel utilisateur`.
3. Renseigner nom, email, rôle, unité et statut.
4. Affecter les permissions selon le profil réel.
5. Enregistrer.
6. Demander à l'utilisateur de changer son mot de passe à la première connexion.

### Désactiver un utilisateur

1. Ouvrir la fiche utilisateur.
2. Vérifier qu'il ne doit plus accéder à l'application.
3. Désactiver le compte au lieu de supprimer l'historique.
4. Contrôler l'audit après l'opération.

### Sauvegarde

1. Aller dans `Paramètres > Sauvegarde`.
2. Lancer une sauvegarde manuelle avant toute mise à jour.
3. Vérifier le fichier généré.
4. Conserver une copie externe.

## Points de contrôle

- Aucun compte partagé.
- Aucun utilisateur actif sans rôle clair.
- Aucun module critique sans responsable.
- Sauvegarde récente disponible.
- Audit consultable.

## Erreurs fréquentes

- Donner un accès administrateur par confort.
- Supprimer un utilisateur au lieu de le désactiver.
- Modifier les rôles sans informer les responsables.
- Lancer une mise à jour sans sauvegarde.

## Règles de sécurité

- Principe du moindre privilège.
- Mot de passe individuel obligatoire.
- Verrouillage des comptes inutilisés.
- Sauvegarde obligatoire avant mise à jour.
- Contrôle mensuel des accès.
