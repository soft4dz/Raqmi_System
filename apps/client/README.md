# Raqmi System Client

Application utilisée par les clients finaux.

## Responsabilités

- Authentification utilisateur.
- Affichage des modules selon licence et rôle.
- Saisie métier.
- Consultation des tableaux de bord.
- Upload des fichiers vers Raqmi System Server.
- Cache local optionnel.

## Règle

Le client ne décide jamais seul de l'activation d'un module. Il demande les droits au serveur, qui vérifie la licence.
