# Recrutement (ATS), pointeuses & badgeuses

## 1. Présentation

Ce module regroupe deux fonctions RH opérationnelles distinctes qui partagent le même hub `talents`/`temps` du [hub RH](rh-productivite.md) :

- le **recrutement (ATS)** : pipeline candidats par étapes, offres d'emploi, entretiens, historique — jusqu'à l'embauche qui crée automatiquement une fiche employé et un compte utilisateur en attente ;
- les **pointeuses & badgeuses** : enregistrement des appareils de pointage (dont synchronisation réseau ZKTeco), import de logs bruts (CSV ou TCP), et génération automatique des pointages RH.

Il s'adresse au responsable RH — voir [`docs/guides-utilisateurs/07-rh-manager.md`](../guides-utilisateurs/07-rh-manager.md) (« Recrutement ATS », « Temps et absences »).

## 2. Prérequis & accès

- **Recrutement** : hub `talents`, sous-onglet `recrutements` → route effective `/rh/talents/recrutements` (composant `RecrutementsTab`, `src/pages/rh/RecrutementsTab.tsx`), visible uniquement si `canManageRh(role)` (`RhHubContent`, cas `'talents'`).
- **Pointeuses** : hub `temps`, sous-onglet `pointeuse` (`manageOnly: true` dans `rhNavigation.ts`) → route effective `/rh/temps/pointeuse` (composant `PointeuseTab`, `src/pages/rh/PointeuseTab.tsx`), visible uniquement si `canManageRh(role)`.
- Toutes les mutations backend vérifient `rh.manage` : `assertRhManage`/`assertRhPointeuse` dans `electron/services/rh-employe.service.ts` et `electron/services/rh-pointeuse.service.ts`.
- Dépend du **référentiel postes/départements** (`/rh/referentiel`) pour rattacher une offre ou une candidature à un poste, et de la **fiche employé** (champ badge pointeuse) pour le rapprochement des pointages.

Note de cohérence avec le sommaire : le tableau du gabarit ([`README.md`](README.md)) référence des routes génériques `/rh/recrutement*` et `/rh/temps/pointeuse*` ; dans le code actuel, ces fonctions sont exposées via le système de hubs sous `/rh/talents/recrutements` et `/rh/temps/pointeuse` (voir [rh-productivite.md](rh-productivite.md) pour le détail du routage `/rh/:hub/:sub`).

## 3. Écrans & champs

### 3.1 Recrutement — Pipeline (`RecrutementsTab`, vue « Pipeline »)

Vue Kanban par étape (`EtapeRecrutement`) : Candidature → Présélection → Entretien RH → Entretien métier → Proposition → Embauche / Refusé. Chaque colonne affiche le nombre de candidatures et des cartes candidat (nom, poste, source). Un filtre par offre est disponible en haut de l'écran.

### 3.2 Recrutement — Offres (vue « Offres »)

Table des offres d'emploi (`RhOffreEmploi`) : Titre, Poste + département, Statut (`brouillon`/`publiee`/`pourvue`/`archivee`), Nombre de candidatures, action « Voir pipeline » (filtre le pipeline sur cette offre). Formulaire **Nouvelle offre** : poste, titre, description, nombre de postes, statut.

### 3.3 Recrutement — Liste (vue « Liste »)

Table à plat de toutes les candidatures (`RhRecrutement`) : Candidat (nom + e-mail), Poste (département), Offre liée, Étape (badge coloré), Source. Formulaire **Nouvelle candidature** : offre (optionnelle) ou poste, nom (obligatoire), prénom, e-mail, téléphone, source (LinkedIn, cooptation…), notes.

### 3.4 Fiche candidature (panneau détail)

Ouverte au clic sur une carte/ligne : coordonnées, offre, source, actions d'avancement d'étape (bouton dynamique selon l'étape courante — `nextEtape`), bouton **Embaucher** (à l'étape « Proposition », avec confirmation « Un employé et un compte en attente seront créés »), bouton **Refuser** (avec motif optionnel). Sous-section **Entretiens** : liste des entretiens planifiés (type, date/heure, statut) et formulaire d'ajout (type : téléphone/RH/technique/direction/autre, date et heure, lieu ou lien). Sous-section **Historique** : journal des transitions d'étape avec commentaire éventuel.

### 3.5 Pointeuses & badgeuses (`PointeuseTab`)

- Sélecteur d'hôtel.
- **Appareils enregistrés** : liste des pointeuses (`RhPointeuse` étendu) — nom, marque, adresse IP, dernière synchronisation ; actions « Sync maintenant » (désactivée sans IP) et case à cocher « Auto 5 min » (synchronisation automatique toutes les 5 minutes) ; badge d'erreur si le dernier statut de sync est en erreur. Formulaire d'ajout : nom, marque (défaut `ZKTeco`), IP optionnelle.
- **Import CSV** : zone de texte pour coller un export de pointeuse (colonnes attendues : `badge_id`/`matricule`, date/datetime, heure optionnelle, type `entree`/`sortie`), bouton **Importer** puis bouton **Générer pointages RH**.
- **Logs bruts non traités** : table des pointages non encore rapprochés (`RhRawPunch`) — horodatage, badge, employé (ou badge « Non mappé » si le badge n'est associé à aucun employé), type.
- Rappel affiché à l'écran : « Import CSV ou sync TCP ZKTeco (port 4370) toutes les 5 min si activé. Empreintes sur l'appareil uniquement (ANPDP). »

## 4. Workflows standards

### 4.1 Publier une offre et suivre un candidat

1. `/rh/talents/recrutements`, vue Offres → **Nouvelle offre** (poste, titre, description).
2. Vue Pipeline ou Liste → **Nouvelle candidature**, rattacher à l'offre ou directement à un poste.
3. Faire progresser la candidature étape par étape via le bouton d'avancement dans le panneau détail (`avancerCandidature`).
4. Planifier un ou plusieurs entretiens (type + date/heure + lieu).
5. À l'étape Proposition, cliquer **Embaucher** — confirme la création automatique d'un employé et d'un compte utilisateur.
6. Ou **Refuser** avec motif — l'étape passe à `refuse`, la candidature n'apparaît plus en pipeline actif.

### 4.2 Embauche automatique (backend — `validerRecrutement`, `electron/services/rh-employe.service.ts`)

Note : cette fonction agit sur le flux « recrutement » historique (`rh:recrutements:valider`) ; le flux ATS pipeline utilisé par `RecrutementsTab` (`rh:ats:candidatures:avancer`) suit une logique d'avancement d'étape équivalente jusqu'à l'étape `embauche`. Dans les deux cas, l'embauche déclenche :
1. Résolution du **rôle système** associé au poste (`role_system_associe` sur `rh_postes`) — erreur si aucun rôle correspondant n'existe.
2. Génération d'un e-mail par défaut si absent (`prenom.nom@raqmi.local`), rejet si e-mail invalide ou déjà utilisé par un compte existant.
3. Création de la **fiche employé** (`rh_employes`, statut `actif`, date d'embauche = aujourd'hui) et initialisation de son onboarding.
4. Création d'un **compte utilisateur en attente d'activation** (`account_status = 'en_attente'`, mot de passe temporaire à changer à la première connexion) lié à l'employé.
5. Création d'un **contrat CDI** par défaut (35 h hebdo, salaire = salaire minimum du poste).
6. Mise à jour de la candidature : statut `valide`, étape `embauche`, `employeCreeId`/`utilisateurCreeId` renseignés.

### 4.3 Enregistrer une pointeuse et badger un employé

1. `/rh/temps/pointeuse`, choisir l'hôtel.
2. **Ajouter** une pointeuse (nom, marque ZKTeco par défaut, IP si synchronisation réseau prévue).
3. Sur la fiche employé (hors périmètre détaillé de cette fiche — voir `EmployeFiche360.tsx`), renseigner le **badge** (`pointeuse_badge_id`) pour permettre le rapprochement automatique.
4. Synchroniser :
   - **Import CSV manuel** : coller l'export de la pointeuse, cliquer **Importer** (déduplication par hash sur hôtel + badge + horodatage).
   - **Synchronisation réseau ZKTeco** : bouton « Sync maintenant » (lecture directe des enregistrements de présence via TCP, port par défaut 4370) ou case « Auto 5 min » pour une synchronisation automatique périodique.
5. Cliquer **Générer pointages RH** — traite les logs bruts non traités par jour et par employé (première entrée = `entree`, dernière = `sortie`, calcul des heures), crée ou met à jour les pointages (`rh_pointages`, statut `brouillon`, source `pointeuse`) ; les pointages déjà `valide` sont ignorés et comptés séparément.
6. Les pointages générés en `brouillon` doivent ensuite être soumis/validés dans `/rh/temps/pointages` avant d'être pris en compte par la pré-paie — voir [rh-paie-declarations.md](rh-paie-declarations.md).

## 5. Règles métier DZ

Aucune règle DZ spécifique n'est codée dans ce module lui-même (pas de barème, cotisation ou déclaration légale ici). Deux points de vigilance réglementaire sont toutefois documentés dans le code :

- **Recrutement** : l'embauche crée un contrat CDI par défaut — la conformité des déclarations d'embauche (ANEM) est traitée dans le module paie ([rh-paie-declarations.md](rh-paie-declarations.md), export « ANEM embauches »), pas ici.
- **Pointeuses biométriques** : le texte affiché dans `PointeuseTab` précise que les **empreintes restent sur l'appareil uniquement**, en cohérence avec la loi algérienne 18-07 sur la protection des données à caractère personnel (ANPDP) — seuls les identifiants de badge et horodatages transitent vers l'ERP, jamais de donnée biométrique. Voir [conformite-donnees-personnelles.md](conformite-donnees-personnelles.md).

## 6. Interconnexions

- Une embauche validée crée un employé et un contrat repris ensuite dans **Collaborateurs** (`/rh/collaborateurs/annuaire`, `/rh/collaborateurs/contrats`) — voir [rh-productivite.md](rh-productivite.md).
- Le référentiel **postes/départements** (`/rh/referentiel`) conditionne la création d'offres et de candidatures, ainsi que le rôle système attribué automatiquement à l'embauche.
- Les pointages générés depuis les pointeuses alimentent directement **Pointages** (`/rh/temps/pointages`) puis la **pré-paie mensuelle** — voir [rh-paie-declarations.md](rh-paie-declarations.md), section « Générer et valider la pré-paie ».
- Toute création de pointeuse, import de pointages ou traitement de logs émet un événement applicatif (`emitErpEvent` : `POINTEUSE_IMPORTED`, `POINTEUSE_SYNCED`, `POINTAGES_GENERATED`) et une entrée d'audit — voir [journalisation-tracabilite.md](journalisation-tracabilite.md).
- Le compte utilisateur créé à l'embauche apparaît en attente d'activation dans **Administration des utilisateurs** — voir [administration-utilisateurs.md](administration-utilisateurs.md).

## 7. Dépannage

- **« Sélectionnez un poste ou une offre. »** à la création d'une candidature : au moins un des deux champs est obligatoire (l'offre pré-remplit le poste).
- **Bouton « Sync maintenant » désactivé** : la pointeuse n'a pas d'adresse IP renseignée — l'éditer pour en ajouter une, ou utiliser l'import CSV manuel.
- **Badge « Sync OK » absent malgré la sync auto activée** : vérifier le dernier statut (`dernierSyncStatut`) — un badge rouge avec message d'erreur apparaît si la connexion TCP à la pointeuse a échoué (IP/port injoignable, timeout).
- **Employé affiché « Non mappé » dans les logs bruts** : aucun employé n'a ce `badge_id` en `pointeuse_badge_id` — associer le badge sur la fiche employé puis relancer le traitement.
- **Pointage non généré malgré des logs importés** : les pointages déjà `valide` pour cet employé/cette date sont ignorés (compteur « ignorés ») ; vérifier aussi que les logs sont bien datés dans la plage traitée (`dateDebut`/`dateFin` si spécifiées).
- **Import CSV pointeuse rejeté** : vérifier que chaque ligne contient au minimum un identifiant de badge et une date exploitable (formats acceptés : `AAAA-MM-JJ HH:MM` ou `JJ/MM/AAAA HH:MM`).
- **« Ce recrutement n'est plus en cours. »** en tentant de valider/refuser une candidature déjà traitée : rafraîchir le pipeline, la candidature a déjà été embauchée ou refusée par un autre utilisateur.
- **« Un compte existe déjà avec cet e-mail. »** à l'embauche : corriger l'e-mail du candidat (doublon avec un compte utilisateur existant) avant de relancer l'embauche.
