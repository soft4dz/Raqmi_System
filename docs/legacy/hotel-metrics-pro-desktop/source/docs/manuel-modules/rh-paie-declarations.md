# Paie DZ, bulletins, déclarations, clôture

## 1. Présentation

Ce module couvre le cycle complet de la paie algérienne dans l'ERP : génération de la pré-paie mensuelle à partir des pointages/absences/primes, calcul des cotisations CNAS et de l'IRG selon le barème en vigueur, édition du bulletin de paie PDF avec mentions légales, passerelle d'échange avec le logiciel externe **DLG PC PAIE**, exports déclaratifs DZ (CNAS, DAS, DADS-U, ANEM, virements bancaires) et **clôture mensuelle verrouillante** de la période de paie.

Il correspond au hub `paie` (« Paie & Légal DZ ») du [hub RH](rh-productivite.md) et s'adresse au responsable RH — voir [`docs/guides-utilisateurs/07-rh-manager.md`](../guides-utilisateurs/07-rh-manager.md), section « Préparer la paie ».

## 2. Prérequis & accès

- Toutes les actions de ce module exigent `canManageRh(role)` côté front (fonction `src/shared/permissions.ts`) et, côté backend, `assertPermission(actorUserId, 'rh.manage')` / `assertRhPaie` / `assertRhManage` dans chaque service (`electron/services/rh-paie-dlg.service.ts`, `rh-bulletin-pdf.service.ts`, `rh-declarations-export.service.ts`, `rh-paie-cloture.service.ts`).
- Accès via le hub RH : `/rh/paie/prepaie`, `/rh/paie/primes`, `/rh/paie/dlg`, `/rh/paie/declarations` (composant `PaieTab`, `src/pages/rh/PaieTab.tsx`), `/rh/paie/registres` (`RegistresLegauxTab`), `/rh/paie/conformite` (`ConformiteTab`).
- Écran de clôture dédié, routé **hors du système d'onglets** : `/rh/paie/cloture` → `RhPaieCloturePage` (`src/pages/rh/RhPaieCloturePage.tsx`), protégé par `RequireRh` comme le reste de `/rh/*`.
- Dépend du **référentiel employés/contrats** (salaire brut contractuel, NSS, NIN, RIB, enfants à charge) et des **informations légales employeur** (raison sociale, NIS, N° employeur CNAS, agence CNAS), lues via des paramètres applicatifs (`app_settings` : `company_legal_name`, `rh_employeur_nis`, `rh_employeur_nss`, `rh_employeur_agence_cnas` — `electron/services/rh-legal-export.util.ts`).

## 3. Écrans & champs

### 3.1 Pré-paie (`PaieTab`, sous-onglet `prepaie`)

- Sélecteur de **Période** (mois, `input type="month"`).
- Bouton **Générer pré-paie** → `ipcClient.rh.generatePrePaie(periode)`.
- Table des bulletins (`RhBulletin`), colonnes : Employé, Matricule DLG, Heures, HS (heures + majoration), Retenue absence, Brut, Net, Statut (badge : `brouillon`/`exporte`/`importe`/`valide`), Actions.
- Actions par ligne : **Valider** (si statut ≠ `valide`), **Bulletin PDF** (icône fichier — mentions légales DZ), **Comptabiliser en trésorerie** (icône billet, visible seulement si statut `valide`/`importe` et pas encore de `tresorerieId`).

### 3.2 Primes variables (sous-onglet `primes`)

- Formulaire d'ajout : Employé (liste des actifs), Code (défaut `PRIME`), Libellé, Montant.
- Table des primes existantes de la période (`RhPrime`) : Employé, Code, Libellé, Montant, action Supprimer.

### 3.3 Déclarations DZ (sous-onglet `declarations`)

Cinq boutons d'export, tous générant un fichier CSV (ou PDF pour le bulletin) sauvegardé via `saveRhExportFile` :

| Bouton | IPC / service | Contenu |
|---|---|---|
| DADS-U {année} | `rh:declarations:exportDadsU` → `exportDadsUAnnuelle` | Déclaration nominative annuelle des salaires : identité (NIN/NSS/matricule), détail par bulletin validé de l'année (brut base, HS, retenues, primes, brut imposable, CNAS 9 %/26 %, IRG, net, heures, absences) |
| DAS {année} | `rh:declarations:exportDas` → `exportDasAnnuelle` | Déclaration annuelle des salaires agrégée par employé (brut annuel, primes, cotisations, parafiscales, IRG et net annuels, nb de bulletins) |
| CNAS {période} | `rh:declarations:exportCnas` → `exportCnasMensuelle` | Dépôt CNAS mensuel : NSS, nom, prénom, brut imposable, cotisation salariale 9 %, cotisation patronale 26 %, code établissement, période + ligne de totaux |
| Virements {période} | `rh:declarations:exportVirements` → `exportVirementsPaie` | Fichier de virements bancaires : RIB, bénéficiaire, montant net, référence `PAIE-{période}-{nom}` (uniquement bulletins validés/importés avec net > 0) |
| ANEM embauches | `rh:declarations:exportAnem` → `exportAnemEmbauches` | Liste des embauches à déclarer à l'ANEM (`declaration_anem_statut = 'a_faire'`) : NIN, NSS, nom, prénom, date d'embauche, poste, établissement |

### 3.4 Passerelle DLG PC PAIE (sous-onglet `dlg`)

Échange bidirectionnel de fichiers avec le logiciel de paie externe **DLG PC PAIE** :

- **Configuration** : dossier export (vers DLG), dossier import (depuis DLG), préfixe matricule (défaut `HMP`) — persistés en `app_settings` via `ipcClient.rh.getDlgConfig` / `setDlgConfig`, sélection de dossier via `pickDlgFolder`.
- **Exporter vers DLG** (`exportVersDlg`) : génère `SALARIES.csv` (matricule, nom, prénom, date embauche, fonction, service, salaire base, téléphone, e-mail), `VARIABLES_{période}.csv` (heures travaillées, heures sup, montant HS, retenue absence, jours absence, primes — une ligne par rubrique et par employé), un `README_IMPORT_DLG.txt` explicatif, un `manifest.json`, le tout compressé dans `DLG_EXPORT_{période}_{horodatage}.zip`. Les bulletins passent en statut `exporte`.
- **Importer depuis dossier / fichier** (`importDepuisDlg`) : lit un CSV `;` (colonnes attendues `MATRICULE;BRUT;NET;CHARGES;HEURES`, extraites automatiquement d'une archive `.zip` si besoin), rattache chaque ligne à l'employé par `dlg_matricule`, met à jour ou insère le bulletin (statut `importe`, source `dlg`). Les matricules non reconnus sont comptés en erreurs et le résultat global est `ok` / `partiel` / `erreur`.
- **Journal des échanges** : historique des imports/exports (`rh:dlg:journal`) — sens, période, nombre de lignes, fichier.

### 3.5 Clôture mensuelle (`/rh/paie/cloture` — `RhPaieCloturePage`)

- Sélecteur de période.
- Statut courant (`PaieClotureMensuelle`) : `brouillon` (ambre), `valide` (vert), `cloture` (gris), avec nombre de bulletins et dates de validation/clôture.
- Bouton **Valider paie mensuelle** (si `brouillon`) → `validerPaieMensuelle`.
- Bouton **Clôturer paie mensuelle** (si `valide`, avec confirmation navigateur) → `cloturerPaieMensuelle`.
- Table **Historique des clôtures** (`listPaieClotures`) : Période, Statut, Bulletins, Validé (date), Clôturé (date).

## 4. Workflows standards

### 4.1 Générer et valider la pré-paie d'une période

1. `/rh/paie/prepaie`, choisir la période (AAAA-MM).
2. **Générer pré-paie** — pour chaque employé actif ayant un contrat actif : somme des heures pointées validées et des jours d'absence de la période, calcule le brut (`calculateBrutPaieMensuel`), puis cotisations/IRG (`calculatePaieDz`), et **insère ou met à jour** (`ON CONFLICT`) le bulletin — uniquement si son statut est encore `brouillon`/`exporte` (un bulletin `valide` ou `importe` n'est plus régénéré).
3. Ajouter d'éventuelles **primes variables** avant régénération (elles sont incluses dans `primesTotal`).
4. Vérifier chaque bulletin, exporter en **PDF** au besoin, puis **Valider** (passe `statut = 'valide'`, verrouillé si la période est déjà clôturée — `assertPeriodePaieModifiable`).
5. Une fois validé (ou importé de DLG), **comptabiliser en trésorerie** : crée une écriture `journal_caisse` (sortie = net) pour l'hôtel choisi et lie le bulletin (`tresorerieId`) — irréversible pour ce bulletin (contrôle « déjà comptabilisé »).

### 4.2 Échanger avec DLG PC PAIE

1. `/rh/paie/dlg` — configurer dossiers export/import et préfixe matricule (une fois).
2. **Exporter vers DLG** pour la période : récupère le ZIP généré, le décompresser puis dans DLG PC PAIE : *Fichier → Importer données depuis un dossier* (tables, salariés, variables).
3. Effectuer le calcul de paie dans DLG PC PAIE, exporter son résultat (`BULLETINS_{AAAAMM}.zip` ou `.csv`) dans le dossier import configuré.
4. **Importer depuis dossier** (ou **Choisir fichier ZIP/CSV** pour une sélection ponctuelle) — les bulletins reçoivent le statut `importe`.
5. Consulter le **journal des échanges** pour vérifier le nombre de lignes traitées et les éventuelles erreurs de matricule.

### 4.3 Exporter les déclarations légales

1. `/rh/paie/declarations`, choisir la période/année selon l'export.
2. Cliquer sur le bouton correspondant (DADS-U, DAS, CNAS, Virements, ANEM) — un fichier est généré et son chemin affiché.
3. Les exports CNAS/DAS/DADS-U ne portent que sur les bulletins au statut `valide` ou `importe` — valider la pré-paie avant d'exporter.

### 4.4 Clôturer la paie du mois

1. `/rh/paie/cloture`, sélectionner la période.
2. **Valider paie mensuelle** — bloqué si des bulletins sont encore en `brouillon` (message d'erreur explicite avec le nombre concerné) ; sinon tous les bulletins de la période passent à `valide`.
3. **Clôturer paie mensuelle** — nécessite le statut `valide` ; confirmation obligatoire. Une fois clôturée, **toute modification de la période est bloquée** côté serveur (`assertPeriodePaieModifiable` lève une erreur pour la génération de pré-paie, la validation de bulletin, la création de prime) : « Paie {période} clôturée — modification interdite. »

## 5. Règles métier DZ

Le moteur de calcul (`electron/services/rh-paie-dz-engine.ts`, fonctions pures testées dans `rh-paie-dz-engine.test.ts`) implémente les règles suivantes :

### 5.1 Brut mensuel, heures supplémentaires, retenues (`calculateBrutPaieMensuel`)

- **Base horaire mensuelle de référence** : `HEURES_MENSUELLES_REF = 173.33` h (référence 40 h/semaine, Loi 90-11).
- **Majoration heures supplémentaires** : `MAJORATION_HS = 1.5` (+50 %) appliquée au taux horaire (`brutBase / 173.33`) pour les heures travaillées au-delà de la référence.
- **Retenue absence sans solde** : `joursAbsenceNonRemuneree × (brutBase / 30)` (`JOURS_MOIS_REF = 30`).
- **Brut** = `brutBase + montantHs + primesTotal − retenueAbsence`, plancher à 0 (ne descend jamais sous zéro).

### 5.2 Cotisations CNAS et parafiscales patronales (`calculatePaieDz`, `calculateParafiscalesPatronales`)

- **CNAS salariale** : `CNAS_SALARIE_TAUX = 9 %` du brut.
- **CNAS patronale** : `CNAS_PATRON_TAUX = 26 %` du brut (affichée « pour information » sur le bulletin, à la charge de l'employeur).
- **Parafiscales patronales**, taux par défaut (`DEFAULT_PARAFISCAL_PARAMS`, surchageables par période via la table `rh_paie_params` — `getPaieParams()`) :
  - Accident du travail : 1,25 %
  - Assurance chômage : 1,5 %
  - Formation professionnelle : 1 %
- **Coût employeur** = `brut + cotisation patronale (26 %) + parafiscales patronales`.

### 5.3 IRG — barème progressif (`calculateIrg`)

- **Abattement** : `40 000 DZD + 1 000 DZD par enfant à charge`.
- **Base imposable** = `max(0, (brut − cotisation salariale 9 %) − abattement)`.
- **Barème par tranches** :
  | Base imposable | Taux / formule |
  |---|---|
  | ≤ 30 000 DZD | 23 % de la base |
  | ≤ 120 000 DZD | 6 900 + 27 % de la fraction au-delà de 30 000 |
  | > 120 000 DZD | 31 200 + 33 % de la fraction au-delà de 120 000 |
- **Net** = `brut − cotisation salariale (9 %) − IRG`.

### 5.4 SMIG et alertes de conformité

- `SMIG_DZD = 20 000` DZD, utilisé comme seuil d'alerte dans le tableau de bord Conformité DZ (`/rh/paie/conformite`, service `rh-conformite-dz.service.ts`) : tout contrat actif avec `salaire_brut < SMIG_DZD` déclenche une alerte « urgent ».

### 5.5 Bulletin de paie PDF — mentions légales

`electron/services/rh-bulletin-pdf.service.ts` génère un PDF par bulletin avec : en-tête « RÉPUBLIQUE ALGÉRIENNE DÉMOCRATIQUE ET POPULAIRE », identité employeur (raison sociale, adresse, n° employeur CNAS, NIS), période, matricule DLG, identité salarié (NSS, NIN, RIB), détail rémunération (salaire de base, heures sup, retenue absence, primes, brut imposable), retenues légales (CNAS salariale 9 %, IRG au barème en vigueur avec nombre d'enfants à charge, part patronale CNAS 26 % pour information), net à payer, et mention légale de conservation obligatoire (employeur + salarié).

### 5.6 Verrouillage de période (clôture)

Une fois `cloturerPaieMensuelle` exécuté, `assertPeriodePaieModifiable(periode)` interdit toute écriture sur les bulletins/primes de cette période — c'est le mécanisme central de conformité (traçabilité, non-modification a posteriori d'une paie clôturée).

### 5.7 Hors périmètre de cette fiche

Le moteur expose aussi `calculateStcDz` (indemnités de solde de tout compte : congés, préavis, licenciement selon le type de rupture) — utilisée par les endpoints `rh:rupture:*` (aperçu/traitement de rupture, certificat de travail, STC PDF), rattachés à la fiche employé (`EmployeFiche360.tsx`) plutôt qu'aux écrans de paie mensuelle décrits ici. Ce calcul est explicitement documenté comme **indicatif** dans le code (« validation expert-comptable requise »).

## 6. Interconnexions

- **Pointages et absences** (`/rh/temps/pointages`, `/rh/temps/absences`, `PointagesTab`/`AbsencesTab`) alimentent directement le calcul de la pré-paie (heures travaillées validées, jours d'absence approuvés) — voir [rh-productivite.md](rh-productivite.md#3-écrans--champs) pour la table des hubs.
- **Pointeuses/badgeuses** génèrent les pointages source « pointeuse » consommés par la pré-paie — voir [rh-recrutement-pointeuses.md](rh-recrutement-pointeuses.md).
- **Contrats** (`/rh/collaborateurs/contrats`) fournissent le salaire brut contractuel de référence pour chaque génération de pré-paie.
- **Comptabilisation trésorerie** d'un bulletin crée une écriture dans `journal_caisse` — voir [encaissements-tresorerie.md](encaissements-tresorerie.md).
- **Registres légaux** (`/rh/paie/registres`) et **Conformité DZ** (`/rh/paie/conformite`) partagent le même moteur de calcul et les mêmes informations légales employeur — voir aussi [conformite-hoteliere.md](conformite-hoteliere.md) et [fiscalite-dgi.md](fiscalite-dgi.md) pour les obligations fiscales connexes.
- Toute action de paie génère une entrée dans le **journal d'audit** (`writeAuditLog`, module `rh`) — voir [journalisation-tracabilite.md](journalisation-tracabilite.md).

## 7. Dépannage

- **« Aucun bulletin. Générez la pré-paie pour cette période. »** : aucun bulletin n'existe encore — cliquer sur « Générer pré-paie » (nécessite au moins un employé actif avec contrat actif).
- **« Paie {période} clôturée — modification interdite. »** : la période est clôturée (`RhPaieCloturePage`) ; aucune régénération, validation de bulletin ou création de prime n'est possible sur cette période — corriger via une régularisation sur une période ouverte si nécessaire.
- **« {N} bulletin(s) encore en brouillon. »** au moment de valider la paie mensuelle : valider ou régénérer individuellement les bulletins restants en brouillon avant de relancer la validation globale.
- **« Configurez le dossier export/import DLG PC PAIE. »** : renseigner les chemins dans le sous-onglet DLG avant tout export/import.
- **« Aucun export DLG trouvé pour {période}… » ou erreur de colonnes à l'import** : vérifier le nom de fichier attendu (`BULLETINS_{AAAAMM}.zip/.csv`) et les colonnes minimales `MATRICULE;BRUT;NET` (séparateur `;`).
- **Matricules DLG non reconnus à l'import** : le journal des échanges indique le nombre de lignes ignorées (statut `partiel`/`erreur`) — vérifier la correspondance `dlg_matricule` sur la fiche employé.
- **Bouton « Comptabiliser en trésorerie » absent** : le bulletin doit être `valide` ou `importe` et ne pas avoir déjà de `tresorerieId` (un bulletin ne peut être comptabilisé qu'une fois).
- **Export CNAS/DAS/DADS-U vide** : ces exports ne portent que sur les bulletins `valide`/`importe` — valider la pré-paie d'abord.
- **Écart entre le bulletin PDF et le montant attendu** : contrôler `enfants_charge` sur la fiche employé (impacte l'abattement IRG) et la présence de primes non prises en compte avant la génération de la pré-paie.
