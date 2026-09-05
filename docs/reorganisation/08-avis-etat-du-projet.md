# 08 — Avis d'architecte sur l'état du projet

**Destinataire :** propriétaire du projet
**Branche analysée :** `reorg/phase-1` (révision `da196a7`)
**Date :** 4 septembre 2026
**Nature :** avis motivé, en lecture seule. Aucun fichier du dépôt n'a été modifié, aucun build ni test n'a été lancé.

> Ce document tranche entre sept analyses sectorielles et trois contre-expertises. Chaque affirmation
> décisive a été revérifiée directement dans le code de la branche. Là où le dossier d'analyse s'est
> trompé, je le dis en annexe, avec la preuve.

---

## 1. Avis en une page

**Ce que ce logiciel est aujourd'hui.** Un moteur métier hôtelier sérieux, écrit par quelqu'un qui
connaît le métier, dont personne n'a encore jamais tenu une heure de travail réelle. Le domaine est
riche et documenté : le refus de départ tant qu'un folio n'est pas soldé est porté par le Domain
(`src/RaqmiSystem.Domain/Lodging/Folio.cs`), l'anti-survente est prouvé contre du vrai PostgreSQL
(`tests/RaqmiSystem.Tests/Postgres/PostgresReservationConcurrencyTests.cs`), le night audit est
idempotent par index unique filtré (`FolioChargeConfiguration.cs`,
`ux_folio_charges_folio_id_source_reference`), la partie double est un invariant redoublé par des
contraintes SQL. 70 des 99 configurations EF posent au moins un CHECK, les 111 colonnes monétaires
sont toutes en `numeric(p,s)` explicite, les 233 colonnes temporelles sont toutes en `timestamptz`.
Ce n'est pas une maquette.

**Ce qu'il n'est pas.** Un produit livrable. Le moteur est branché sur rien à un bout et sur personne
à l'autre.

*Sur rien* : le folio ne devient jamais une facture — `Folio.AttachInvoice()` existe (ligne 161) et
n'a **aucun appelant** dans tout le dépôt, seul MICE facture (`MiceService.cs:704`). Aucun document
ne s'imprime : 21 paquets dans `Directory.Packages.props`, **zéro** bibliothèque PDF, et une seule
implémentation d'impression dans tout le produit (`MainWindow.xaml.cs:814`, la grille des recettes).
L'argent encaissé au comptoir ne crée aucun `CashReceipt`. Aucun module ne déverse d'écriture
comptable : le coffre-fort comptable est excellent et il est vide.

*Sur personne* : aucun utilisateur n'est rattaché à un établissement — `SecurityClaimTypes.cs` ne
déclare qu'une constante, `"permission"` ; `hotelUnitCode` est un paramètre choisi par le client. Et
surtout, **le client lourd ne renouvelle jamais son jeton** : `RaqmiApiClient.cs` ne stocke que
`accessToken` (ligne 27), jette le `RefreshToken` renvoyé par le serveur, ne lit jamais `ExpiresAt`,
et ne traite jamais un 401 (`git grep Unauthorized -- src/RaqmiSystem.Desktop` → 0). Avec
`JwtOptions.AccessTokenMinutes = 60`, **toute session de travail meurt à 60 minutes**, en plein
service, sans message exploitable et sans autre issue que fermer et rouvrir l'application. Aucun des
sept analystes ne l'a vu ; l'un d'eux a même classé la mécanique de rotation des refresh tokens —
qui est du code que rien n'appelle — parmi les forces du produit.

**Deux constats de cadrage que le dossier a manqués.** D'abord, la CI ne s'exécute **jamais** sur
cette branche : `.github/workflows/dotnet.yml` ne se déclenche que sur `push` vers `main`, sur tag
`v*.*.*` et sur `pull_request`. Les 947 tests, le gate PostgreSQL et la matrice RBAC n'ont donc
jamais tourné sur l'état que vous me demandez d'évaluer. Ensuite, une installation on-premise faite
**exactement selon votre documentation** démarre sans aucune sauvegarde possible : les migrations
créent 19 schémas, `deploy/postgres/create-app-role.sql` n'en accorde que 15 — `crm`,
`housekeeping`, `hr` et `kpi` manquent, soit 25 des 104 tables (dont les 11 tables RH), et `pg_dump`
tourne sous ce rôle. Le RPO annoncé de 24 h est une fiction : le RPO réel est « tout depuis
l'installation », et `/health/database` répond `healthy` pendant ce temps.

**Ma note.** 4/10 en tant que produit livrable. 7/10 en tant que socle d'ingénierie. Cet écart est la
seule chose importante de ce document : vous n'avez pas un problème de qualité, vous avez un problème
de **dernier mètre** — aux deux extrémités de la chaîne.

**Si je n'avais qu'une phrase :** *vous avez construit un très bon moteur d'ERP hôtelier et vous
n'avez encore construit ni la sortie (aucun document, aucune facture, aucune écriture comptable), ni
l'entrée (aucun périmètre, aucun import, aucune session qui dure plus d'une heure) — et tant que ces
deux extrémités manquent, la qualité du moteur ne se voit nulle part.*

---

## 2. Ce qui est solide

Ce ne sont pas des politesses : ce sont des acquis que je ne referais pas et sur lesquels il faut
construire.

**1. Le noyau de partie double est du travail de professionnel.**
`src/RaqmiSystem.Domain/Accounting/JournalEntry.cs` : `Post()` exige ≥ 2 lignes et
`TotalDebit == TotalCredit` ; `JournalEntryLine` impose débit XOR crédit ; les deux règles sont
redoublées en base (`ck_journal_entry_lines_debit_credit_exclusive`,
`ck_journal_entries_posted_balanced`). Aucun état « supprimé », aucune route DELETE, correction
uniquement par contre-passation, avec `ux_journal_entries_reverses_entry_id` qui interdit deux
extournes concurrentes. **Pourquoi ça compte :** c'est la partie la plus chère à réécrire d'un ERP et
elle est faite. Attention cependant : elle ne vaut que pour le noyau, pas pour le socle SCF posé
par-dessus (voir §7).

**2. Le cœur PMS porte de vraies règles de réception, dans le Domain.**
Refus de départ à solde non nul sur **tous** les folios du séjour, relu en transaction Serializable
(`LodgingService.Stay.cs`, `CheckOutAsync`) ; night audit rejouable par clé de geste déterministe
(`FolioCharge.SourceReference` + index unique filtré) ; pont automatique PMS → gouvernante
(`LodgingService.Housekeeping.cs`), avec la règle explicite que le `RoomBlock` fait foi sur
l'inventaire. **Pourquoi ça compte :** ces trois règles sont exactement celles qu'un PMS mal écrit
rate, et elles vivent dans le domaine, pas dans un écran.

**3. La concurrence est traitée au bon niveau, et prouvée.**
34 sites `catch (Exception e) when (e.IsSerializationFailure())`,
`DbUpdateExceptionExtensions.cs` qui distingue 23505 (avec nom de contrainte) de 40001/40P01,
revendication atomique du brouillon par `UPDATE` conditionnel avant comptabilisation
(`AccountingService.TryClaimDraftEntryAsync`). Le TOCTOU est fermé côté base, pas en mémoire.
**Pourquoi ça compte :** c'est ce qui sépare un logiciel mono-poste d'un logiciel de réception.

**4. Le schéma de base est très au-dessus de la moyenne.**
111 colonnes `numeric(p,s)` explicites, **zéro** `numeric` sans précision ; 233 `timestamptz`, **zéro**
`timestamp without time zone` ; migrations de reprise écrites pour être rejouables
(`20260901130154_WavePmsFrontOffice.cs`, six `Sql` de backfill filtrés sur les lignes encore vides).
**Pourquoi ça compte :** aucune dérive de centimes, aucune nuitée décalée par un fuseau au niveau du
stockage.

**5. Le gate PostgreSQL et le garde anti-dérive du modèle sont les bons gardes.**
`tests/RaqmiSystem.Tests/Postgres/PostgresMigrationTests.cs` : migration depuis zéro, depuis N-1,
retrait puis réapplication de la dernière migration, et `HasPendingModelChanges()` avec la commande
corrective dans le message d'échec. **Pourquoi ça compte :** c'est la seule parade au fait que 83 %
de la suite tourne sur SQLite. Il ne lui manque que de s'exécuter (§3a).

**6. L'installation on-premise est un vrai produit d'installation.**
`deploy/onpremise/install-server.ps1` : contrôle d'élévation, secrets en `Read-Host -AsSecureString`
jamais en argv, clé JWT `RandomNumberGenerator` 64 octets, ACL `icacls` posée **avant** l'écriture du
secret, pare-feu `Domain,Private` seulement, wrapper `.psql` temporaire pour ne pas passer le mot de
passe en ligne de commande. **Pourquoi ça compte :** c'est rare et c'est la moitié du travail
d'industrialisation — l'autre moitié (mise à jour, supervision, restauration) manque entièrement.

**7. Le socle d'authentification est bien écrit.**
PBKDF2-SHA256 310 000 itérations, sel CSPRNG par mot de passe, `FixedTimeEquals`, itérations stockées
dans le hash ; parité temporelle anti-énumération réellement implémentée (`DummyPasswordHash`) ;
rotation des refresh tokens par `ExecuteUpdateAsync` conditionnel, résistante à la course ; gardes
anti-verrouillage administratif en transaction Serializable. **Pourquoi ça compte :** le jour où le
client lourd appellera enfin `/auth/refresh`, la mécanique serveur sera correcte.

**8. Le projet est intellectuellement honnête sur lui-même.**
`docs/reorganisation/03-cartographie-cible.md` écrit « Alimentation du moteur comptable | Absent » ;
`ReportCatalog.cs:22` écrit « PDF and Excel exports are deliberately OUT OF SCOPE » ;
`SyncSupervisionService.cs:19` écrit « AUCUNE FILE DE REJEU » et explique pourquoi ; le garde de
readiness aboutit à **0 écran** Production Ready et refuse de mentir sur ce résultat. **Pourquoi ça
compte :** je peux faire confiance à vos documents, ce qui n'est pas courant. C'est un actif
managérial réel.

---

## 3. Ce qui manque, hiérarchisé

Effort = charge d'un développeur seul, du niveau observé dans le dépôt. **« Dép. »** = dépendances.

### (a) BLOQUANT pour ouvrir chez un premier client

| # | Quoi | Pourquoi | Effort | Dép. |
|---|------|----------|--------|------|
| A1 | **Renouvellement de session dans le client lourd** : stocker `RefreshToken`, lire `ExpiresAt`, renouveler avant expiration, traiter le 401 par un retour à l'écran de connexion avec message. | `RaqmiApiClient.cs:27` ne garde que `accessToken` ; `LoginAsync` jette `login.RefreshToken` ; zéro traitement de `HttpStatusCode.Unauthorized` ; `AccessTokenMinutes = 60`. **Chaque poste meurt à 60 minutes**, huit fois par service de 8 h, sur l'action en cours. Aucune recette, aucun smoke sur 30 écrans, aucune journée d'exploitation ne peut aller au bout. | **jours** | aucune |
| A2 | **Les 4 `GRANT` manquants** (`crm`, `housekeeping`, `hr`, `kpi`) + un test PostgreSQL qui échoue si une migration crée un schéma non accordé. | 19 schémas créés, 15 accordés → 25 des 104 tables inaccessibles. `pg_dump` tourne sous `raqmi_app` : **zéro sauvegarde depuis le jour 1**. L'écran d'accueil lui-même interroge `/housekeeping/board`, `/hr/absences`, `/hr/payroll/periods` (`HomeWorkQueueCatalog.cs`) : le produit s'ouvre en erreur. Et `/health/database` répond `healthy`. | **jours** | aucune |
| A3 | **`deploy/onpremise/update-server.ps1`** idempotent : détection d'installation existante, sauvegarde pré-mise-à-jour, conservation de `api-previous\`, migrations, vérification `/health`, rollback automatique. | `git ls-tree -- deploy` : 10 fichiers, **aucun script de mise à jour**. Relancer `install-server.ps1` génère un nouveau mot de passe applicatif, l'écrit dans `raqmi.env.ps1` (l. 325-335) **avant** que `create-app-role.sql` refuse de le changer sur un rôle existant : le fichier de configuration porte alors un mot de passe qui n'existe nulle part, et `start-api.ps1` le dot-source à chaque démarrage. **La première mise à jour livrée tue le serveur du client.** | **semaines** | A2 |
| A4 | **Facturation du folio** : `POST /lodging/reservations/{id}/folios/{folioId}/invoice`, reprenant le patron déjà écrit dans `MiceService.cs:704`. | `Folio.AttachInvoice()` (l. 161) n'a aucun appelant ; la colonne `InvoiceId` reste NULL à vie ; aucune route `/lodging/**/invoice` n'existe. Un hôtel qui ne peut pas remettre de facture ne peut pas vendre une nuit. La méthode et la colonne existent : c'est le trou le plus absurde du produit. | **semaines** | B1 (numérotation par unité) |
| A5 | **Chaîne documentaire imprimable** : moteur de rendu, facture, note de séjour, reçu, bon de commande, bulletin de paie. | Aucune dépendance PDF parmi les 21 paquets ; une seule impression dans tout le produit (`MainWindow.xaml.cs:814`, la grille des recettes) ; aucun stockage de fichier (`IFormFile`, `byte[]`, blob → 0 dans Api/Domain/Application). Ce n'est pas un lot, c'est un **sous-système** : générer, archiver, remettre. Et il faut décider où le rendu vit — aujourd'hui le seul candidat est un client WPF sans un seul test. | **mois** | A4 |
| A6 | **Périmètre utilisateur ↔ unité** : entité d'affectation, claim de périmètre, filtrage **imposé dans les services**, tests d'isolation par module. | `SecurityClaimTypes.cs` = une seule constante ; `JwtTokenService` n'émet que rôle et permission ; `hotelUnitCode` est un paramètre client sur 431 routes. Le lot existe **non commité** dans un worktree. Chaque écran livré avant ce lot sera à reprendre. Le chiffrage « semaines » du dossier est optimiste d'un facteur 2 à 3. | **1-2 mois** | aucune (à faire avant tout nouvel écran) |
| A7 | **Chaîne d'encaissement au comptoir** : session de caisse par caissier et par shift, règlement de folio créant un `CashReceipt` réel, main courante. | Un règlement est une ligne de folio dont la « pièce de trésorerie » est un `string?` libre jamais vérifié (`AddFolioChargeRequest.cs`). `git grep "new CashReceipt"` → uniquement `TreasuryService.cs` ; aucune référence à `CashReceipt` dans `Infrastructure/Lodging/`. La recette du jour est retapée à la main. | **semaines** | A6 |
| A8 | **Timeout HTTP explicite (10-15 s) côté client** + message de panne réseau immédiat. | `MainWindow.xaml.cs:51` : `new HttpClient()` sans `Timeout` → 100 s par défaut, ce que le commentaire de la ligne 1147 confirme. Sur une coupure pendant un check-in, la réception attend deux minutes. **Correction d'une ligne.** | **jours** | aucune |
| A9 | **Horloge métier au fuseau algérien** : `TimeZoneInfo` « Africa/Algiers » (UTC+1, pas d'heure d'été) injecté via `TimeProvider`. | 17 occurrences de `DateOnly.FromDateTime(DateTime.UtcNow)` côté serveur, dont `LodgingService.Operations.cs:39` qui résout la journée d'exploitation, et `BillingService.cs` qui en tire l'**année de la série de factures**. Entre 00 h et 01 h locales — l'heure exacte du veilleur — le serveur se trompe de journée ; une facture émise le 1<sup>er</sup> janvier avant 1 h reçoit un numéro de la série de l'année précédente. En prime, `HomeDayWindow.cs` utilise `TimeZoneInfo.Local` : deux horloges métier différentes coexistent. | **jours** | aucune |
| A10 | **Culture décimale unique et explicite à la saisie.** | Motif `TryParse(CurrentCulture) \|\| TryParse(InvariantCulture)` dans 13 vues (`TreasuryView.xaml.cs:1344`…). Sur un poste fr-FR, « 1,500 » vaut 1,5 ; sur un poste en-US, 1500. Les deux réussissent en silence. Deux caissiers du même hôtel encaissent des montants différents à partir de la même frappe. Invisible en CI (qui tourne en invariant). | **jours** | aucune |
| A11 | **CI sur `reorg/**`** et `publish-api` dépendant de `postgres-integration`. | `dotnet.yml` : `push: branches [main]`, tags, `pull_request` — la branche n'a **jamais** été construite ni testée. Et `publish-api: needs: build-core` seulement : une image peut partir sur ghcr.io avec le gate PostgreSQL rouge. **Une heure de travail**, sans quoi tout le reste est déclaratif. | **jours** | aucune |
| A12 | **Sauvegarde hors machine + restauration prouvée** : copie sur second support, chiffrement du dump, `restore-raqmi.ps1`, restauration de vérification hebdomadaire horodatée. | Les dumps vivent dans `C:\RaqmiSystem\backups`, même disque que la base ; aucun `robocopy`, aucun chiffrement, aucune somme de contrôle, aucun `OnFailure=` ; `git ls-tree | grep -i restore` → **aucun fichier**. RTO jamais mesuré. Le dump contient en clair les salaires, les NIN/NSS/RIB et les données clients. | **semaines** | A2 |

### (b) BLOQUANT pour la conformité légale algérienne

| # | Quoi | Pourquoi | Effort | Dép. |
|---|------|----------|--------|------|
| B1 | **Identité fiscale et numérotation par établissement** : NIF/RC/AI/NIS portés par `HotelUnit`, séquence de facture cloisonnée par unité et par exercice. | `ApplicationSettings` est un **singleton** (`SingletonKeyValue = "GLOBAL"`) injecté tel quel dans `CaptureIssuerSnapshot` ; `NextIssueSequenceAsync` filtre sur `IssuedYear` seul. Chaque hôtel est un établissement distinct : une série partagée produit des trous inexplicables dans chaque livre de ventes, et une facture au mauvais NIF n'est pas conforme. | **semaines** | A6 |
| B2 | **Identité du client hébergé** + registre de police. | `Reservation.GuestName` est un `string?` nullable ; `GuestProfile` n'a ni nationalité ni document ; `PassportNumber\|Nationality\|IdentityDocument` → 0 résultat sur `src`. « Fiches police » n'existe que comme libellé de navigation (`ModuleCatalog.cs:252`, statut Planifié). Obligation légale de l'hôtelier, contrôlée. Préalable à la taxe de séjour par personne. | **semaines** | A5 |
| B3 | **Facture d'avoir** numérotée dans sa propre série, **et suppression de l'annulation d'une facture émise**. | `Invoice.Cancel()` accepte `Status == Issued`, la route `POST /invoices/{id}/cancel` existe, et `ReportingService.BuildInvoicedVatAsync` ne compte que les `Issued` : **la TVA collectée d'une période déjà déclarée est réductible a posteriori, sans pièce corrective**. Irrégulier et vecteur de fraude direct. `Folio.AddCharge()` renvoie déjà l'utilisateur vers « un avoir » qui n'existe pas. | **semaines** | A4 |
| B4 | **Droit de timbre** sur les règlements en espèces : paramètre par unité, calcul sur la facture, ligne dédiée. | `git grep -i "timbre"` → uniquement `docs/legacy/`. Obligation sur tout règlement en espèces : la facture est non conforme **dès le premier client qui paie cash**, c'est-à-dire dès le premier client. | **jours** | A4 |
| B5 | **Taxe de séjour** : taux par unité et catégorie, pose automatique par nuitée et par personne, déclaration communale. | Nuance contre le dossier : `ChargeKind.Tax` **est** câblé — `ExtraItem.cs:43` et `:151` l'autorisent comme nature d'article extra. Une taxe est donc posable **à la main** dès aujourd'hui. Ce qui manque est l'**automatisme** et la **déclaration**, pas la mécanique. Le lot est plus petit qu'annoncé. | **semaines** | B2 |
| B6 | **Fermeture du trou de numérotation comptable + route de création de période.** | Double défaut : (i) `RequireOpenPeriodAsync` fait `if (!await dbContext.AccountingPeriods.AnyAsync(ct)) return null;` — donc on comptabilise sans période, et `AssignDefinitiveNumberAsync` fait `if (period is null) return;` — l'écriture est postée **sans numéro de pièce, définitivement** ; (ii) **il n'existe aucune route de création de période** : `IAccountingCoreService` a `ListPeriodsAsync` et `ClosePeriodAsync`, pas de `CreatePeriodAsync`, et `AccountingEndpoints` n'expose rien. Un exercice créé avec `CreateMonthlyPeriods=false` est **définitivement inutilisable**, réparable uniquement en SQL direct en production. Ce n'est pas un correctif, c'est une fonction manquante. | **jours** (+ semaines pour la fonction) | aucune |
| B7 | **Correction de la balance auxiliaire** (chiffre faux affichable aujourd'hui) et **transaction sur le lettrage**. | `AccountingCoreService.GetAuxiliaryBalanceAsync` somme **toutes** les allocations rattachées aux lignes du tiers, les deux côtés confondus, sans filtre de date : un lettrage de 1 000 au débit contre 1 000 au crédit donne `Reconciled = 2 000`. Puis `Outstanding = Math.Max(0, |balance| - matched)`. Un client à 1 500 de factures et 1 000 d'encaissements, lettré à 1 000, doit 500 — le rapport affiche **« rien à recouvrer »**. C'est le seul rapport de recouvrement du produit et il est faux dès le premier lettrage partiel. Et `ReconcileAsync` est la **seule opération financière du dépôt sans aucune transaction** (0 `BeginTransactionAsync` dans ce fichier, contre 35 ailleurs), avec un index non unique sur `JournalEntryLineId` : deux lettrages simultanés passent tous les deux. | **jours** | aucune |
| B8 | **Refonte du calcul IRG** depuis le barème publié, **et réécriture de ses tests**. | `AlgerianPayrollEngine.cs` : `incomeTaxBase = Math.Max(0, taxableGross - employeeSocial - abatement)` puis barème. L'abattement IRG salaires s'applique à l'**impôt**, avec plancher et plafond mensuels, pas à la base. Le barème par défaut n'a **aucune tranche à 0 %** (`{30 000 → 23 %, 120 000 → 27 %, ∞ → 33 %}`) : ce n'est que l'abattement forfaitaire de 40 000 retranché de la base qui simule la tranche exonérée. Et ce **n'est pas corrigeable par paramétrage** : `PayrollParameters` n'a ni taux, ni plancher, ni plafond d'abattement sur l'impôt — contrairement à ce que promet `PayrollParameterSet.cs:95` (« confirming it is a data edit, not a code change »). Aggravant : les tests les mieux écrits du dépôt encodent la formule fausse dans leurs commentaires (`AlgerianPayrollEngineTests.cs`, « IRG base = 60 000 - 5 400 - 40 000 = 14 600 »). L'employeur est le redevable légal de la retenue : c'est la seule faiblesse du dossier qui coûte de l'argent **rétroactivement**. | **mois** | aucune |
| B9 | **Déclaration TVA / G50** : TVA déductible (cycle achat avec facture fournisseur), TVA nette, G50 mensuel. | `PurchaseOrderLine` ne porte **aucun champ TVA** ; `SupplierInvoice` → 0 résultat. Sans TVA déductible, le G50 est incalculable même à la main. Un hôtel algérien dépose un G50 tous les mois. | **mois** | A4, B3 |
| B10 | **Déversement comptable automatique** : schémas d'écriture par événement métier, avec clé d'idempotence et lien pièce comptable ↔ pièce opérationnelle. | `IAccountingService` n'est référencé que par `AccountingEndpoints.cs` et son DI. `BillingService` n'injecte que `dbContext`, `IAuditLogWriter`, `IApplicationSettingsService`. Attention au faux ami : `AddJournalEntry` du PMS écrit un `ReservationEvent`, pas une écriture comptable. Non bloquant le jour de l'ouverture, **bloquant à la première clôture mensuelle**. | **mois** | A4, A7 |
| B11 | **États financiers SCF** (bilan, TCR, flux, variation des capitaux propres, annexe) et **clôture d'exercice réelle** (détermination du résultat, affectation, à-nouveaux). | `AccountingEndpoints` n'expose que `/trial-balance`, `/general-ledger/{code}`, `/auxiliary-balance`. `CloseFiscalYearAsync` ne fait qu'un changement de statut : sans à-nouveaux, **le bilan de la deuxième année est faux par construction**. | **mois** | B10 |
| B12 | **Inaltérabilité et conservation** : exclure les événements comptables et de facturation de la purge d'audit, rétention à 10 ans, `REVOKE UPDATE, DELETE` sur `audit.audit_logs` et `accounting.journal_entries`. | `POST /audit/purge` est la **seule route mutante des 431 qui n'écrit aucune entrée d'audit** — la seule opération non traçable du produit est précisément celle qui détruit la traçabilité. Elle accepte `olderThanDays >= 1` et purge sans filtre de domaine. Et `AuditRetentionDays`, exposé dans l'écran de paramétrage, **n'est lu par aucun code de production** : il n'existe aucun `IHostedService` dans l'application. | **semaines** | aucune |
| B13 | **Module immobilisations** (classe 2, plans d'amortissement, dotations, tableau pour l'annexe). | Aucun répertoire `Assets`/`Immobilisations` ; `amortissement` n'apparaît que dans des libellés de KPI. Un hôtel est une activité capitalistique : sans dotations, le résultat comptable **et** fiscal sont faux. | **mois** | B10, B11 |
| B14 | **Dispositif 18-07 opérationnel** : registre des traitements, durées de conservation avec purge automatique, droits d'accès/rectification/effacement exposés comme fonctions, journalisation des accès en **lecture** aux données personnelles. | Le code montre une conscience de 18-07 (`Employee.cs`, `GuestProfile.cs`) mais n'implémente aucune obligation. Point précis que le dossier a manqué : `ListEmployeesAsync` **n'écrit aucune entrée d'audit** et porte `ActiveContractGrossSalary` (`EmployeeSummaryResponse.cs`) — un seul GET rend la grille salariale complète, unité par unité, sans laisser de trace. C'est la donnée la plus explosive socialement d'un groupe hôtelier. | **mois** | A6 |

### (c) Nécessaire pour exploiter sans l'éditeur sur place

| # | Quoi | Pourquoi | Effort | Dép. |
|---|------|----------|--------|------|
| C1 | **Versionnage réel du produit** : `<VersionPrefix>` alimenté par le tag Git, propagé dans l'assembly desktop, dans `/health` et dans `MyAppVersion` de l'`.iss`. | Recherche exhaustive de `<Version>`, `<VersionPrefix>`, `<AssemblyVersion>`, `<InformationalVersion>` sur tous les `.csproj` et `.props` : **zéro résultat**. Tous les postes remonteront « 1.0.0 » alors que `Workstation.cs` qualifie ce champ de « donnée la plus utile que ce poste transmette ». Le registre des postes est aveugle par construction. | **jours** | aucune |
| C2 | **Supervision qui alerte** : enregistrer la tâche `check-health` (elle est copiée, jamais enregistrée), canal d'alerte sortant sur API arrêtée / sauvegarde en retard > 26 h / disque < 10 %. | `install-server.ps1` n'enregistre que deux tâches (API, sauvegarde). `smtp\|webhook\|alert` dans `deploy/` → 0. `BackupPolicy.IsOverdue` calcule correctement le retard — il faut qu'un humain ouvre un écran pour le voir. Une sauvegarde en échec silencieux pendant trois semaines est le scénario classique. | **semaines** | A2, A12 |
| C3 | **Journal d'accès HTTP + identifiant de corrélation + gestionnaire d'exception global.** | `UseSerilogRequestLogging`, `UseExceptionHandler`, `IExceptionHandler`, `AddProblemDetails`, `CorrelationId` → **0 résultat** sur `src`. En face : 450 `ArgumentException` dans 126 fichiers et 153 `InvalidOperationException` dans 38 fichiers. Quand une réceptionniste dit « ça a planté vers 10 h 15 », rien ne permet de retrouver l'appel. | **jours** | aucune |
| C4 | **Paquet de diagnostic à distance** : un ZIP horodaté (logs N jours, health, inventaire des sauvegardes, version, migrations appliquées), secrets exclus. | Aucun OpenTelemetry, aucun `/metrics`, aucun `AddHealthChecks`, aucun `support-bundle`. L'éditeur ne peut obtenir **aucune** information d'un site sans s'y rendre. | **semaines** | C1, C3 |
| C5 | **Runbook d'exploitation** (`docs/exploitation.md`) : 10 pannes prévisibles, symptôme → diagnostic → action, rollback complet, escalade. | `docs/deployment-onpremise.md` est excellent et s'arrête au jour J. La seule ligne de rollback suppose qu'on a gardé l'exe précédent — l'installeur l'écrase (`dotnet publish -o $apiDir`). | **semaines** | A3 |
| C6 | **Procédure de secours papier pour la réception** (fiche d'arrivée, ressaisie, qui décide de basculer, comment on vérifie qu'on n'a pas saisi deux fois). | Le refus de la file de rejeu est le **bon** choix technique (`SyncSupervisionService.cs` : « Un encaissement en double est un incident comptable ; une ressaisie n'en est pas un »), mais il déplace la charge sur l'humain — et rien n'a été écrit pour cet humain. | **jours** | aucune |
| C7 | **Chaîne de livraison du client lourd** : job CI produisant l'installeur **signé** (SignTool), artefact versionné, URL d'API posée par l'installeur. | `docs/deployment.md` admet « There is no CI job for this yet » ; l'`.iss` n'a aucune directive de signature (SmartScreen à chaque poste) ; l'URL de l'API se pose machine par machine. Sur 15-20 postes, chaque livraison est une journée sur site. | **semaines** | C1 |
| C8 | **Garde de compatibilité client ↔ serveur** : version minimale déclarée par le serveur, refus ou avertissement fort côté client. | Le client envoie sa version au heartbeat, aucune route ne la vérifie. Combiné à C1 et C7, le scénario « poste oublié pendant deux livraisons » est certain et invisible. | **jours** | C1 |
| C9 | **Sauvegarde et rotation de la configuration serveur** (`raqmi.env.ps1`) : export chiffré à côté des dumps, procédure de rotation de la clé JWT et du mot de passe applicatif. | Ces secrets sont générés une fois et **jamais affichés** ; `backup-raqmi.ps1` ne sauvegarde que la base. Restaurer un dump sur une machine neuve **ne remonte pas le service**. | **jours** | A12 |
| C10 | **Import de données et soldes d'ouverture.** | `import\|bulk\|batch` dans les 33 fichiers d'endpoints → **0 résultat** ; `OpeningBalance\|à-nouveau` → 0. Et la date d'arrivée d'un walk-in n'est pas saisissable (`LodgingService.Reservations.cs` : `var arrival = businessDay.HasClosing ? businessDay.Date : today;`) : **impossible de reprendre les clients déjà en maison le jour de la mise en service**. La paie demandera 150 salariés × 30 jours = 4 500 pointages saisis un par un chaque mois. | **mois** | B6 |
| C11 | **Exigences matérielles** : onduleur, disques en miroir, dimensionnement, décision explicite sur la reprise (PC de secours ou indisponibilité contractualisée). | `docs/deployment-onpremise.md` admet le point de défaillance unique mais n'exige rien. Sur un site algérien, un arrêt brutal en écriture PostgreSQL n'est pas théorique. | **jours** | aucune |
| C12 | **TLS obligatoire** en mode on-premise + limitation de débit sur `/auth/login` et `/auth/refresh`. | `docs/deployment-onpremise.md:202` : « HTTP on the LAN, no TLS » ; `UseHttpsRedirection\|UseHsts\|AddRateLimiter` → 0. Mots de passe et Bearer en clair sur un LAN d'hôtel rarement séparé du Wi-Fi de service ; et chaque tentative de connexion coûte un PBKDF2 310 k, donc la route est aussi un amplificateur de déni de service. | **jours** | aucune |
| C13 | **Documentation utilisateur du produit actuel** : jouer `tools/generate-guide.ps1`, réécrire les guides des trois rôles du pilote. | `docs/documentation-index.md` ne liste que du technique en « Documentation active » ; les 12 guides par rôle sont des imports legacy explicitement marqués « architecture Electron/SQLite ». `docs/guide/` n'existe pas sur la branche alors que l'outillage est écrit. | **semaines** | A1 |

### (d) Nécessaire pour tenir dans la durée

| # | Quoi | Pourquoi | Effort | Dép. |
|---|------|----------|--------|------|
| D1 | **Tests de garde d'architecture en CI** (NetArchTest ou équivalent) + `TreatWarningsAsErrors=true` sur Domain et Application + couverture avec plancher. | `NetArchTest\|ArchUnit` → 0 ; `Directory.Build.props` met `TreatWarningsAsErrors=false` ; `coverlet.collector` est référencé et **jamais invoqué**. Les interdictions R12/R13 sont des vœux. **Meilleur rapport effort/effet de tout le rapport.** Nuance contre le dossier : les analyseurs .NET intégrés **sont** activés (`AnalysisLevel=latest`) — le problème n'est pas leur absence, c'est qu'ils n'ont aucune dent. | **jours** | A11 |
| D2 | **Clé d'idempotence** sur les routes d'écriture P0 + politique de reprise bornée sur les abandons 40001. | Le dépôt le documente lui-même comme danger connu (`ClientFailureBuffer.cs:12`, `SyncEndpoints.cs:21`). `EnableRetryOnFailure` → 0 résultat : chaque contention normale de PostgreSQL remonte à l'écran du réceptionniste. Le lot existe **non commité** dans un worktree. Préalable technique à l'outbox et au Posting Engine. | **semaines** | A6 |
| D3 | **Durcir en base les invariants d'argent** : `total_debit = SUM(lignes.debit)`, ≥ 2 lignes, `total_incl_vat = total_excl_vat + total_vat`, `Issued ⇒ number IS NOT NULL AND issuer_nif_snapshot IS NOT NULL`. | `InvoiceConfiguration.cs` pose **un seul** CHECK (le statut), alors que `FolioChargeConfiguration.cs` en pose cinq. La pièce juridiquement opposable du produit est protégée par une politesse de service, sur une base où `raqmi_app` a UPDATE et DELETE sur le schéma `finance`. | **semaines** | aucune |
| D4 | **Contraintes de base sur le module RH**, au niveau de la comptabilité. | 11 fichiers `HumanResources/*Configuration.cs`, **0** `HasCheckConstraint`, contre 70/99 ailleurs. Pas d'énumération de statut, pas de `brut ≥ net`, pas d'immutabilité d'un bulletin validé. À rapprocher de A2 (schéma `hr` non accordé) et B8 (IRG faux) : **trois défauts indépendants convergent sur la même donnée**, la plus sensible juridiquement après le grand livre. Les traiter dans trois lots séparés garantit qu'aucun ne sera priorisé. | **semaines** | A2, B8 |
| D5 | **Une vraie couche Application** (cas d'usage derrière les 45 interfaces) et découpage des cinq classes-monstres. | `src/RaqmiSystem.Application` : 420 `sealed record`, 45 interfaces, **0 cas d'usage**. Toute la logique vit dans Infrastructure, soudée au `RaqmiDbContext` unique de 99 DbSets référencé par 38 services. `LodgingService` = 377 Ko sur 15 partiels — un seul type, un seul état, une seule surface de test. Et `docs/architecture.md` décrit une couche qui n'a jamais été écrite : il **envoie activement** le nouvel arrivant au mauvais endroit. | **mois** | D1 |
| D6 | **Architecture de présentation WPF** (ViewModels + commandes) + projet `RaqmiSystem.Desktop.Tests`. | 1,09 Mo de code-behind, 1,69 Mo de XAML, 67 167 lignes, et le projet de tests **ne référence même pas** Desktop. Nuance contre le dossier : « zéro ICommand » est faux au sens WPF — `MainWindow.xaml:26-28` et `MainWindow.Shortcuts.cs` utilisent le commanding routé pour les raccourcis. Enjeu réel : c'est ce client qui portera les pièces opposables (A5). | **mois** | A5 |
| D7 | **Contrat d'API publié** (OpenAPI versionné) + découplage binaire du client. | `Swashbuckle`, `AddOpenApi`, `MapOpenApi` → 0. Le Desktop référence directement `RaqmiSystem.Application` : aucune mise à jour décalée sur site n'est possible, et le Booking Engine n'a aucune surface contractuelle. | **semaines** | D5 |
| D8 | **Tenue de la volumétrie** : table de solde courant par (dépôt, article), partitionnement de `audit_logs` / `reservation_events` / `stock_movements`, virtualisation du tape chart, pagination des listes. | `InventoryService` réagrège **tous** les mouvements sous Serializable à chaque sortie. Le tape chart dessine un `Border` par barre sans aucune virtualisation (`VirtualizingStackPanel\|EnableRowVirtualization` → 0 sur tout le client) alors que le serveur autorise 366 jours. Et `GET /accounting/entries` **n'est pas paginé** — `from` et `to` sont nullables, `pageSize` n'existe que dans `AuditEndpoints.cs` : sans paramètre, c'est le grand livre entier matérialisé, sérialisé, désérialisé dans un PC de réception. Le système marchera parfaitement la première année. | **mois** | D5 |
| D9 | **Abstraction du temps** (`TimeProvider`) + jeux de bascule (31/12 → 01/01, passage de journée). | `TimeProvider\|IClock\|ISystemClock` → 0 ; 62 fichiers de test collés à l'horloge murale. La numérotation légale est annuelle : la seule occasion de découvrir un défaut de bascule serait le 1<sup>er</sup> janvier, en production, sur une pièce opposable. | **jours** | A9 |
| D10 | **Suite E2E transversale sur PostgreSQL** + couverture HTTP des routes P0 non atteintes + assertion d'exécution effective du gate. | ≥ 203 des 431 routes ne sont jamais appelées, dont 75 sur le PMS (night audit, tape chart, arrivées, départs, acomptes, transfert de folio). Le seul test « d'intégration » achats↔stock **retire les deux contrats du conteneur**. Et sans `RAQMI_TEST_POSTGRES`, les 10 tests PostgreSQL passent en Skipped et le job sort en 0 : **le garde-fou peut se désarmer sans que personne ne le voie**. | **mois** | A11, B10 |
| D11 | **Internationalisation** (arabe, RTL). | Aucun `.resx`, `FlowDirection\|RightToLeft` → 0. L'arabe est la langue officielle ; une part du personnel d'exploitation ne lit pas couramment le français. La marque **est** bilingue (`assets/brand/.../logo-bilingual-*.svg`), l'application ne l'est pas. Coût quasi nul aujourd'hui (une convention + un dictionnaire), des mois dans deux ans. | **semaines** | D6 |
| D12 | **Mécanisme de licence effectif** — ou réécriture de l'EULA. | `assets/LICENSE-EULA.txt`, affichée à chaque installation, engage sur « la clé de licence fournie » qui « expire » et se « renouvelle ». `NavigationNodes.cs` constate qu'« aucune licence n'est modélisée ». Faire signer un contrat décrivant un mécanisme inexistant est un risque juridique **et** prive l'éditeur de tout levier de renouvellement. | **semaines** | C1 |
| D13 | **Protection de la propriété intellectuelle à l'installation.** | `docs/deployment-onpremise.md:51-54` exige « .NET SDK 10 » et « a clone of this repository » sur le PC du client, parce que l'installeur exécute `dotnet publish` depuis les sources. **Chaque client reçoit le code source complet et l'historique Git**, en contradiction directe avec l'EULA §2 qu'il signe. Publier l'API en amont et livrer un binaire. | **semaines** | C7 |
| D14 | **Réduction du bus factor.** | `git shortlog -sne reorg/phase-1` → **149 commits, un seul auteur**. Aucun `CODEOWNERS`, aucun `CONTRIBUTING.md`, aucun template de revue. Et les quatre lots de la vague 2 — dont le périmètre unité et l'idempotence — vivent dans **11 worktrees locaux** sur des branches qui n'existent sur **aucun dépôt distant** (`git branch -r | grep worktree` → 0). Un projet qui explique aux hôtels que la sauvegarde doit sortir de la machine ne s'applique pas la règle à lui-même. | **jours** (pousser les branches) | aucune |

---

## 4. Les cinq risques qui feraient le plus mal

### R-A — La sauvegarde qui n'a jamais existé

**Scénario.** Mars, site pilote. Le disque du PC serveur lâche un mardi matin. On va chercher les
dumps : `C:\RaqmiSystem\backups` contient des fichiers `.dump.part` orphelins et rien d'autre — depuis
l'installation, `pg_dump` avortait sur le premier objet du schéma `hr`, faute de `GRANT`. Le
`raqmi-backup.service` n'a pas de `OnFailure=`, la tâche `check-health` n'a jamais été enregistrée,
et `/health/database` affichait `healthy` tous les jours. Perte : la totalité de l'exploitation depuis
la mise en service. Et même si le dump avait existé, aucune restauration n'a jamais été jouée
(`git ls-tree | grep -i restore` → 0) et `raqmi.env.ps1` n'est sauvegardé nulle part, donc le service
ne remonterait pas sur une machine neuve.

**Parade.** A2 (les 4 `GRANT` + test de couverture des schémas), A12 (copie hors machine, chiffrement,
`restore-raqmi.ps1`, restauration de vérification hebdomadaire horodatée), C2 (alerte sur retard
> 26 h), C9 (sauvegarde de la configuration). **Effort total : quelques jours + une semaine.** C'est
le meilleur rapport gravité/coût de tout ce document.

### R-B — Le détournement de recette que rien ne peut constater

**Scénario.** Le caissier de nuit détient `lodging.checkin`. Cette **seule clé** couvre l'arrivée, la
tenue des folios **et** le départ (`PermissionRegistry.cs:174-181` : `LodgingCheckoutExecute` et
`LodgingFolioManage` ont tous deux `LodgingCheckin` en alias). `AddFolioChargeAsync` n'impose **aucun
plafond, aucune approbation, aucun second acteur** sur un `Adjustment` négatif : une ligne « geste
commercial −180 000 » solde le folio, le départ passe puisque le solde est nul, aucune facture n'est
produite (le PMS n'en produit jamais), aucune écriture comptable n'existe, et la recette du jour est
de toute façon retapée à la main dans `DailyRevenue`. Variante symétrique : un `CashReceipt`
**confirmé** s'annule par son propre auteur, sans approbation et sans limite de date
(`CashReceipt.Cancel` ne refuse que le déjà-annulé, `/receipts/{id}/cancel` porte la **même** clé que
`/confirm`), et le récapitulatif ne compte que les `Confirmed` : la somme s'efface rétroactivement des
recettes lues par la direction. Pendant ce temps, un ordre de **paiement** — de l'argent qui sort —
exige, lui, une clé distincte. Le produit protège mieux ce qu'il dépense que ce qu'il encaisse. Et le
contrôle compensatoire que tout le monde suppose présent n'existe pas : `IDailyClosingReadService` n'a
**qu'un seul consommateur** dans tout le dépôt, `DailyRevenueService` — la « clôture journalière » ne
fige que les quatre montants tapés à la main. Dernier maillon : le seul témoin est une ligne d'audit,
et `POST /audit/purge` accepte `olderThanDays = 1`, purge sans filtre de domaine, et **n'écrit aucune
entrée d'audit de sa propre exécution**.

**Parade.** Immédiat et bon marché : plafond + second acteur sur l'ajustement négatif ; clé
d'annulation d'encaissement distincte de la clé de confirmation ; audit obligatoire de la purge et
plancher de rétention non contournable ; `REVOKE DELETE` sur `audit.audit_logs` au profit d'une
fonction `SECURITY DEFINER`. Puis A7 (session de caisse) et B10 (déversement comptable) qui
reconstruisent le contrôle réel. **Quelques jours pour la séparation des tâches, des semaines pour le
reste.**

### R-C — La première mise à jour tue le serveur du client

**Scénario.** Six semaines après la mise en service, vous livrez un correctif. Il n'existe **aucun**
`update-server.ps1` : le seul chemin documenté est de relancer `install-server.ps1`. Celui-ci génère
un nouveau mot de passe applicatif aléatoire, l'écrit dans `raqmi.env.ps1` à l'étape 7, puis exécute
`create-app-role.sql` — qui, par conception, « will NOT reset the password of an already-existing
role ». L'étape 8 échoue sur authentification refusée. Le fichier de configuration porte désormais un
mot de passe qui n'existe nulle part, l'ancien n'a jamais été affiché, et `start-api.ps1` dot-source
ce fichier à **chaque** démarrage. Au prochain redémarrage du PC, l'API ne se connecte plus. Remise en
service : accès admin PostgreSQL et `ALTER ROLE` manuel, qu'aucun runbook ne décrit. L'installeur a
par ailleurs écrasé `C:\RaqmiSystem\api` : il n'y a pas d'exe précédent vers lequel revenir.

**Parade.** A3 (`update-server.ps1` idempotent avec `api-previous\` et rollback sur échec de sonde),
C5 (runbook), C1 (versionnage, sans quoi on ne sait pas ce qui tourne où). **Semaines. À faire avant
la première livraison chez un client, pas après.**

### R-D — La paie fausse, découverte rétroactivement

**Scénario.** Quatorze mois après la mise en service, contrôle CNAS ou DGI sur la retenue à la source.
L'abattement IRG a été appliqué à la base au lieu de l'impôt, sans plancher ni plafond, sur un barème
sans tranche à 0 %. Les nets versés sont faux depuis le premier bulletin. L'employeur est le redevable
légal : rappel d'impôt sur l'exercice entier, majorations, et bulletins déjà remis à rectifier un par
un. Aggravant spécifique : le module ne se corrige **pas** par paramétrage — `PayrollParameters` n'a ni
taux, ni plancher, ni plafond d'abattement sur l'impôt — donc le correctif est un changement de code,
et la suite de tests la mieux écrite du dépôt encode la formule fausse dans ses commentaires calculés
à la main. Toute correction fera échouer huit tests présentés partout comme la référence, ce qui la
rendra politiquement suspecte. C'est la forme la plus dangereuse de dette technique : **un module dont
les tests certifient l'erreur**.

**Parade.** B8 : refonte depuis le barème publié, avec les paramètres d'abattement sur l'impôt ajoutés
au type, **et réécriture des tests depuis la règle légale, pas depuis le moteur**. Faire valider les
huit scénarios par un expert-comptable agréé avant de reprendre le développement RH. Y adjoindre D4
(contraintes de base RH) et A2 (schéma `hr` accordé) dans le **même lot** : ces trois défauts portent
sur la même donnée et seront ignorés s'ils sont dispersés. **Mois.**

### R-E — Le contrôle qui trouve un hôtel sans pièces

**Scénario.** Contrôle DGI, ou simple contrôle de police sur le registre des étrangers. L'hôtel ne
peut produire : aucune facture d'hébergement (le folio ne devient jamais une facture), aucun document
imprimé (aucune bibliothèque PDF dans le dépôt), aucun registre de police (`Reservation.GuestName` est
un `string?`, ni pièce, ni nationalité), aucun G50 (pas de TVA déductible, `PurchaseOrderLine` ne
porte aucun champ TVA), aucun avoir (mais la possibilité d'**annuler** une facture émise, ce qui
réduit rétroactivement une TVA déjà déclarée), aucun droit de timbre sur les règlements en espèces, et
des écritures comptables potentiellement sans numéro de pièce si l'installation a démarré sans
période. Chacun de ces points pris isolément est un redressement ; ensemble, ils caractérisent une
comptabilité non régulière.

**Parade.** A4 + A5 + B1 → B6. C'est le gros du chantier légal, et c'est pourquoi je recommande
au §5 de **ne pas ouvrir chez un client tant que la chaîne facture-document-registre n'est pas
livrée**, plutôt que de la promettre pour plus tard.

---

## 5. Ce que je ferais à ta place

### Vague 0 — « rendre une journée de travail possible » (2 à 3 semaines)

Rien de ce qui suit n'est un chantier. Ce sont des correctifs de quelques dizaines de lignes chacun,
et tant qu'ils ne sont pas faits, **personne ne peut recetter quoi que ce soit** — pas même vous.

1. **A11** — faire tourner la CI sur `reorg/**` et rendre `publish-api` dépendant de
   `postgres-integration`. *Une heure.* Sans cela, tout ce qui suit est déclaratif.
2. **A1** — renouvellement du jeton + traitement du 401 dans le client lourd.
3. **A2** — les quatre `GRANT` + le test qui échoue si une migration crée un schéma non accordé.
4. **A8** — timeout HTTP explicite côté client.
5. **A9 + A10** — fuseau « Africa/Algiers » et culture décimale unique.
6. **B7** — la balance auxiliaire (chiffre faux déjà affichable) et la transaction sur le lettrage.
7. **B6** (moitié corrective) — refuser de comptabiliser sans période, et ajouter la route de création
   de période.
8. **D14** — pousser les onze worktrees sur le dépôt distant. *Dix minutes, et cela supprime le risque
   de perdre le lot le plus critique du projet.*

À la fin de la vague 0, jouez vous-même le protocole de smoke des 30 écrans
(`docs/stabilization/module-readiness.md`) et remplissez les 30 `"smoke": null` de
`tools/readiness/screens.json`. C'est la première fois qu'un humain aura ouvert le produit de bout en
bout.

### Vague 1 — « rendre un premier client possible » (3 à 4 mois)

Dans cet ordre, parce que chaque élément conditionne le suivant :

9. **A6** — périmètre utilisateur ↔ unité. *Avant tout nouvel écran*, sinon chaque écran livré est à
   reprendre. Le lot existe déjà dans un worktree ; il est plus gros qu'annoncé.
10. **B1** — identité fiscale et numérotation **par établissement**. Préalable à toute facture.
11. **A4** — facturation du folio.
12. **A5** — chaîne documentaire imprimable (rendu, archivage, remise).
13. **A7** — session de caisse et `CashReceipt` réel sur règlement de folio.
14. **B2 + B4 + B5** — identité client / registre de police, droit de timbre, taxe de séjour
    automatique.
15. **B3** — avoir, et suppression de l'annulation d'une facture émise.
16. **A3 + A12 + C1 + C2** — mise à jour, sauvegarde hors site prouvée, versionnage, supervision.
    *Ceux-là doivent être finis avant que le logiciel quitte vos locaux, pas avant la fin du pilote.*
17. **B8** — refonte IRG, seulement si le pilote comprend la paie (voir ci-dessous).

### Jalon « premier client pilote »

**Ce qu'il faut avoir fini — sans exception :**
vague 0 complète ; A6 (périmètre) ; B1 + A4 + A5 (facture légale imprimée et remise) ; A7 (caisse) ;
B2 (registre de police) ; B4 (droit de timbre) ; B5 (taxe de séjour) ; B3 (avoir) ; A3 + A12 (mise à
jour et sauvegarde restaurée au moins une fois **devant témoin**) ; C1 + C2 + C5 + C6 (version,
alerte, runbook, procédure papier) ; C12 (TLS + rate limit) ; C13 (guides des trois rôles) ; et les 30
lignes de smoke réellement validées.

**Ce qu'on peut assumer de ne pas avoir, si on le dit :**
le déversement comptable automatique (B10) — à condition que le pilote soit une **unité unique**, que
le comptable exporte les factures et les encaissements une fois par mois, et que vous ayez livré
l'export des livres ; les états financiers SCF (B11) et les immobilisations (B13) — pas dus avant la
première clôture annuelle ; le POS restaurant, le channel manager, le moteur de réservation directe,
MICE, PortMaster ; le mode dégradé hors ligne (le refus de la file de rejeu est le bon arbitrage,
compensé par C6) ; l'arabe (D11) ; le multi-unités réel.

**Ce qu'il faut refuser de promettre, par écrit, dans le contrat pilote :**
- le **G50** et la liasse fiscale (B9) — vous ne pouvez pas les produire, même à la main, faute de TVA
  déductible ;
- le **multi-unités** : le tableau de bord groupe existe à l'écran mais l'architecture est **une base
  PostgreSQL par site, sans réplication** (`SyncSupervisionService.cs:11-15` l'assume). Livrer A6 rend
  le multi-unités *sûr*, pas *possible* ;
- toute reprise de données : `import|bulk|batch` → 0 route, aucun solde d'ouverture, et la date
  d'arrivée d'un walk-in n'est pas saisissable. **Ce PMS ne sait pas démarrer dans un hôtel déjà
  ouvert** : exigez une mise en service sur un hôtel vide, ou un mois de double saisie assumé et
  facturé ;
- la **paie**, tant que B8 n'est pas fait et validé par un expert-comptable ;
- une **licence** avec date d'expiration et clé de renouvellement (l'EULA la promet, `NavigationNodes.cs`
  constate qu'elle n'est pas modélisée) ;
- tout **engagement de disponibilité** : PC serveur unique, sans onduleur exigé, sans failover.

**Et une décision que personne d'autre ne prendra à votre place.** `docs/legacy/` contient un dossier
de certification interne daté du 24 juillet 2026 décrivant un produit — Hotel Metrics Pro Desktop
v0.8.0 — qui déclarait livrés l'avoir numéroté, l'écriture automatique facture → 411/707/445710, le
registre TVA, l'archivage 10 ans et la licence, avec un pilote EGT Sidi Fredj autorisé. Je vous invite
à la prudence sur ce document : c'est une **auto-évaluation interne**, plusieurs lignes y sont 🟡
(fiche de police, taxe de séjour, immobilisations, bulletins PDF), et il porte sur une pile Electron
+ SQLite abandonnée. Mais le fait demeure : `docs/reorganisation/06-risques.md` contient 25 risques,
tous techniques, et **pas un seul** sur la régression fonctionnelle, le client existant ou la reprise
de ses données — le seul risque qui mentionne le legacy (R17) le formule à l'envers. Écrivez ce risque
et arbitrez-le : soit vous assumez une réécriture longue sans client entre-temps, soit vous datez une
parité fonctionnelle. Ne laissez pas cette question hors registre.

---

## 6. Ce que je ne recommande pas

**1. Ne réduisez pas `AccessTokenMinutes` à 10-15 minutes.** C'est la recommandation de l'analyse
sécurité, et elle transformerait une panne horaire en panne au quart d'heure, puisque **le client ne
renouvelle rien**. Ordre correct : d'abord A1, ensuite seulement le durcissement des durées.

**2. N'ajoutez pas de paquet d'analyseurs (StyleCop, Roslynator, Sonar) pour l'instant.** Le dossier
conclut « aucun analyseur » ; c'est inexact — `Directory.Build.props` porte `AnalysisLevel=latest` et
les analyseurs .NET tournent à chaque build. Le problème est qu'ils n'ont aucune dent
(`TreatWarningsAsErrors=false`, aucun workflow ne lit leurs sorties). Durcissez ce qui existe, ne
superposez pas une seconde couche de bruit. *(En passant, personne n'a relevé `LangVersion=preview` sur
toute la solution : pour un logiciel comptable destiné à dix ans de conservation, épinglez une version
de langage figée.)*

**3. Ne lancez pas la refonte MVVM complète du client WPF maintenant.** 1,09 Mo de code-behind est un
passif réel, mais le réécrire avant d'avoir livré la chaîne documentaire (A5) revient à réorganiser
une maison avant d'y avoir mis le toit. Introduisez des ViewModels **uniquement** sur les écrans que la
vague 1 touche de toute façon, et créez le projet de tests Desktop dès le premier.

**4. Ne partez pas sur un `DbContext` par contexte borné en rétro-adaptation.** Le `RaqmiDbContext`
unique à 99 DbSets est un vrai frein — le snapshot de 401 Ko garantit des conflits dès que deux lots
touchent une entité — mais le découper rétroactivement sur 22 migrations existantes vous coûtera des
mois sans gain visible pour le client. Appliquez la règle aux **nouveaux** contextes (Posting Engine,
Outbox, Notifications) et ajoutez une convention de fusion pour le snapshot.

**5. Ne développez aucun des 20 modules « Planifié ».** `ModuleCatalog.cs` affiche 57 entrées dont 33
`Disponible`, 20 `Planifie`, 2 `Partiel`, 2 `ApiPrete` : **plus d'un tiers des portes de l'application
s'ouvrent sur rien**, PortMaster compris (`HotelUnitType.Marina` existe, aucun fichier de domaine
n'existe). En démonstration, c'est un passif de crédibilité, pas un actif commercial. Faites l'inverse :
masquez ou grisez explicitement les entrées Planifié avec une date, et n'en ouvrez aucune avant que la
vague 1 soit finie.

**6. Ne construisez pas la file de rejeu hors ligne.** Le refus explicite de
`SyncSupervisionService.cs` (« Un encaissement en double est un incident comptable ; une ressaisie n'en
est pas un ») est le **bon** arbitrage, et il est bien argumenté. Livrez d'abord D2 (clé
d'idempotence), puis C6 (procédure papier). Le mode dégradé viendra après, ou jamais.

**7. Ne remplacez pas le garde de readiness par un garde plus gros.**
`tools/check-module-readiness.ps1` (623 lignes d'expressions régulières sur du C#) est de l'outillage
de compensation devenu contrainte de conception : renommer `ModuleCatalogEntry` ou extraire
`MainWindow` casse la CI. Réduisez-le progressivement au rendu du tableau, et déplacez les preuves
dans des tests xUnit compilés. Et notez l'échéance que personne n'a signalée : **21 des 30 écrans ne
tiennent leur niveau `Functional` que par la clause `documentationGrace` de
`tools/readiness/screens.json`, qui expire le 31/12/2026** — dans moins de quatre mois, sans action,
21 écrans retombent en `TechnicalPreview` et la CI passe au rouge, par conception. C'est un jalon dur,
inscrit dans votre dépôt.

**8. N'investissez pas dans la sur-couverture de tests SQLite.** 795 des 957 tests ne voient jamais
PostgreSQL et 10 seulement (1 %) tournent sur la vraie base. Ajouter des tests SQLite augmente la
durée de la suite sans réduire le risque. Basculez plutôt un sous-ensemble représentatif — tous les
parcours argent, stock et inventaire PMS — vers le job `postgres-integration` existant.

---

## 7. Annexe

### 7.1 Notes par dimension (les miennes, après revérification)

| Dimension | Note du panel | Ma note | Motif de l'écart |
|---|---|---|---|
| Couverture métier (PMS + back-office) | 4/10 | **4/10** | Confirmée. Le cœur est bon, le dernier mètre manque aux deux bouts. |
| Comptabilité SCF et fiscalité | 3/10 | **2,5/10** | Le noyau de partie double est excellent ; le socle SCF posé par-dessus (exercices, périodes, tiers, lettrage, grand livre, balance auxiliaire) est écrit en one-liners minifiés, sans une seule transaction, avec 6 tests — **et il produit un chiffre faux** (B7). Les deux couches n'ont pas la même exigence. |
| Sécurité applicative | 4/10 | **4/10** | Note maintenue, motif changé : le code d'authentification est bon ; le problème est qu'il n'est jamais rejoué (CI absente sur la branche), que la moitié n'est jamais appelée (refresh), et que le cloisonnement — le seul sujet qui compte pour un ERP multi-unités — est absent. |
| Intégrité des données / persistance | 6,5/10 | **6/10** | Le durcissement en base est réel et mesurable, mais il s'arrête aux quatre modules que le rôle PostgreSQL ne peut de toute façon pas lire, et l'invariant d'argent le plus important (`en-tête = somme des lignes`) n'est pas en base. |
| Architecture logicielle | 5/10 | **5/10** | Confirmée. Façade propre, fond soudé à EF. Le vrai risque n'est pas la dette, c'est qu'aucun garde n'empêche de l'aggraver. |
| Stratégie de tests / CI | 4/10 | **3/10** | Abaissée : le dossier évalue la suite comme si elle protégeait le travail en cours. **Elle ne s'exécute pas sur cette branche.** Une suite qui ne tourne pas ne vaut pas 4/10. |
| Exploitation / on-premise | 4/10 | **3,5/10** | Abaissée : l'installation est bonne, mais la sauvegarde est **morte à l'installation** (A2), il n'existe aucun chemin de mise à jour, et le premier correctif livré détruit le serveur du client. |
| **Ensemble** | ~4,4/10 | **4/10 en produit, 7/10 en socle** | L'écart entre les deux est le vrai diagnostic. |

### 7.2 Points où les analystes se sont trompés

**Surestimations.**

1. **« Rotation des refresh tokens » classée en force majeure.** C'est du code que rien n'appelle :
   `RaqmiApiClient.cs:27` ne garde que `accessToken`, `LoginAsync` jette `login.RefreshToken`,
   `git grep -in refresh -- src/RaqmiSystem.Desktop` ne remonte que des boutons d'interface. La force
   annoncée est une fonctionnalité fantôme, et la recommandation associée (descendre à 10-15 min) est
   un accélérateur de panne.
2. **« 100 % des 448 routes protégées ».** Comptage exact sur les 33 fichiers d'endpoints à `da196a7` :
   **431** `Map*` et **432** `RequireAuthorization` (l'écart de 1 est un commentaire dans
   `AccountEndpoints.cs`). Et surtout : 3 routes portent un `RequireAuthorization()` **vide** —
   `AccountEndpoints.cs:62`, `SyncEndpoints.cs:46` et `:60` — plus `/me` dans `Program.cs:181`. Porter
   un attribut n'est pas être protégé : c'est exactement le reproche que le même dossier adresse au
   garde de readiness.
3. **« La paie algérienne est réellement calculée » + « testée comme une fonction pure ».** Vraies au
   sens littéral, fausses au sens qui compte (B8). Les tests ne prouvent que la fidélité du moteur à sa
   propre erreur, et ils la verrouillent.
4. **« Concurrence traitée au bon niveau sur les gestes comptables ».** Vrai pour les trois gestes sur
   l'écriture, faux pour le quatrième geste qui touche à l'argent : `ReconcileAsync` n'a **aucune**
   transaction et aucune contrainte d'unicité en base.
5. **« La facture est pensée pour le contrôle fiscal algérien ».** Les trois règles existent — en C#
   uniquement. `InvoiceConfiguration.cs` pose **un seul** CHECK (le statut), et `raqmi_app` a UPDATE et
   DELETE sur le schéma `finance`.
6. **« Aucun analyseur, aucune porte qualité ».** Inexact sur le fait : `AnalysisLevel=latest` est posé
   dans `Directory.Build.props`. Juste sur la conclusion, mais la formulation envoie sur la mauvaise
   action.
7. **« `ChargeKind.Tax` n'est jamais utilisé ».** Faux : `ExtraItem.cs:43` et `:151` l'autorisent
   explicitement comme nature d'article extra. Ce qui manque est l'automatisme de pose et la
   déclaration — le lot B5 est plus petit qu'annoncé.
8. **« Les seules sorties de la comptabilité sont des réponses JSON paginées ».** Rien n'est paginé :
   `ListEntriesAsync(DateOnly? from, DateOnly? to, ...)` retourne un
   `IReadOnlyCollection<JournalEntryResponse>` **lignes comprises**, et `pageSize` n'existe que dans
   `AuditEndpoints.cs`. Le défaut est plus grave que décrit, pas moins.
9. **« ZÉRO ICommand » dans le client WPF.** Faux au sens WPF : le commanding routé porte toute la
   couche raccourcis (`MainWindow.xaml:26-33`, `MainWindow.Shortcuts.cs`). Le diagnostic « pas de
   MVVM » reste juste.
10. **« Le cœur PMS est meilleur que ce qui se vend localement ».** Non étayé, et démenti par un point
    non testé : la date d'arrivée d'un walk-in n'est pas saisissable
    (`LodgingService.Reservations.cs`). Un PMS qui exige que l'hôtel soit vide pour démarrer n'est pas
    utilisable en reprise.

**Sous-estimations.**

11. **Les 4 schémas non accordés** ne « cassent pas quatre modules » : ils suppriment **toute**
    sauvegarde dès le jour 1, cassent l'**écran d'accueil** (qui appelle `/housekeeping/board`,
    `/hr/absences`, `/hr/payroll/periods`), et l'installation se termine en **succès apparent** parce
    que le schéma `security` est accordé. 25 des 104 tables, dont les 11 tables RH.
12. **Le trou de numérotation comptable** est classé « majeur » alors qu'il est structurel et **sans
    issue par l'API** : il n'existe **aucune route de création de période**.
13. **Le périmètre unité** manque à **deux** étages, pas un : dans les droits **et** dans
    l'infrastructure (une base par site, sans réplication, face à un tableau de bord de groupe).
    Livrer le lot rend le multi-unités sûr, pas possible.
14. **La purge d'audit** est la **seule route mutante des 431 qui n'écrit aucune entrée d'audit**, et
    `AuditRetentionDays` n'est lu par **aucun** code de production (aucun `IHostedService` n'existe).
15. **« Aucun document ne s'imprime »** est chiffré en semaines. C'est un sous-système : ni moteur de
    rendu, ni stockage de fichier (`IFormFile|byte[]|blob` → 0 dans Api/Domain/Application), ni canal
    de remise (`Reminder.cs:10` : « There is no mail, SMS or postal infrastructure anywhere in this
    repository »).

**Angles morts — ce que personne n'avait vu et que j'ai vérifié :**
la session qui meurt à 60 minutes (A1) ; le fuseau UTC contre UTC+1, avec deux horloges métier
incohérentes côté serveur (A9) ; la culture décimale sur 13 vues (A10) ; l'erreur d'arithmétique de la
balance auxiliaire (B7) ; le lettrage sans transaction (B7) ; `RefreshAsync` qui ne vérifie **pas** le
verrouillage du compte — `SignInAsync` teste `IsLockedOut`, `RefreshAsync` ne teste que `IsActive`,
donc un compte verrouillé après cinq échecs continue d'émettre des jetons complets pendant 14 jours ;
`SeedInitialAdminAsync` qui appelle `user.AssignRole(adminRole, ...)` **hors** du bloc
`if (user is null)`, donc toute réexécution de l'installeur restaure silencieusement
`system.administrator` sur un compte rétrogradé, sans entrée d'audit ; l'acompte versé, remboursé ou
conservé qui n'entre dans aucun registre encaissable (`Deposit.MarkPaid/Refund/Forfeit` ne font que
changer des champs texte et un statut, seul `ApplyTo` pose une ligne de folio) ; `IDailyClosingReadService`
qui n'a **qu'un seul consommateur** ; la livraison du code source complet au client, contre l'EULA
qu'il signe (D13) ; les 11 worktrees sur aucun dépôt distant (D14) ; l'absence totale d'import et de
solde d'ouverture (C10) ; l'échéance `documentationGrace` du 31/12/2026 sur 21 écrans.

---

*Fin du document. Aucun autre fichier du dépôt n'a été modifié.*
