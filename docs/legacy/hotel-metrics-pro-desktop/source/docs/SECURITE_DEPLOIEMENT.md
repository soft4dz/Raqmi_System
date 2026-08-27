# Procédure de sécurité et de déploiement

## Statut

Cette procédure s'applique à partir du correctif de stabilisation P0 de la version 0.8.
Elle ne remplace pas un audit de sécurité, une homologation interne ou les formalités
requises au titre de la protection des données personnelles.

## 1. Première installation

Lorsqu'une base vide est initialisée :

1. un compte `admin@hotelmetrics.local` est créé ;
2. un mot de passe aléatoire propre à l'installation est généré ;
3. le changement de mot de passe est obligatoire à la première connexion ;
4. les identifiants temporaires sont écrits dans :
   `%AppData%\hotel-metrics-pro-desktop\data\INITIAL_ADMIN_CREDENTIALS.txt` ;
5. le fichier doit être supprimé dès que le mot de passe a été changé.

Aucun mot de passe administrateur universel ne doit être communiqué, documenté ou
réintroduit dans les scripts.

## 2. Authentification

- Une session authentifiée est obligatoire pour tout IPC métier protégé.
- L'auto-connexion administrateur est interdite, y compris en développement.
- La session n'est pas restaurée après redémarrage tant qu'un stockage natif chiffré
  n'est pas intégré.
- Un compte désactivé ne doit jamais être réactivé automatiquement au démarrage.
- Chaque personne doit utiliser un compte nominatif.

## 3. Récupération d'un compte

Fermer complètement l'application puis exécuter :

```bat
fix-auth.bat
```

Le script demande l'adresse du compte, génère un secret temporaire et crée un fichier
`PASSWORD_RESET_<compte>.txt` près de la base. Le changement de mot de passe est
obligatoire à la connexion suivante.

Le secret ne doit pas être transmis par un canal public ni conservé après utilisation.

## 4. Synchronisation

La variable suivante est obligatoire côté poste et côté serveur :

```text
HMP_SYNC_API_KEY=<secret d'au moins 32 caractères>
```

Règles :

- ne jamais utiliser `dev-sync-key-change-me` ;
- utiliser un secret généré aléatoirement ;
- injecter le secret par l'environnement, jamais dans Git ;
- utiliser HTTPS pour tout serveur distant ;
- renouveler le secret après un incident ou un départ d'administrateur ;
- désactiver la synchronisation si le secret n'est pas configuré.

La clé partagée actuelle doit être remplacée à terme par une identité et un secret
propres à chaque poste, avec révocation individuelle.

## 5. Build et signature Windows

Le build exécute un contrôle préalable qui bloque les anciens drapeaux :

- `VITE_AUTO_LOGIN=true` ;
- `HMP_DEV_AUTO_ADMIN=1`.

L'installateur exige une signature de code. Les secrets du certificat doivent être
injectés dans l'environnement sécurisé de build, par exemple :

```text
WIN_CSC_LINK
WIN_CSC_KEY_PASSWORD
```

Le certificat et son mot de passe ne doivent jamais être ajoutés au dépôt.

## 6. Facturation

Les nouvelles factures utilisent une séquence atomique par unité et exercice :

```text
FAC-<CODE_UNITE>-<ANNEE>-<SEQUENCE>
```

La création de l'en-tête, des lignes et de la séquence est transactionnelle. Avant une
mise en production fiscale, faire valider par la comptabilité et le commissaire aux
comptes :

- la convention de numérotation ;
- les mentions obligatoires ;
- les taux et dates d'effet de TVA ;
- la procédure d'avoir et d'annulation ;
- les durées de conservation.

## 7. Sauvegardes et données sensibles

Mesures provisoires obligatoires :

- restreindre les droits Windows sur le dossier de données ;
- conserver une copie hors du poste ;
- tester périodiquement la restauration ;
- interdire l'envoi des fichiers `.db` par messagerie non sécurisée ;
- limiter l'accès aux données RH, médicales et salariales.

Mesures encore à développer avant production sensible :

- chiffrement de la base et des sauvegardes ;
- stockage sécurisé des clés avec DPAPI ou mécanisme équivalent ;
- politique de conservation et d'effacement ;
- journal réglementaire des opérations sur les données personnelles ;
- registre des traitements et gestion des demandes des personnes concernées.

## 8. Validation avant fusion ou livraison

Exécuter au minimum :

```bash
npm ci --ignore-scripts
npm run rebuild:native
npm test
npm run build
```

Puis tester manuellement :

1. connexion et changement obligatoire du mot de passe initial ;
2. rejet de tout IPC métier sans session ;
3. séparation des accès entre unités ;
4. création simultanée de plusieurs factures ;
5. sauvegarde puis restauration ;
6. récupération d'un compte ;
7. synchronisation avec une clé explicitement configurée ;
8. vérification de la signature de l'installateur.

Aucune livraison ne doit être réalisée si la CI échoue ou si l'installateur n'est pas
signé.
