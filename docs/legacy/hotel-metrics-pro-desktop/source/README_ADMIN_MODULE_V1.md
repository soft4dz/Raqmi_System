# Patch V1 - Module Administration & Utilisateurs

Ce patch ajoute une page centrale au module Administration.

## Fichier à créer

Copier le fichier fourni vers :

src/pages/administration/AdminDashboardPage.tsx

## Routes à modifier

Voir :

snippets/AppRoutes_modification.txt

## Titres à modifier

Voir :

snippets/pageTitles_modification.txt

## Fonctionnalités ajoutées

- Tableau de bord administration
- Statistiques utilisateurs actifs / inactifs
- Statistiques unités actives
- Nombre de rôles
- Nombre de super-administrateurs
- Comptes multi-unités
- Vue des rôles et permissions
- Activité administrative récente via audit
- Raccourcis vers utilisateurs, rôles, unités, rubriques

## Commandes après application

npm run build
npm run dev

## Note

L'outil GitHub a bloqué l'écriture directe dans le module administration. Ce patch est prêt pour Cursor.
