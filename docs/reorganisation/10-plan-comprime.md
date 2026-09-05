# 10 — Plan comprimé : le calendrier refait sur la cadence mesurée

Ce document **remplace le calendrier** du plan 09 (§2.2, §2.3, et les durées en tête des §3.0 à §3.8). Il ne
remplace **ni ses lots, ni ses périmètres de fichiers, ni ses critères d'acceptation, ni ses gardes CI** (voir §7).

**Révision 2 — après revue adverse.** La version 1 annonçait 24 semaines. Elle se trompait sur quatre points
mesurables, tous dans le sens qui arrange : étalon de production gonflé 4,2×, durée de cycle mesurée sur la fenêtre
de commit et non de travail, facteur de compression déclaré inapplicable à la conformité algérienne puis appliqué
au chiffre près à la vague qui n'est que cela, et pari mono-établissement engagé sans que sa branche perdante soit
chiffrée. **Total corrigé : 33 semaines** dans le cas favorable, **44** dans le cas par défaut. Un calendrier faux
qui plaît est pire qu'un calendrier long qui tient.

---

## 1. Pourquoi 60 semaines, et pourquoi c'était faux — mais pas de 20×

**L'erreur de catégorie.** Le plan 09 a estimé chaque point en jours-développeur-humain, puis converti en calendrier
avec un rendement humain (3,5 jours-agent par agent et par semaine, 4 agents). Sa formule §2.2 est
`max(charge/14 ; plus_gros_lot/3,5 ; chaîne/3,5 ; charge_humaine/3,5)`, et il précise lui-même que « c'est la
deuxième ligne qui commande, presque partout ». D'où « un lot de 24 j-a occupe cet agent 6,9 semaines ».

Son §2.1 le reconnaît : « Le rendement retenu ci-dessous est une hypothèse de travail, pas une mesure. » Le
propriétaire ne conteste donc pas une mesure : il conteste une hypothèse déclarée non mesurée par son auteur.

**Ce que la branche a réellement produit** — `git diff --numstat main...reorg/phase-1`, ventilé par chemin :

| Catégorie | Insertions | Part | Nature |
|---|---:|---:|---|
| EF Core `*.Designer.cs` | 19 011 | 41,9 % | **Régénéré par `dotnet ef migrations add`.** Personne ne l'a écrit. Deux fichiers : `AccountingScfCore.Designer.cs` (9 500, commit `94ba504`) et `AccountingAuxiliaryLedger.Designer.cs` (9 511, `0b9f841`) |
| `docs/` (md + maquettes HTML) | 11 457 | 25,3 % | Documentation et maquettes |
| `src/` C# | **6 790** | 15,0 % | Production écrite à la main |
| `tests/` | **4 080** | 9,0 % | Production écrite à la main |
| `*.xaml` 1 611 · `tools/` 1 427 · `Migrations/` hors Designer 807 · autres 164 | 4 009 | 8,8 % | Production, outillage, généré |
| **Total** | **45 347** | 100 % | — |

La version 1 affichait « 45 347 insertions » comme « le chiffre qui démonte l'hypothèse ». **Elle comptait comme
production humaine deux instantanés régénérés par une commande de deux secondes.** L'assiette défendable est de
**10 870 lignes** (`src/` + `tests/`), 13 908 avec XAML et outillage : le numérateur était **4,2× trop grand**.

**Et la durée était mesurée au mauvais endroit.** « Vague 1 : 49 minutes » est la fenêtre de **commit et de
fusion**, pas de production. Les trois premiers commits portent la seconde identique — `a239151`, `c617b3b`,
`f02ee6a` à `2026-09-01 21:06:10`, signature d'une écriture antérieure validée en lot — et un silence de **3 h 25
sans aucun commit** (`0b9f841` à 17:41:14 → 21:06:10) les précède, seul emplacement possible du travail des 5
agents. Le cycle réel est d'au moins **≈ 4 heures**, soit 5× la valeur citée ; le volume de 9 638 est exact, lui.

**Le nouveau facteur, pris par son mauvais bout.** Deux étalons subsistent et divergent d'un facteur 6 : la vague 1
(8 130 lignes de production en ≥ 4 h) et la refonte de l'accueil (5 465 lignes en 7 h mesurées le 3 septembre,
**plus une journée d'exploration le 2 qui n'est pas mesurée**). Deux lots ne font pas une mesure. On retient donc
**la borne lente : 1 j-a du plan 09 = 1,2 h de cycle complet, facteur ≈ 10×** — et non 0,6 h / 20×. C'est un
**budget plafond, pas une prévision**, à confirmer à la fin de B1 par le relevé du §7, instrumenté aux horodatages
de **lancement d'agent**, pas de commit.

**Ce que l'étalon ne dit pas. La fenêtre étalon ne contient aucun lot de conformité légale** — `git grep -il`
`"timbre"`, `"Nationality|PassportNumber"`, `"TouristTax"` sur `src` renvoient **0** — et son unique lot comptable
(**500 lignes de `src/` hors migrations**, commits `94ba504` et `0b9f841`) a **produit** le point bloquant B7 :
`GetAuxiliaryBalanceAsync` (recouvrement faux dès le premier lettrage partiel) et `ReconcileAsync` (seule opération
financière du dépôt sans transaction) sont nées là. L'étalon produit à grande vitesse a fabriqué une partie du
travail qu'on lui demande de chiffrer. **Aucun point B\* n'est chiffrable par cette mesure** : ils gardent tous leur
estimation du plan 09 tant qu'un lot de conformité n'a pas été livré, relu par un expert-comptable et intégré.

**Ce qui reste des 60 semaines.** Le quatrième terme, `charge_humaine / 3,5` : les « 55 à 59 semaines » du plan 09
ne sont pas une addition de charges agent, c'est la disponibilité du propriétaire écrite une seconde fois
(83 ÷ 1,5 j-h utiles/sem. = 55,3). **Erreur en sens inverse :** le plan 09 chiffre **zéro semaine de délai
extérieur** (« délai externe », 3 occurrences, jamais suivi d'un nombre) alors que six tiers commandent des vagues.

---

## 2. Le nouveau calendrier — deux branches, toutes deux affichées

**Deux facteurs, jamais mélangés.** « Agent outillé » (navigation, RBAC, WPF, CI, exploitation, tests, scripts) :
**1,2 h par j-a du plan 09**. « Conformité, non comprimé » (**tout point B\*, plus B6, B7 et D3**) : **facteur 1**,
le chiffrage jour-développeur du plan 09 tel quel, à 3,5 jours par agent et par semaine. Ce sont ces jours-là qui
commandent B1 et B2.

### Branche A — pilote mono-établissement **et** hôtel vide, vérifié par W8.1 avant B2

| # | Vague | Objectif | Agent outillé | Conformité non comprimée | Propriétaire | Délai extérieur subi | Durée |
|---|---|---|---|---|---|---|---|
| S1 | Décisions, amorçage, **sauvetage des 4 lots dormants** | 3 décisions, 4 demandes extérieures, rebase + build + 948 tests sur les lots dormants, **lancement de W8.1** | 14 j-a → **17 h** | — | 14 j-h | démarre les attentes | **3 sem.** |
| B1 | Socle exploitable | A1, A8, A2, A11, C1, D14, A9, D9, A10, A6a, D1 *(outillé)* — **B7 → B6** (chaîne, même agent, jeton EF), D3, B12 min, **B14 min** | 37 j-a → **44 h** | **25 j** — chaîne B7→B6 = 15,5 j / 3,5 = **4,4 sem.** | 8 j-h dont **2 de relecture comptable** | — | **5 sem.** |
| B2 | Facture légale — *ne démarre pas avant W8.1* | A4, A5 min., A7 min. *(outillé)* — **B3 → B4** (chaîne), B2a min. | 35,5 j-a → **43 h** | **37 j** — chaîne B3→B4 = 29 j / 3,5 = **8,3 sem.** | 10 j-h | **fiche de police** (2-6 sem.), **barème du timbre** (2-6 sem.), gabarit relu (2-4 sem. **à partir de la fusion d'A5**) | **9 sem.** |
| B3 | Site exploitable sans l'éditeur | A12, A3 min., **C9**, **D13 min.**, C2/C3/C5/C6/**C11**/C12 min., **C13 rédaction**, D2b min., export des livres | 64,5 j-a → **77 h** | — | 9 j-h dont H1.4 | — | **5 sem.** |
| R | Recette et build candidate | R30 réduite (2 passes), reprise des réserves, démonstration intégrée, 3 PV | 20-30 j-a → **30 h** | — | 14 j-h | — | **4 sem.** |
| M | Mise en service chez le pilote | W8.3 à W8.6 — *W8.4 exige les guides de C13* | 10 h réservés | — | 31 j-h | disponibilité du personnel | **7 sem.** |
| — | *File pilote (parallèle, dès S1)* | *identification → **W8.1, porte de B2** → contrat + EULA (W8.2)* | — | — | 10 j-h | **7 à 14 sem.** | *doit tenir dans S1+B1 = 8 sem.* |

**Total branche A : 30 à 38 semaines, cible 33** (3+5+9+5+4+7). Charge agent : **≈ 265 h de cycle complet**, provision de reprise de 20 % comprise (fourchette honnête 130 à 450 h). Charge propriétaire : **≈ 96 j-h**.

### Branche B — hypothèse mono-établissement perdue : **c'est l'issue par défaut tant qu'aucun hôtel n'est identifié**

| Poste réintégré | Charge | Effet calendaire |
|---|---|---|
| A6b filtrage du périmètre unité (L1.1/L1.2/L1.3) | 39 j-a outillé → 47 h, 3 gros lots à intégrer | +2 sem. |
| B1 identité fiscale et numérotation par établissement | **11 j non comprimés** (point B\*) | +3 sem. |
| **Reprise de ce qui aura été livré entre-temps** | Avis 08 A6 : « chaque écran livré avant ce lot sera à reprendre », facteur **2 à 3**. B2 et B3 pèsent ≈ 100 j-a de contenu → **30 à 60 j-a de reprise** + une passe de recette complète (7 j-h) | +3 à 9 sem. |
| **Total branche B** | | **41 à 47 semaines, cible 44** |

**Et si l'hôtel n'est pas vide**, C10 redevient bloquant (import et soldes d'ouverture, « mois » à l'avis 08) :
**+4 à 8 semaines**, cumulables avec la branche B. **Durée plancher — si le code était instantané : 27 semaines**
(≈ 96 j-h ÷ 3,5 par semaine), contre 19 à 26 annoncées en version 1.

**Conséquence à dire franchement.** Sur les 33 semaines de la branche A, **24 ne dépendent pas des agents** — mais
les 9 de B2 en dépendent, et rien ne les comprime : ce sont des jours de conformité, tranchés par un texte officiel
ou un expert-comptable, pas par un test. Ajouter un cinquième agent ne déplace aucune des deux catégories.

---

## 3. Le périmètre pilote resserré

**Le resserrement ne porte pas sur le nombre de points, il porte sur leur profondeur.** Comptés un par un, 34 des 43 points de l'avis 08 restent au jalon pilote — comme dans le plan 09. Ce qui change : 11 sont livrés en version minimale, 9 sont différés avec conséquence écrite, 3 sont contournés à la main, et **7 obligations légales** (B2a, B3, B4, B6, B7, B12, B14 min.) restent non négociables. La version 1 annonçait « 18 sur 43 » : c'était un décompte de tableau, pas un périmètre.

### Indispensable — aucune réduction

| Point | Pourquoi |
|---|---|
| A1 renouvellement de jeton, A8 timeout | Jeton de 60 min, `Unauthorized` absent du Desktop : aucune journée ni aucune recette ne va au bout |
| A9 horloge Africa/Algiers + **D9 `TimeProvider`**, A10 culture décimale | 17 `DateOnly.FromDateTime(UtcNow)` ; `« 1,500 »` vaut 1,5 ou 1500 selon le poste, les deux réussissent en silence |
| A2 les 4 GRANT manquants ; A11 CI sur `reorg/**` ; D14 pousser les 10 branches | 19 schémas, 15 accordés : `pg_dump` tronqué depuis le jour 1 (4 lignes de SQL). Les 948 tests n'ont jamais tourné sur cette branche. 10 branches locales, **0 poussée** |
| B7 balance auxiliaire, B6 période comptable, D3 invariants en base | Chiffre faux déjà affichable ; écriture postée sans numéro de pièce, irréparable. **Écrits par cette branche, non comprimables** |
| A4 facturation du folio ; B3 moitié 1 — `Invoice.Cancel` restreint à `Draft` | `Folio.AttachInvoice()` n'a aucun appelant côté hébergement. `Cancel` accepte `Draft or Issued` (Invoice.cs:234) : TVA déjà déclarée réductible a posteriori |
| B4 droit de timbre, **répercuté dans le total en base** | Obligation légale. L'erreur du legacy était de ne l'ajouter que sur le PDF |
| A12 sauvegarde hors machine, chiffrée, **restaurée devant témoin**, **+ C9 secrets serveur** | RPO réel « tout depuis l'installation ». **Sans C9, la restauration d'A12 prouve la récupération des données mais pas celle du service** : `backup-raqmi.ps1` ne sauvegarde que la base, `raqmi.env.ps1` n'est ni exporté ni réaffichable |
| **B14 min — audit des accès en lecture aux données personnelles** | `ListEmployeesAsync` renvoie `ActiveContractGrossSalary` **sans écrire une entrée d'audit** : un seul GET rend la grille salariale complète sans trace. Obligation 18-07, pas une dette technique. Tenu par l'extrait L0.5 |

### Version minimale

| Point | Version retenue | Ce qu'on retire |
|---|---|---|
| A5 chaîne documentaire | Un gabarit — la facture — monolingue français, imprimable et réimprimable par son numéro | 4 autres gabarits, archivage documentaire et sa migration, bilingue |
| A7 encaissement ; B3 moitié 2 — avoir ; B2a identité de l'hébergé | Le règlement de folio crée un `CashReceipt` réel (TreasuryService.cs:257, jamais appelée) ; série d'avoirs distincte sur le modèle de numérotation existant ; nationalité, type et numéro de pièce sur la fiche existante | Modèle session/shift et plafond d'ajustement (parade R-B) ; sous-système documentaire dédié ; entité `StayOccupant` et multi-occupants |
| B12 inaltérabilité | Purge d'audit filtrée + `REVOKE UPDATE, DELETE` sur `audit.audit_logs` et `accounting.journal_entries` | Rétention 10 ans avec purge automatique (`IHostedService`) |
| C12, C3, C2, C5, **C11** | Rate limit `/auth/login` ; TLS par le `deploy/Caddyfile` ; handler global + corrélation ; tâche `check-health` enregistrée ; runbook à 3 pannes ; **exigences matérielles écrites (onduleur, disques en miroir)** | Observabilité complète, canal d'alerte sortant, 10 pannes |
| **D13** protection de la PI | L'installeur consomme un binaire publié en amont au lieu de cloner le dépôt (~5 j-a) | Signature SmartScreen (C7), chaîne de livraison complète |
| **C13 documentation — RÉDACTION, pas relecture** | Jouer `tools/generate-guide.ps1` puis **écrire les trois guides** (réception, comptabilité, direction). 9 j-a (L2.6 + L3.3). **Support de la formation attestée W8.4, donc dans le chemin critique de la mise en service** | Les 9 autres guides par rôle. *Correction de la v1 : le lot dormant `agent-a3b07db6dff7d6ed5` **ne contient aucun guide** — `docs/guide/` n'y existe pas ; il porte 21 fiches techniques de modules et 8 documents d'architecture, comptés sous documentation technique* |
| R30 recette | Les seuls écrans du pilote ; les autres masqués (L0.2) | Chaque écran retiré = 36 contrôles en moins |

### Différé après le pilote — conséquence assumée

| Point | Charge retirée | Conséquence assumée |
|---|---|---|
| **A6b** filtrage du périmètre unité | 39 j-a + porte L1.0 | Chez un mono-établissement, `hotelUnitCode` n'a qu'une valeur légitime. **Prix : branche B du §2** |
| **B1** identité fiscale par établissement | 11 j + jeton EF | Conforme pour un établissement. **Indispensable au deuxième — branche B** |
| B5 automatisme taxe de séjour ; B2b registre de police logiciel | 14 j ; 11 j | B5 : voir « contourné ». B2b : registre papier, **à écrire au contrat** |
| C4 diagnostic à distance, C7 installeur signé, C8 garde de compatibilité | 13 j-a | Un seul site, le propriétaire y est 7 semaines et livre lui-même les postes |
| **D5** couche Application, **D7** OpenAPI, **D8** volumétrie, **D10** suite E2E | 4 points, « mois » chacun à l'avis 08 | Dette structurelle. **D8 est une bombe à retardement : `GET /accounting/entries` n'est pas paginé.** Le système marchera parfaitement la première année — c'est précisément ce qui la rend invisible au pilote |
| D6, L5.3, L6.3, L4.3, lot ports (L0.6) | 27 j-a | Dette et outillage. **Exception gardée : D1/L2.5 (`TreatWarningsAsErrors`)** |

### Contourné à la main — l'obligation reste due

| Point | Contournement | Condition non négociable |
|---|---|---|
| B5 taxe de séjour | Posée à la main : `ChargeKind.Tax` est déjà autorisé (ExtraItem.cs:43, :151) | Procédure écrite, **contrôle quotidien**, taux obtenu (S1). La collecte reste légalement due |
| B10 déversement comptable | Rapports mensuels + export CSV | **Trou du plan 09 à boucher :** l'exclusion de B10 est conditionnée à « l'export des livres est livré », phrase unique (l. 1360) qu'aucun lot ne porte. **Doté ici comme lot de B3, 5 j-a** |
| C10 reprise de données | **Exiger un hôtel vide au contrat** | Si l'hôtel n'est pas vide : +4 à 8 sem. (§2, branche B) |

### Refusé au contrat — par une clause écrite, pas par omission

POS restaurant, MICE, channel manager, moteur de réservation directe, PortMaster, paie (B8 + D4), immobilisations
(B13), états financiers SCF (B11), G50 et liasse fiscale (B9), arabe et RTL (D11), multi-unités, licence à échéance
(D12), toute reprise de données, tout engagement de disponibilité — **et le solde de B14 au-delà du minimum retenu**
(registre des traitements, purge automatique, droits d'accès/rectification/effacement) : obligation légale, donc
exclue **par une clause écrite**, mentionnant que la formalité 18-07 reste à la charge de l'hôtelier. *(Plan 09 §5.2
et §5.3. L'arbitrage POS de S1 doit conclure à « mis de côté ».)*

**Les deux paris et leur vérification.** (1) Établissement unique — **vérifié par W8.1, porte bloquante de B2** (le
plan 09 l'impose déjà « avant V2 »). B1 démarre sans, son contenu étant neutre au périmètre unité. Si W8.1 n'est pas
faite à la fin de B1, B2 attend, ou part sur l'hypothèse et la branche B s'applique. (2) Hôtel vide : même porte,
même conséquence.

---

## 4. Ce qui commande vraiment le calendrier

| # | Goulet | Chiffre | Ce qu'on fait pour le réduire |
|---|---|---|---|
| 1 | **Disponibilité du propriétaire** | ≈ 96 j-h. À 3,5 j-h/sem. = 27 sem. ; à 5 = 19 ; à 2 = 48 | **Le paramètre le plus sensible, et non mesuré.** Le trancher en S1 : combien de jours, et qui intègre quand il s'arrête |
| 2 | **Débit d'intégration — NON MESURÉ** | Hypothèse : 2 à 4 lots/sem. ; ~30 lots = 8 à 15 sem. **Aucune mesure ne la soutient.** | Les fusions relevées (1 min 19, 1 min 40, 59 s, 2 min 27, **7 secondes** entre `9bfc4c3` et `da196a7`) prouvent que **les six vérifications du §7.2 n'ont jamais été appliquées** : une solution .NET 10 ne se construit pas et 948 tests ne s'exécutent pas en 7 secondes. **Action : chronométrer le premier lot de B1 avec build + suite complète + gardes readiness, PostgreSQL et RBAC + relecture réellement joués, et recalculer ce goulet.** Jusque-là, non mesuré au même titre que le goulet 1 |
| 3 | **Dépendances officielles** | **4 demandes envoyables en semaine 1**, 2 à 6 sem. chacune, en parallèle | Elles se consomment pendant S1 + B1 (8 sem.). **Gain réel : 2 à 5 semaines**, et non 4 à 8 |
| 4 | **File pilote** | 7 à 14 sem. Aucun hôtel n'est identifié | **Le vrai chemin critique, et il n'est pas dans le dépôt.** W8.1 doit tenir dans les 8 semaines de S1+B1, sinon B2 attend ou la branche B s'applique. Sous-traitable : la rédaction et la relecture du contrat |
| 5 | **Recette et mise en service** | R30 réduite 2-2,5 sem. ; W8.3 1,6 sem. ; **W8.5 double saisie 4 sem. incompressibles** | **Ni les agents, ni le propriétaire, ni un budget ne raccourcissent un mois.** |
| 6 | **Taux de perte des lots** | **4 lots sur 4 perdus** sur la seule vague dont on connaisse le sort | D'où la **provision de reprise de 20 %** au §2, et la règle : lancer 2 lots à la fois et les fermer, plutôt que 4 et les perdre. **Aucun lot ne survit à la nuit** |

**Les 4 lots dormants ne sont pas des livrables.** Modifications **non commitées**, jamais construites, jamais
testées, jamais passées en CI, basées sur `cbd5e6a` — **avant** les neuf commits de la refonte de l'accueil. Deux
touchent des fichiers réécrits depuis (`agent-a7246a03` → `MainWindow.Navigation.cs`, `agent-a3b07db6` →
`docs/desktop-client.md`, tous deux dans le diff `cbd5e6a..reorg/phase-1`) ; 113 fichiers, 55 suivis modifiés.
**Ils ont donc leur propre lot en S1** — rebase, conflits, build, 948 tests, trois gardes — chiffré 14 j-a et
2 j-h d'intégration, pas les ~5 h de la version 1.

---

## 5. Le chemin resserré, semaine par semaine (branche A)

| Sem. | Agents | Propriétaire |
|---|---|---|
| 1 | *(aucun en vol)* | 3 décisions du §6. Pousser les 10 branches (D14). **Envoyer les 4 demandes extérieures.** **Ouvrir la prospection du pilote — le poste le plus urgent du dossier** |
| 2-3 | **Lot de sauvetage des 4 lots dormants** : rebase, conflits, build, 948 tests, gardes | Intégrer le lot de sauvetage (2 j-h). Arbitrage POS, inventaire des écrans figé, EULA en rédaction. **Prospection** |
| 4 | Socle B1 outillé, série 1 : A1+A8, A2+A11+C1, A9+D9, A10 | Intégrer. **Chronométrer la première fusion avec les six vérifications** (§4 goulet 2) |
| 5-6 | **B7** (8 j, jeton EF), puis A6a, D1, C3 min. en parallèle | Intégrer. Réponses attendues sur les textes. **W8.1 dès qu'un hôtel est identifié** |
| 7-8 | **B6** (7,5 j, après fusion de B7, même agent), D3, B12 min. + **B14 min.** | Intégrer. **Relecture comptable de B6/B7 par un expert-comptable (2 j-h)** |
| 9 | **Porte : W8.1 faite ?** Si oui, B2 démarre. Si non, B2 attend, ou branche B | Porte L5.0 (2 j-h) franchie **avant** le lancement d'A5 |
| 10-11 | B2 outillé : A4, puis **A5 minimal (gabarit facture)** | Intégrer. **Envoyer le gabarit en relecture dès sa fusion** (2-4 sem. → retour sem. 13-15). Contrat chez le conseil |
| 12-15 | **B3 — avoir** (16 j, chaîne) | Intégrer. **Contrat W8.2 signé attendu ici au plus tard.** Écrire la procédure de pose manuelle de la taxe |
| 16-17 | **B4 — droit de timbre** (13 j, après fusion de B3, même agent) *(ne démarre pas sans le barème)* ; en parallèle B2a min. et A7 min. | Intégrer. Relancer les demandes sans réponse |
| 18-22 | B3 exploitation : A12+C9, A3 min.+D13 min., C2/C5, C3/C12, C6/C11, D2b, export des livres, **rédaction des 3 guides** | Intégrer 13 lots. **PV de restauration devant témoin (H1.4)**. Relire les guides |
| 23-26 | Reprise des réserves de recette (20-30 j-a) | **Recette R30 réduite, 2 passes.** Démonstration intégrée, 3 PV |
| 27-33 | 10 h réservées aux correctifs de terrain | **Mise en service :** W8.3, **W8.4 formation attestée sur les guides de C13**, **W8.5 mois de double saisie**, W8.6 |

*Semaines 4, 10-11, 18-22 : dimensionnées par l'intégration. Semaines 5-8 et 12-17 : par du travail de conformité
que rien ne comprime. Semaines 23-33 : par le propriétaire et le terrain. Les agents finiront leurs séries outillées
en quelques heures et attendront — c'est normal, et c'est le résultat de la mesure.*

---

## 6. Les trois décisions à prendre cette semaine

1. **Le pilote est-il un établissement unique, et est-il déjà identifié ?** C'est le seul point de bascule : la
   branche A vaut 33 semaines, la branche B 44, et la branche B est l'**issue par défaut** tant qu'aucun hôtel n'est
   identifié. Si un hôtel est déjà acquis et vide, W8.1 tient dans S1 et le programme reste à 30-33 semaines.
2. **Combien de jours par semaine le propriétaire donne-t-il réellement, et qui intègre quand il s'arrête ?** Le
   calendrier est proportionnel : 19 semaines à 5 j-h, 27 à 3,5, 48 à 2. C'est le seul chiffre du plan que personne
   n'a mesuré — avec le débit d'intégration, désormais reconnu comme non mesuré lui aussi.
3. **Envoyer aujourd'hui les 4 demandes réellement envoyables** — champs de la fiche de police, taux de taxe de
   séjour, barème du droit de timbre, statut de la formalité 18-07. Coût : une demi-journée. Effet : **2 à 5
   semaines** retirées du chemin critique. *(Les deux relectures professionnelles ne sont pas envoyables en semaine
   1 : le gabarit n'existe qu'en semaine 11, le contrat qu'une fois W8.2 rédigée ; elles partent à la fusion de leur
   prédécesseur.)* **Hygiène, sans arbitrage :** pousser les 10 branches worktree ; **commiter
   `docs/reorganisation/`, qui n'est versionné nulle part** ; figer l'inventaire des écrans du pilote.

---

## 7. Ce qui reste vrai du plan 09 — et la contrainte qu'on lève

| Élément du plan 09 | Statut |
|---|---|
| Les **lots** L0.x à L7.x et leurs périmètres exclusifs ; les **critères d'acceptation** ; les **gardes CI** (dont G2) | **Valables sans exception.** Seul le sous-ensemble du §3 est lancé ; aucune réduction n'autorise à désarmer un garde |
| Les **règles d'intégration** du §7.2 (six vérifications par PR) | **Valables, et jamais appliquées à ce jour** — les fusions de 7 s à 2 min 27 le prouvent. Coût réel à chronométrer sur le premier lot de B1 |
| **W8.1 avant V2** | **Valable et durci** : W8.1 devient une **porte bloquante de B2**, inscrite en semaine 9 du §5 |
| **W8.2 signé avant V5** | **LEVÉE**, et voici ce qu'on paie pour cela. A5 (gabarit de facture) est écrit en semaine 11, le contrat est signé en semaine 15 au plus tard : la pièce opposable précède le contrat qui en définit le périmètre. **Conséquence assumée : provision de 5 j-a de reprise du gabarit après signature**, et interdiction d'émettre une facture réelle chez le pilote avant W8.2 signée (la vague M ne démarre pas sans) |
| Le **jeton EF** et les règles de sérialisation des migrations | **Valables** |
| Les **exclusions** du §5.2 et les **refus écrits** du §5.3 | **Valables et durcis**, complétés au §3 par les 9 points que ni le plan 09 ni la version 1 de ce document ne classaient : B14 (min. retenu, solde refusé au contrat), C9 et C11 et D13 (retenus en B3), D5, D7, D8, D10 (différés, conséquence écrite) |
| Le **relevé du temps consommé par lot** (§7.1) | **À exécuter**, aux horodatages de **lancement d'agent**. Le facteur de 1,2 h/j-a du §1 est un plafond provisoire, à confirmer ou infirmer à la fin de B1 |
| L'**hypothèse de capacité du §2.1** et le **tableau des vagues du §2.2** | **Remplacés** par le §2 ci-dessus |

**Prudences.** Aucun build, aucun test, aucun garde n'a été exécuté pour produire ce document — 948 est un comptage
d'attributs, pas un résultat vert. Les délais extérieurs sont des **estimations raisonnées, pas des mesures**. Le
facteur de 1,2 h/j-a est mesuré sur deux lots de navigation, RBAC, WPF, CI et conception ; **il n'est appliqué à
aucun point de conformité algérienne**, et le §2 en tire la conséquence chiffrée au lieu de la mentionner. Enfin,
aucune obligation légale algérienne n'est vérifiable depuis le dépôt : les 7 obligations non négociables viennent de
l'avis 08 §3(b), pas d'une source primaire, et doivent être confirmées avant d'être opposées à un client.
