# Alertes & notifications

## 1. Présentation

Écran de configuration des préférences de notification de l'utilisateur connecté (alertes du tableau de bord, suivi opérationnel, rapports automatiques, événements système), combiné à la gestion des règles système persistées côté serveur qui alimentent la cloche de notification de la barre de navigation.

Composant : `src/pages/system/NotificationsPage.tsx`. Route : `/settings/notifications`. Guide associé : [`01-super-admin.md`](../guides-utilisateurs/01-super-admin.md).

## 2. Prérequis & accès

- Authentification requise ; **aucune garde de permission** (`RequirePermission`/`RequireSystemAdmin`) n'entoure la route `/settings/notifications` dans `src/routes/AppRoutes.tsx` — tout utilisateur connecté peut y accéder et modifier ses propres préférences.
- Les préférences d'alerte (`notifPrefs`) sont stockées **côté client uniquement**, dans `useUiStore` (Zustand, persistance locale du poste) — elles ne transitent pas par un appel IPC dédié à leur sauvegarde sur cette page (contrairement aux réglages d'interface de [`parametrage-global.md`](parametrage-global.md) qui appellent `saveUserUiPreferencesToServer`).
- Les « Règles système (backend) » (bascules actif/inactif par règle) sont, elles, lues et modifiées via IPC (`notifications:getRules`, `notifications:updateRule`) — aucune vérification de permission particulière n'a été trouvée côté service (`electron/services/notifications.service.ts`) au-delà de l'authentification générale gérée par `wrapIpc`.

## 3. Écrans & champs

- **Règles système (backend)** : liste des règles issues de `notification_rules` (table SQL), chacune avec libellé de condition, module d'origine, code technique, et un interrupteur actif/inactif. Bouton « Générer maintenant » qui déclenche une génération à la demande des notifications système.
- **Alertes du tableau de bord** : trois interrupteurs — Alertes critiques (rouge), Avertissements (orange), Informations (bleu), avec badge du nombre de catégories actives.
- **Opérationnel** :
  - Saisies manquantes (interrupteur simple).
  - Objectifs sous seuil (interrupteur + curseur de seuil d'alerte en %, 10 à 100 %, pas de 5).
- **Rapports automatiques** :
  - Résumé quotidien (interrupteur + champ heure d'envoi).
  - Résumé hebdomadaire (interrupteur simple).
- **Système** : Statut de synchronisation, Erreurs critiques système (interrupteurs simples).
- **Bandeau récapitulatif** : nombre total de notifications actives, précisant que les notifications s'affichent uniquement dans l'application (canaux e-mail/SMS annoncés comme à venir dans une version future).
- Bouton « Réinitialiser » (en-tête) : remet toutes les préférences à leurs valeurs par défaut (`DEFAULT_NOTIF` dans `src/stores/ui.store.ts`).

## 4. Workflows standards

**Activer/désactiver une préférence utilisateur** : clic sur un interrupteur → `setNotifPref(key, value)` (action Zustand) → mise à jour immédiate de l'état local persistant (`persist` middleware) ; ces préférences ne sont **pas** transmises au processus principal Electron sur cette page.

**Basculer une règle système** : clic sur l'interrupteur d'une règle → `ipcClient.notifications.updateRule({ code, actif })` → `notifications:updateRule` (`electron/ipc/notifications.ipc.ts`) → `updateNotificationRule()` (`electron/services/notifications.service.ts`) : met à jour `notification_rules.actif`, écrit une entrée d'audit `UPDATE` / module `notifications`.

**Générer les notifications système à la demande** : bouton « Générer maintenant » → `ipcClient.notifications.generateSystem()` → `notifications:generateSystem` → `generateSystemNotifications()` : évalue trois conditions et crée des notifications pour les administrateurs si la règle correspondante est active :
- Factures échues depuis plus de 30 jours (`facture_echeue`) → lien `/facturation`.
- Produits sous le seuil d'alerte de stock (`stock_seuil`) → lien `/stocks`.
- Workflows en attente de validation (`workflow_attente`) → lien `/workflows`.

**Consommation des notifications** : la cloche de la barre de navigation (`NotificationBell`, `src/layouts/TopNavbar.tsx`) lit les notifications via `notifications:list`/`notifications:countUnread` et permet de les marquer lues (`notifications:markRead`/`markAllRead`) — indépendamment de cet écran de préférences.

## 5. Règles métier DZ

Aucune règle DZ spécifique à ce module.

## 6. Interconnexions

- **Paramétrage global** ([`parametrage-global.md`](parametrage-global.md)) : accessible depuis la même zone « Paramètres » de la sidebar ; utilise le même magasin `useUiStore` que la page Interface & Thème.
- **Facturation** (`facturation.md`), **Stocks & consommations** ([`stocks-consommations.md`](stocks-consommations.md)), **Workflows** (`workflows.md`) : sources des trois règles système générées par `generateSystemNotifications()`.
- **Journalisation & traçabilité** ([`journalisation-tracabilite.md`](journalisation-tracabilite.md)) : chaque bascule de règle système écrit une entrée `audit_log` (module `notifications`).
- Le catalogue de modules (`src/modules/moduleCatalog.ts`, entrée `alertes-notifications`) référence aussi Recettes journalières, Créances & recouvrement, Décisions & instructions et Sauvegarde & restauration comme sources potentielles d'alertes — ces liens ne sont pas tous concrétisés par du code de génération de notification à ce jour (seules les trois règles listées ci-dessus sont implémentées dans `generateSystemNotifications`).

## 7. Dépannage

- **Une préférence utilisateur (alertes, résumés) semble réinitialisée sur un autre poste** : normal — ces réglages sont stockés localement (persistance Zustand du navigateur/poste), pas synchronisés serveur depuis cet écran.
- **« Générer maintenant » ne crée aucune notification alors que des factures sont échues/stocks bas** : vérifier que la règle système correspondante (`facture_echeue`, `stock_seuil`, `workflow_attente`) est bien activée dans la section « Règles système (backend) » — `isRuleActive()` bloque silencieusement la création si la règle est désactivée.
- **Les administrateurs ne reçoivent jamais de notification générée automatiquement** : la fonction `generateSystemNotifications()` (`electron/services/notifications.service.ts`) sélectionne les destinataires par un rôle dont le code SQL est `IN ('super_admin', 'admin_systeme', 'admin')` (minuscules), alors que les codes de rôle réellement utilisés dans l'application sont `SUPERADMIN` et `ADMIN_DEC` (majuscules, voir `src/shared/permissions.ts`) — cette requête ne correspond à aucun rôle existant dans le seed actuel, donc `notifyAdmins()` ne trouve probablement aucun destinataire. À vérifier/corriger côté code si ce comportement n'est pas voulu.
- **Canal e-mail/SMS absent** : confirmé par le message affiché en bas de page — seules les notifications in-app existent dans la version actuelle.
