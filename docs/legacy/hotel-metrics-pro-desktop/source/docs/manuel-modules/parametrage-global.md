# Paramètres, interface & thème, sécurité & accès

## 1. Présentation

Module socle regroupant trois volets : les paramètres généraux de l'installation (identité de l'entreprise, préférences d'exploitation, sauvegarde, audit, licence), la personnalisation de l'interface (thème, densité, sidebar) propre à chaque utilisateur, et la sécurité du compte connecté (mot de passe, politique de connexion, session). C'est le hub de navigation vers les autres écrans « Système » (modules, notifications, base de données, sauvegarde, santé système).

Composants : `src/pages/system/SettingsPage.tsx` (`/settings`), `InterfaceThemePage.tsx` (`/settings/interface`), `SecuriteAccesPage.tsx` (`/settings/securite`). Guide associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md).

## 2. Prérequis & accès

- Authentification requise ; les trois pages sont accessibles à tout utilisateur connecté (pas de garde `RequirePermission`/`RequireSystemAdmin` dans `AppRoutes.tsx` sur `/settings`, `/settings/interface`, `/settings/securite`).
- Dans le détail, chaque page adapte son contenu selon le rôle :
  - `SettingsPage` : la modification des paramètres généraux (formulaire « Informations entreprise », « Exploitation & finances », etc.) et la licence sont réservées à `canManageUsers(user?.role)` — les champs sont sinon affichés en lecture seule. Côté service, `getAppInfo`/`updateAppSettings` exigent la permission `users.manage` (`assertSettingsAdmin` dans `electron/services/settings.service.ts`).
  - `InterfaceThemePage` : aucune restriction — chaque utilisateur personnalise son propre affichage (stocké dans `useUiStore`, persistance locale + synchronisation serveur via `saveUserUiPreferencesToServer`).
  - `SecuriteAccesPage` : le changement de mot de passe est ouvert à tout utilisateur (son propre compte) ; le bloc « Politique de connexion » (tentatives max, durée de verrouillage) n'est visible que si `canManageUsers(user?.role)`.
- Liens de navigation présents sur `SettingsPage` vers `/settings/modules` (si admin), `/system/sync` (si `canManageSync`), `/rh/referentiel` (si `canManageRh`).

## 3. Écrans & champs

### Paramètres généraux (`SettingsPage.tsx` / `/settings`)
- Carte « Mon compte » : nom, e-mail, rôle de l'utilisateur connecté (lecture seule).
- Carte « Application » : version, chemin de la base SQLite locale, raccourcis « Modules activés » et « Synchronisation ».
- Carte « Licence Raqmi System » (admin uniquement) : état (`active`/`trial`/`expired`/`development`), édition, date d'expiration, identifiant poste (`machineId`), champ de saisie de clé de licence (format `RS-PRO-AAAAMMJJ-XXXXXXXX`) et bouton « Activer la licence ».
- Formulaire « Informations entreprise » : nom court, raison sociale, adresse, téléphone, e-mail officiel, logo entreprise (upload/suppression, stocké `data/logos/company/`).
- Formulaire « Exploitation & finances » : page d'accueil par défaut, devise par défaut (`DZD`), nombre de décimales, heure limite de saisie du CA quotidien, taux TVA port (%).
- Formulaire « Validation » : case « Validation obligatoire » des recettes, case « Motif obligatoire en cas de correction ».
- Formulaire « Sécurité » : tentatives de connexion max (3–20), durée de verrouillage en minutes (5–120).
- Formulaire « Sauvegarde » : case « Sauvegarde automatique activée », heure de sauvegarde, nombre de sauvegardes à conserver (1–365).
- Formulaire « Rapports & audit » : case « Audit activé », texte + image d'en-tête de rapport, texte + image de pied de page de rapport.

### Interface & Thème (`InterfaceThemePage.tsx` / `/settings/interface`)
- « Profils d'interface » : cartes cliquables issues de `LAYOUT_PROFILES` (`src/shared/constants/layoutProfiles.ts`) appliquant en un clic sidebar/densité/couleur.
- « Couleur d'accentuation » : 8 couleurs (`navy`, `blue`, `violet`, `emerald`, `rose`, `amber`, `cyan`, `slate`).
- « Densité d'affichage » : `compact`, `comfortable`, `spacious` (avec aperçu du rayon des coins).
- « Sidebar » : étendue ou réduite par défaut au démarrage.
- « Prévisualisation » : bloc miniature illustrant le rendu combiné des réglages actifs.
- Chaque changement est appliqué immédiatement (`useUiStore`) puis enregistré côté serveur via `saveUserUiPreferencesToServer()` (persistance par utilisateur).

### Sécurité & Accès (`SecuriteAccesPage.tsx` / `/settings/securite`)
- « Compte actif » : avatar (initiales), nom, e-mail, rôle, date de dernière connexion.
- « Changer le mot de passe » : mot de passe actuel, nouveau mot de passe (jauge de force en direct), confirmation.
- « Politique de connexion » (admin) : tentatives max, durée de verrouillage — mêmes champs que dans `SettingsPage` (persistés via `settings:update`).
- « Session » : bouton de déconnexion.
- Bannière d'avertissement si `user.mustChangePassword` est vrai (redirection imposée avant de continuer).

## 4. Workflows standards

**Modifier les paramètres généraux** : formulaire → `ipcClient.settings.update(form)` → `settings:update` (`electron/ipc/settings.ipc.ts`) → `updateAppSettings()` (`electron/services/settings.service.ts`) : valide chaque champ (bornes numériques : décimales 0–4, taux TVA 0–100, tentatives 3–20, verrouillage 5–120 min, conservation sauvegardes 1–365), écrit chaque paramètre en base clé/valeur.

**Téléverser une image de marque** (logo, en-tête, pied de page rapport) : bouton « Téléverser »/« Changer » → `ipcClient.settings.pickBrandAsset(asset)` → sélecteur de fichier natif → `settingsService.pickCompanyBrandAsset()` : copie le fichier, supprime l'ancien, met à jour le paramètre correspondant ; déclenche `notifyBrandingUpdated()` pour rafraîchir le logo affiché dans la sidebar sans recharger l'application.

**Activer une licence** : saisie de la clé → `ipcClient.license.activate(licenseKey)` → met à jour le statut affiché (`LicenseStatusDto`).

**Changer son mot de passe** : formulaire → `ipcClient.auth.changePassword({ currentPassword, newPassword })` — le nouveau mot de passe doit faire au moins 8 caractères (contrôle front) et respecter la politique serveur (`validatePasswordStrength` : majuscule, chiffre, caractère spécial) ; en cas de succès, rafraîchit l'utilisateur courant et efface `must_change_password`.

**Modifier la politique de connexion** : formulaire dédié dans `SecuriteAccesPage` → `ipcClient.settings.update({ tauxTvaPort, maxLoginAttempts, lockoutMinutes })` (mêmes bornes que dans `SettingsPage`).

**Personnaliser l'interface** : chaque clic (profil, couleur, densité, sidebar) appelle immédiatement l'action Zustand correspondante puis `saveUserUiPreferencesToServer()` ; en cas d'échec réseau/serveur, le réglage reste appliqué localement avec un message « Préférences locales appliquées (enregistrement serveur indisponible) ».

## 5. Règles métier DZ

Le champ « Taux TVA port (%) » (`tauxTvaPort`, valeur par défaut 19 %) sert de taux par défaut pour la facturation PortMaster (voir [`portmaster-facturation.md`](portmaster-facturation.md)) — c'est le seul paramètre à connotation fiscale DZ de ce module. Aucune autre règle légale/fiscale spécifique n'est appliquée ici ; les autres réglages (thème, sécurité, sauvegarde) sont purement techniques.

## 6. Interconnexions

- **Administration & utilisateurs** ([`administration-utilisateurs.md`](administration-utilisateurs.md)) : la permission qui contrôle l'édition des paramètres (`users.manage`) est la même que celle qui gère les comptes utilisateurs.
- **Activation des modules** ([`modules-activation.md`](modules-activation.md)) : raccourci direct depuis `SettingsPage` vers `/settings/modules`.
- **Synchronisation multi-postes** ([`synchronisation-multi-postes.md`](synchronisation-multi-postes.md)) : raccourci direct vers `/system/sync`, visible si `canManageSync`.
- **Sauvegarde, base de données, santé système** ([`sauvegarde-restauration.md`](sauvegarde-restauration.md)) : les réglages « Sauvegarde automatique » (heure, rétention) sont saisis ici mais **consommés uniquement en tant que préférence affichée** — voir Dépannage.
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : la case « Audit activé » (`auditEnabled`) est un paramètre stocké, mais aucune fonction du code n'a été trouvée qui lit ce paramètre pour conditionner l'écriture des journaux (`writeAuditLog()` s'exécute inconditionnellement).
- **Alertes & notifications** ([`alertes-notifications.md`](alertes-notifications.md)) : le seuil TVA et les autres réglages n'influent pas directement, mais la page est accessible depuis la même zone « Paramètres » de la sidebar.

## 7. Dépannage

- **Formulaire grisé sur `/settings`** : normal pour tout profil sans permission `users.manage` — les champs sont en lecture seule, seul un administrateur peut enregistrer.
- **Sauvegarde automatique programmée mais aucune sauvegarde ne se déclenche à l'heure configurée** : les champs `autoBackupEnabled`/`autoBackupTime`/`backupRetentionCount` sont bien enregistrés en base, mais **aucun planificateur (cron/`setInterval`) n'a été trouvé dans `electron/main.ts` ou les services** qui déclenche automatiquement une sauvegarde à l'heure choisie — la création de sauvegarde reste une action manuelle sur `/settings/backup` (voir [`sauvegarde-restauration.md`](sauvegarde-restauration.md)).
- **« Le mot de passe doit contenir au moins une majuscule/un chiffre/un caractère spécial »** : politique serveur (`validatePasswordStrength`) plus stricte que le minimum 8 caractères affiché côté front — respecter les 4 critères.
- **Logo/entête ne se met pas à jour dans la sidebar après téléversement** : vérifier que `notifyBrandingUpdated()` s'est bien déclenché (pas d'erreur affichée) ; sinon recharger la fenêtre.
- **Préférences d'interface perdues après changement de poste** : la synchronisation serveur (`saveUserUiPreferencesToServer`) nécessite que l'appel IPC `settings:saveUiPreferences` ait réussi ; en cas d'échec silencieux, seules les préférences locales (ce poste) sont conservées.
