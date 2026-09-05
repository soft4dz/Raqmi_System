# 09 — Plan de développement

**Objet.** Ce document transforme les 43 points de `08-avis-etat-du-projet.md` en un plan que le
propriétaire peut suivre semaine après semaine, jusqu'à l'ouverture d'un premier client pilote. Il
remplace la partie calendaire de `07-plan-migration.md`, dont il conserve la gouvernance (niveaux de
test, fiches de module, matrice de readiness) et dont il corrige l'ordre.

**Base d'analyse.** `reorg/phase-1` @ `da196a7`, **et `origin/feature/personalized-homepage`**, qui porte
9 commits d'avance, un module POS complet et deux migrations EF sur une lignée divergente (§4.1). Toutes
les mesures citées ont été prises sur ces révisions ou dans les quatre worktrees d'agents. Aucune n'a été
obtenue par exécution : le dossier d'instruction a été constitué en lecture seule, build et tests
interdits — et c'est la première limite de ce plan (§8.2).

**Révision.** Ce document a été corrigé après une revue adverse portant sur trois axes : l'exécutabilité
(périmètres réellement disjoints, critères jouables par une commande réelle), la sincérité des charges
et des durées, et les angles blancs de la mise en service. Les corrections portent partout sur les
chiffres, la découpe des lots et le calendrier. **Le principal changement à connaître : le programme
complet ne vaut pas 34 semaines mais 55 à 59 semaines de travail, et il n'est plus daté.**

---
## 1. Ce que ce plan décide

**L'objectif est un premier client pilote en production**, pas un produit complet. La stratégie est
celle de l'avis : **finir la chaîne avant d'élargir le catalogue.** Un hôtel qui peut réserver,
encaisser, facturer légalement, imprimer ses pièces et restaurer sa base vaut mieux que cinquante
portes de menu dont vingt s'ouvrent sur rien.

Cinq arbitrages commandent tout le reste.

**Arbitrage 1 — A6 n'est pas fait, il est commencé au tiers.** Le lot qui dort dans
`agent-a7246a035c2ecd403` livre l'entité d'affectation, le claim de périmètre, le jeton,
l'administration et un filtre HTTP. Il ne livre pas le filtrage dans les services :
`grep -rl "IUnitScopeProvider" src/RaqmiSystem.Infrastructure` ne remonte que `UnitScopeProvider.cs` et
son enregistrement — **aucun service métier ne l'injecte** — et l'en-tête de
`UnitScopeEndpointFilter.cs` documente lui-même ses deux trous (« les listes sans parametre d'unite
[…] rendent aujourd'hui toutes les unites », « les routes qui n'identifient l'unite qu'apres
chargement »). Le point est donc scindé en **A6a** (livré, 3 j-a de complément et de fusion, en V0) et
**A6b** (15 à 39 j-a selon l'issue de la porte L1.0, en V1). Cocher A6 à la fusion du worktree serait
l'erreur la plus coûteuse possible, puisque l'avis fait de A6 le préalable à tout nouvel écran.

**Arbitrage 2 — le plafond de parallélisme n'est pas la migration EF, c'est le lot indivisible.**
`RaqmiDbContextModelSnapshot.cs` pèse 401 133 octets (mesuré) et une seule migration peut être
**générée** à la fois. La version antérieure de ce plan convertissait cette contrainte de simultanéité
en une contrainte de **fréquence** — « une seule migration par vague de 4 à 6 semaines », soit un
facteur d'environ 30 — et facturait cinq semaines de calendrier pour cela. L'historique du dépôt la
réfute : **22 migrations en cinq jours**, dont deux séparées de 19 secondes et deux portées par des
commits de 16 h 51 et 17 h 41 le même après-midi. **Sérialiser deux migrations coûte quelques heures de
file d'attente, pas un créneau de vague** (règle 2, §7.2). Le vrai plafond est ailleurs : **un lot est
tenu par un seul agent, et un lot de 24 jours-agent occupe cet agent sept semaines, quel que soit le
nombre d'agents disponibles.** C'est cette contrainte-là, et non la migration, qui commande la découpe
et la durée des vagues (§2.2).

**Arbitrage 3 — la vague 1 de l'avis n'est pas de 3 à 4 mois, et ce plan n'est pas de 34 semaines
non plus.** La chaîne dure A6 → B1 → A4 → A5 compte quatre maillons. La version antérieure concluait à
34 semaines en divisant la charge de chaque vague par quatre agents ; **elle ignorait que chaque vague
contient un lot indivisible dont la charge dépasse ce qu'un agent produit pendant la vague**, et elle
comptait cinq semaines de sérialisation de migrations qui n'existent pas. Les deux corrections jouent en
sens contraire, et la seconde ne compense pas la première : le programme complet, mise en service chez le
client comprise, vaut **55 à 59 semaines de travail**, plus les marges nommées et les jours non ouvrés.
Le §5 de l'avis n'est pas révisé à la baisse : il est révisé à la hausse, et pour des raisons mesurées.

**Arbitrage 4 — la paie sort du périmètre pilote.** B8 (calcul IRG faux) et D4 (aucune contrainte de
base RH) exigent une validation par un expert-comptable agréé avant la première ligne de code, et
l'avis range déjà la paie parmi les refus écrits du contrat pilote. Ils sont traités après le pilote,
et le module RH est grisé d'ici là. C'est le seul point de niveau (b) que ce plan déplace hors du
pilote, et il le fait en suivant l'avis, pas contre lui.

**Arbitrage 5 — le jalon technique n'est pas l'ouverture de l'hôtel.** La version antérieure appelait
« premier client pilote » un jalon dont les quatre lots avaient pour périmètre des fichiers du dépôt :
aucun client identifié, aucune visite de site, aucun contrat, aucune installation, aucun paramétrage
initial, aucune formation, aucun accompagnement, aucun modèle de support, aucun scénario d'échec.
**La mise en service devient une vague à part entière, V8 (§3.8)**, avec ses charges, ses délais
externes et ses critères d'arrêt écrits — et deux de ses lots, la visite de site et le contrat, sont des
**dépendances de calendrier** de V2 et de V5, pas des formalités de fin.

**Contrainte de date.** La clause `documentationGrace` de `tools/readiness/screens.json` expire le
**31/12/2026** : elle couvre 21 des 30 écrans (vérifié : `documentationGrace.screens` contient 21
entrées, `until = "2026-12-31"`). Passé cette date, ces écrans retombent en `TechnicalPreview` et la CI
passe au rouge par conception. **Ce plan ne repousse pas cette date : il la supprime en S1**, en
intégrant le lot de documentation qui dort dans `agent-a3b07db6dff7d6ed5` et qui ramène
`documentationGrace.screens` à `[]`. C'est l'action 4 de S1, et elle coûte une journée et demie.

---

## 2. Vue d'ensemble

### 2.1 Hypothèse de capacité

> **Réserve à lire avant le tableau.** Le rendement retenu ci-dessous est **une hypothèse de travail,
> pas une mesure** (§8.2). Aucune charge de ce document n'a été recalculée contre la vélocité réelle du
> dépôt, et la version antérieure affirmait à tort que l'étalon était « mesuré dans les quatre
> worktrees ». La **topologie** du plan — l'ordre, les dépendances, les collisions de fichiers, les
> périmètres — est vérifiée dans le code. Sa **durée** ne l'est pas. **C'est pourquoi ce document ne
> publie plus aucune date, seulement des durées relatives et conditionnelles.** Le premier chiffre
> opposable sera produit à la fin de V0, en relevant le temps réellement consommé par lot (§7.1).

**Deux unités de compte, jamais additionnées.**

| Unité | Ressource | Débit | Ce qu'elle couvre |
|-------|-----------|-------|-------------------|
| **jour-agent (j-a)** | 4 agents en parallèle, worktrees isolés | **3,5 j-a par agent et par semaine** (5 jours × 0,70), soit 14 j-a/semaine au total | l'écriture, les tests, le rebasage et la mise en état de fusion d'un lot |
| **jour-homme (j-h)** | le propriétaire, **une seule personne** | **3,5 j-h par semaine**, dont 2 d'intégration continue et de revue | décision, arbitrage, intégration, recette, procès-verbaux, formation, mise en service |

L'abattement de 30 % sur les jours-agent couvre le rebasage, la revue, l'attente d'intégration et les
temps morts imposés par les deux jetons rares (migration EF, coquille WPF).

**Ce que la version antérieure additionnait à tort.** Elle définissait le j-h comme un jour-agent
(« 4 agents × 5 jours × 0,70 ») puis comptait dans la même unité la semaine 1 « intégrateur seul », la
recette humaine, la démonstration devant témoin et les procès-verbaux. Aucun agent n'accélère ces
postes, et ils n'apparaissaient nulle part sur le chemin critique. **Ils imposent un plancher que le
nombre d'agents ne déplace pas**, et ils sont désormais comptés à part et portés au §2.3.

**Ce qui fixe la durée d'une vague.** Trois contraintes, dont on prend le **maximum** :

```
duree_vague = max(  charge_agent_totale / 14
                  , charge_du_plus_gros_lot / 3,5
                  , longueur de la plus longue chaine sequentielle interne / 3,5
                  , charge_humaine_de_la_vague / 3,5  )
```

**C'est la deuxième ligne qui commande, presque partout.** Un lot est tenu par un seul agent — c'est la
condition même du périmètre de fichiers exclusif — donc un lot de 24 j-a occupe cet agent 6,9 semaines,
et aucun effectif supplémentaire ne rattrape l'écart. La version antérieure ne retenait que la première
ligne : elle divisait la charge totale par 4 agents et publiait des vagues deux fois trop courtes.
**Chaque vague porte désormais son tableau d'affectation d'agents**, et la ligne qui commande y est
signalée.

Si le propriétaire développe seul et à la main, appliquer un facteur de 1,3 à 1,6 aux jours-agent :
l'hypothèse est calibrée sur des agents, pas sur un développeur isolé. Les jours-homme, eux, ne bougent
pas.

### 2.2 Tableau des vagues

Aucune date. Chaque vague démarre quand la précédente est **fusionnée**, pas quand ses agents ont fini
d'écrire.

| Nº | Nom | Objectif | Durée | j-agent | j-homme | Semaines imposées par le lot dominant | Jeton EF | Jalon de sortie |
|----|-----|----------|-------|---------|---------|----------------------------------------|----------|-----------------|
| S1 | Récupération et décisions | Aucun lot critique ne vit plus sur un seul disque ; les six arbitrages ouverts sont écrits | **3 sem.** | 0 | 9,2 | 9,2 / 3,5 = **2,6** | aucun (aucun code) | 5 branches réconciliées ; `documentationGrace` vidée ; inventaire des écrans figé |
| V0 | Débloquer | Une journée de travail devient possible ; la CI peut dire non | **5 sem.** | 60 | 2,5 | L0.1→L0.5→L0.4a = 17,5 / 3,5 = **5,0** | L0.7 → L0.8 → L0.3a → L0.3b | Premier run vert de `postgres-integration` sur la branche ; 3 lots dormants fusionnés |
| V1 | Périmètre et exploitation | Un utilisateur ne voit que son unité ; une sauvegarde se restaure | **5 à 6 sem.** | 30 à 58 | 7 | découpe A : L1.2→L1.5 = 16 / 3,5 = **4,6** — découpe B : L1.1′ = 20 / 3,5 = **5,7** | A : L1.5 — B : L1.1′ | Restauration prouvée devant témoin ; isolation verte module par module |
| V2 | Identité fiscale et socle API | Une facture porte le NIF de son unité et une série cloisonnée | **5 sem.** | 52 | 2,5 | L2.3→L2.5 = 16 / 3,5 = **4,6** | L2.1 | Deux unités facturent le même jour sans collision de série |
| V3 | Identité du séjour | On sait qui dort dans la chambre, combien ils sont, et ce qu'on a le droit d'en garder | **4 sem.** | 37 | 3,5 | L3.0→L3.1a = 13 / 3,5 = **3,7** | L3.1a puis L3.1b | Un séjour porte ses occupants ; position 18-07 écrite |
| V4 | Encaissement et folio | L'argent du comptoir est rattaché à une caisse et à une facture | **7 sem.** | 53 | 2,5 | L4.1a→L4.1b = 24 / 3,5 = **6,9** | L4.1a | Main courante du jour recoupée au dinar près |
| V5 | Chaîne documentaire | Une pièce opposable est produite, archivée et remise | **6 à 8 sem.** | 33 à 42 | 4 | L5.1a = 18 à 27 / 3,5 = **5,1 à 7,7** | L5.1a | Facture réimprimable par son numéro après redémarrage |
| V6 | Conformité de la facture | On corrige par un avoir, plus par une annulation | **8 sem.** | 45 | 2,5 | L6.1a→L6.1b = 29 / 3,5 = **8,3** | L6.1a | `POST /invoices/{id}/cancel` sur une facture émise renvoie 409 |
| V7 | Recette et build candidate | Une build passe toutes les portes du §5.1 | **6 sem.** | 28 | 18 | recette humaine = 18 / 3,5 = **5,1** | aucun (libre) | Deux passes de recette ; 3 PV signés ; `pilot-gate` vert |
| V8 | Mise en service chez le pilote | Un hôtel s'en sert | **6 à 7 sem.** | 10 réservés | 31 | 31 / 5 = **6,2** *(terrain, sans abattement)* | aucun | Formation attestée ; mois de double saisie clos sans critère d'arrêt déclenché |

**Total : 55 à 59 semaines de travail, 338 à 375 jours-agent, ≈ 83 jours-homme du propriétaire.** À quoi
s'ajoutent **quatre semaines de marge nommée** (après V0, V2, V4 et V6) et les jours non ouvrés
algériens, Ramadan et Aïd compris (§7.1). Ordre de grandeur du programme complet : **60 à 65 semaines**.

**Comment lire ce tableau contre la version antérieure.** Elle annonçait 34 semaines et 384,5 « j-h ».
L'écart tient à cinq causes mesurées, et à aucune ambition nouvelle : *(i)* les durées de vague étaient
calculées en divisant la charge totale par 4 agents, alors que chaque vague contient un lot indivisible
plus long que cela — à lui seul, cet écart valait environ 16 semaines ; *(ii)* la semaine 1 demandait
12,5 j-h à une seule personne en cinq jours ouvrés ; *(iii)* la mise en service chez le client n'était
chiffrée nulle part ; *(iv)* la recette de 540 contrôles était budgétée à 4 j-h en une seule passe ;
*(v)* en sens inverse, les cinq semaines facturées à la sérialisation des migrations sont retirées,
parce qu'elles reposaient sur une conversion fausse.

**Ce qui n'est pas dans ce total** : le support du client en exploitation après V8 (§6), la
réconciliation de la branche accueil/POS si l'arbitrage de S1 conclut à une fusion (chiffrée en S1 mais
seulement dans son minimum de 4 j-h), et les quatre fourchettes ouvertes que le plan reconnaît (A6b,
A4, A5 bilingue, lecture B2 → A5).

### 2.3 Chemin critique

Les flèches sont des dépendances **dures** : le maillon aval ne peut pas commencer avant que l'amont
soit fusionné. **Les postes humains y figurent désormais** : ils ne sont pas accélérables par le nombre
d'agents, et deux d'entre eux conditionnent des vagues entières.

```
  S1        V0           V1              V2          V3         V4         V5           V6        V7        V8
 3 sem     5 sem       5-6 sem          5 sem       4 sem      7 sem     6-8 sem       8 sem     6 sem    6-7 sem
   |         |            |               |           |          |          |            |         |         |
[A6a] ---> [ socle ] --> [A6b] -------> [B1] ------------------> [A4] ---> [A5] ------------------> [R30] -> [MES]
 lot        CI +          filtrage       identite                 facturation  chaine                recette   sur
 dormant    argent +      dans les       fiscale                  du folio     documentaire          2 passes  site
 fusionne   horloge       services       par unite                    ^          |                      ^        ^
                            |                                         |          |                      |        |
                            |                                         +----------+                      |        |
                            |                                                    |                      |        |
                            +--> [B2a] ------------------> [B5] taxe de sejour    +--> [B2b] registre ---+        |
                                  identite du sejour                                    de police                |
                                                                                                        |        |
                                                                        [B3 + B4] avoir + timbre -------+        |

 Postes HUMAINS sur le chemin, non compressibles par le nombre d'agents :
   [S1 decisions 9,2 j-h] --> ... --> [L1.0 arbitrage A6b 3 j-h] --> ...
   [W8.1 visite de site 3 j-h] --------> avant V2
   [L3.0 position 18-07 1 j-h] --------> avant L3.1a
   [W8.2 contrat + EULA 5 j-h] --------> avant V5      [L5.0 decision PDF 2 j-h] --> avant L5.1a
   [L7.1 recette 12 j-h, 2 passes] --> [L7.3 demo + 3 PV 6 j-h] --> [V8 mise en service 31 j-h]

 Dependances EXTERNES, sans lesquelles la vague ne demarre pas :
   champs de la fiche de police --> avant V3      taux de taxe de sejour --> avant V4
   bareme du droit de timbre ----> avant V6       relecture du modele de facture --> avant L5.1a

 Branches independantes, jamais sur le chemin critique :
   [A2] --> [A12] --> [C2]        [A11] --> [D1]        [A1] --> [C13] --> [R30]
    GRANT   sauvegarde  alerte      CI       gardes      session   guides
            restauree
```

**Lecture.** La plus longue chaîne **de dépendances** reste `A6a → A6b → B1 → A4 → A5 → recette → mise
en service`. Mais la longueur du **calendrier** n'est plus donnée par cette chaîne : elle est donnée par
la somme des vagues, dont deux — V4 et V6 — ne sont pas sur le chemin de dépendances et durent pourtant
sept et huit semaines, parce que leur lot dominant est indivisible. **Si l'on voulait comprimer le
calendrier, ce n'est pas la chaîne qu'il faudrait raccourcir, c'est V4 et V6 qu'il faudrait chevaucher
avec V5** — ce qui suppose un cinquième agent et un cinquième détenteur de `BillingService.cs`, donc
un arbitrage sur la règle 1. Ce plan ne le fait pas, et le dit.

**Le seul levier qui raccourcit la chaîne de dépendances** est la scission de B2, retenue ici : **B2a**
(nationalité, type et numéro de pièce, décompte des occupants — ne dépend que d'A6) part en V3 sur la
branche parallèle ; **B2b** (registre de police imprimé et remis — dépend d'A5) part en V6. Sans cette
scission, B2 entier dépendrait d'A5 et la chaîne compterait deux maillons de plus, soit environ six
semaines.
*Réserve à lever par le propriétaire :* cette scission suppose que la dépendance « B2 → A5 » écrite
dans l'avis porte sur le registre **imprimé** et non sur un stockage de pièce scannée. Si c'est la
seconde lecture qui est la bonne, ajouter six semaines.

**Trois autres leviers, tous à un prix écrit.** *(i)* Reporter B4 (droit de timbre) après le pilote
ramène V6 de 8 à 5 semaines — mais c'est un refus de conformité à écrire au contrat (§3.6). *(ii)*
Reporter la parade R-B après le pilote ramène V4 de 7 à 4 semaines — mais le risque R-B reste ouvert le
jour de l'ouverture (§3.4). *(iii)* Livrer A5 avec deux modèles de documents au lieu de cinq est déjà
intégré au chiffrage (L5.1a / L5.1b, §3.5). Aucun de ces leviers ne se prend en silence.

---

## 3. Les vagues

Convention commune à toutes les vagues :

- **Périmètre exclusif, et nominatif.** Les listes de fichiers d'une même vague ne se recoupent jamais.
  **Un périmètre s'énumère fichier par fichier ; un joker de répertoire est interdit dès qu'un
  `*Configuration.cs` s'y trouve** — c'est la règle 7(a), et la version antérieure de ce document ne se
  l'appliquait pas : le joker `Accounting/**` de L1.2 avalait deux des trois fichiers déclarés exclusifs
  à L1.5, et douze autres avec eux. Quand un lot a besoin d'une ligne dans un fichier détenu par un
  autre, il livre un **fragment de patch** (bloc à insérer + emplacement) dans son message de livraison,
  et l'intégrateur l'applique dans le lot du détenteur. On ne fusionne pas un fichier partagé : on
  réordonnance.
- **Un périmètre doit contenir ce que le lot est obligé d'écrire.** Fichiers de migration et
  `RaqmiDbContext.cs` pour un détenteur du jeton ; chemins de test pour tout lot dont un critère
  d'acceptation ou un garde est un `dotnet test`. Sinon la règle 6.5 rejette une PR qui a pourtant bien
  fait son travail.
- **Une seule migration EF *en vol* à la fois** (règle 2). Plusieurs lots d'une même vague peuvent en
  porter une, à condition d'être sérialisés par le registre écrit du détenteur, chacun rebasant sur le
  snapshot fusionné. Rappel mécanique : `RaqmiDbContext.cs:273` appelle `ApplyConfigurationsFromAssembly`,
  donc **ajouter un `*Configuration.cs` suffit à rendre rouge `HasPendingModelChanges()`**, même sans
  toucher au `DbContext`. C'est exactement le piège dans lequel les lots A6 et D2 sont tombés.
- **Un seul détenteur de la coquille WPF par vague** (`MainWindow*`, `ModuleCatalog.cs`,
  `Themes/RaqmiTheme.xaml`). Son lot est court et fusionné en dernier parmi les lots Desktop.
- **Chaque vague publie son tableau d'affectation d'agents**, avec la charge cumulée par agent et la
  ligne qui commande sa durée. Une vague dont un agent porte plus de `durée × 3,5` jours-agent n'est pas
  planifiable telle quelle.
- **Fusion par pull request**, jamais par poussée directe. `.github/workflows/dotnet.yml` déclare
  `pull_request:` **sans filtre de branche** (vérifié, lignes 3-9) et le job `postgres-integration`
  n'a aucune condition `if:` — une PR vers `reorg/phase-1` déclenche donc le gate PostgreSQL dès
  aujourd'hui, sans modifier un octet de workflow.
- **Le gel fonctionnel de `docs/stabilization/module-readiness.md` interdit ce que V2 à V6 livrent.**
  Sa liste exhaustive de changements autorisés — bugs, sécurité et permissions, navigation et
  ergonomie, fiabilisation API/DB, tests, documentation, performance, sauvegarde/déploiement/
  observabilité — ne couvre ni `StayOccupant`, ni la session de caisse, ni la taxe de séjour, ni le
  projet `RaqmiSystem.Documents`, ni l'avoir, ni le registre de police. **La décision écrite qui lève ou
  amende ce gel est l'action 6 de S1** ; sans elle, soit le gel est fictif, soit chaque vague de V2 à V6
  est en infraction avec la règle que l'intégrateur applique à chaque fusion.

---

## 3.0 Vague 0 — Débloquer (5 semaines, 60 j-a)

**Objectif.** Rendre une journée de travail possible et donner à la CI le pouvoir de dire non.

**Ce qui devient possible à la fin.** Un utilisateur reste connecté trois heures ; une panne réseau se
voit en quinze secondes ; la sauvegarde contient les 19 schémas ; la balance auxiliaire affiche un
chiffre juste ; aucune écriture ne se poste sans période ni numéro ; les trois lots dormants sont
fusionnés ; et toute régression sur ces acquis rend la CI rouge — **à l'exception de G7 et G8, dont la
portée est précisée plus bas.**

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L0.1** | A11, **C1a + C1b**, A2, D14, règle 8 | `.github/**` (dont `CODEOWNERS`, `dotnet.yml`, l'étape de la règle 8(b) et l'étape CI du projet `RaqmiSystem.Desktop.Tests`) ; `CONTRIBUTING.md` ; `Directory.Build.props` ; les 6 `*.csproj` ; `deploy/postgres/create-app-role.sql` ; `deploy/installer/RaqmiSystemDesktop.iss` ; `tests/RaqmiSystem.Tests/Postgres/SchemaGrantCoverageTests.cs` (nouveau) ; **`src/RaqmiSystem.Api/Program.cs`** (3 lignes, C1b — détenu pour la vague, fusionné **après** L0.7 et L0.8) | 6 j-a | aucune | DevOps / CI |
| **L0.2** | A1, A8, **coquille WPF** | `src/RaqmiSystem.Desktop/Api/**` (30 fichiers) ; `MainWindow.xaml.cs`, `MainWindow.xaml`, `ModuleCatalog.cs`, `Themes/RaqmiTheme.xaml` ; `tests/RaqmiSystem.Desktop.Tests/**` (nouveau projet) ; `RaqmiSystem.sln` | 12 j-a | aucune | WPF + HTTP |
| **L0.3a** | B7 | `src/RaqmiSystem.Domain/Accounting/**` ; `Application/Accounting/**` ; `Infrastructure/Accounting/**` ; `Api/Endpoints/AccountingEndpoints.cs` ; **`Infrastructure/Persistence/Migrations/**`** ; **`Infrastructure/Persistence/RaqmiDbContext.cs`** ; `tests/RaqmiSystem.Tests/AccountingCoreTests.cs` | 8 j-a | aucune | Comptabilité SCF |
| **L0.3b** | B6 | idem L0.3a, **même agent, après fusion de L0.3a et rebasage sur le snapshot** ; `tests/RaqmiSystem.Tests/Accounting/AccountingPeriodCreationTests.cs` (nouveau) | 7,5 j-a | L0.3a | Comptabilité SCF |
| **L0.4a** | A9, D9 | **Énumération nominative — les 14 fichiers d'`Infrastructure` et d'`Api` portant `DateOnly.FromDateTime(DateTime.UtcNow)`** : `Api/Endpoints/CrmEndpoints.cs`, `LodgingOperationsEndpoints.cs`, `PilotageEndpoints.cs`, `RevenueEndpoints.cs` ; `Infrastructure/Closing/DailyClosingService.cs`, `Housekeeping/HousekeepingService.cs`, `Inventory/InventoryService.cs`, `Kpi/KpiAdministrationService.cs`, `Kpi/KpiService.cs`, `Lodging/LodgingService.Extras.cs`, `LodgingService.Inventory.cs`, `LodgingService.Operations.cs`, `LodgingService.Reservations.cs`, `LodgingService.Rooms.cs` ; plus `Desktop/Views/ReceivablesView.xaml.cs` ; `Application/Common/BusinessClock.cs` (nouveau) ; `Infrastructure/DependencyInjection.cs` ; **`tests/RaqmiSystem.Tests/Common/BusinessClockTests.cs`** (nouveau) ; **`tests/RaqmiSystem.Tests/Architecture/**`** (nouveau répertoire, créé ici) | 6 j-a | aucune | Domaine |
| **L0.4b** | A10 | **Énumération nominative — les 16 fichiers de `Desktop/Views` portant `TryParse(… CultureInfo.CurrentCulture)`** : `AccountingView.xaml.cs`, `BudgetView.xaml.cs`, `CrmView.xaml.cs`, `HumanResourcesView.xaml.cs`, `InventoryView.xaml.cs`, `InvoicesView.xaml.cs`, `KitchenView.xaml.cs`, `KpiView.xaml.cs`, `LodgingView.xaml.cs`, `LodgingView.RoomSetup.cs`, `MiceView.xaml.cs`, `MiceView.Allotments.cs`, `PurchasingView.xaml.cs`, `SettingsView.xaml.cs`, `TariffsView.xaml.cs`, `TreasuryView.xaml.cs` ; `src/RaqmiSystem.Desktop/HomeDayWindow.cs` ; `Application/Common/DecimalInput.cs` (nouveau) ; **`tests/RaqmiSystem.Tests/Common/DecimalInputTests.cs`** (nouveau) | 4 j-a | aucune | WPF |
| **L0.5** | Extraits B12 et B14 | `src/RaqmiSystem.Api/Endpoints/AuditEndpoints.cs` ; `Infrastructure/Audit/**` ; `Infrastructure/HumanResources/HumanResourcesService.cs` ; `Application/HumanResources/EmployeeSummaryResponse.cs` ; **`tests/RaqmiSystem.Tests/Audit/**`** (nouveau) | 5,5 j-a | reprise par l'agent de L0.1 après fusion de L0.1 | Sécurité / audit |
| **L0.6** | Lot ports dormant | les 29 fichiers du worktree `agent-a763d009c908f5cde` ; **`tests/RaqmiSystem.Tests/Ports/**`** (tests de caractérisation sur les 7 ports) | 5 j-a | S1 | Architecture |
| **L0.7** | **A6a** | les 33 fichiers du worktree `agent-a7246a035c2ecd403` ; `Persistence/RaqmiDbContext.cs` ; `Persistence/Migrations/**` ; `Api/Program.cs` *(1 ligne)* | 3 j-a | S1 | Sécurité |
| **L0.8** | **D2a** | les 21 fichiers du worktree `agent-a7e76acfc1693b771` ; `Persistence/RaqmiDbContext.cs` ; `Persistence/Migrations/**` ; `Api/Program.cs` *(1 ligne)* | 3 j-a | **L0.7 fusionné** | API |

**Jeton EF : quatre porteurs, sérialisés par le registre (règle 2 corrigée).** Ordre imposé :
**L0.7 → L0.8 → L0.3a → L0.3b.** Chacun génère sa migration, la fait passer au gate, fusionne, rend le
jeton ; le suivant rebase sur le snapshot fusionné avant de générer la sienne. *La version antérieure
faisait de L0.3 le détenteur unique de la vague sans inscrire `Persistence/Migrations/**` ni
`RaqmiDbContext.cs` à son périmètre — la règle 6.5 aurait rejeté sa PR.*
**Détenteur de la coquille WPF : L0.2** (`MainWindow*`, `ModuleCatalog.cs`, `Themes/RaqmiTheme.xaml`).
**Détenteur de `Program.cs` : trois lots successifs**, L0.7 puis L0.8 puis L0.1, jamais simultanément.

### Parallélisme et sérialisation

- **Chaînes séquentielles imposées.** L0.7 → L0.8 (D2a rebase sur A6a et reprend son édition de
  `Program.cs`). L0.3a → L0.3b (même agent, deux migrations). L0.1 → L0.5 (le `REVOKE UPDATE, DELETE`
  sur `audit.audit_logs` s'écrit dans `create-app-role.sql`, détenu par L0.1 ; le confier au même agent
  supprime le partage au lieu de l'arbitrer). L0.1 fusionne **après** L0.7 et L0.8 à cause de
  `Program.cs`.
- **Affectation et durée.**

| Agent | Lots | Charge | Semaines à 3,5 j-a |
|-------|------|--------|--------------------|
| 1 | L0.3a → L0.3b | 15,5 j-a | **4,4 — c'est la vague** |
| 2 | L0.7 → L0.8 → L0.6 → L0.4b | 15 j-a | 4,3 |
| 3 | L0.1 → L0.5 → L0.4a | 17,5 j-a | **5,0 — c'est la vague** |
| 4 | L0.2 | 12 j-a | 3,4 |

  **Durée : 5 semaines, 60 j-a.**
- **Rechiffrage de L0.1 et L0.2, dans l'ordre de difficulté.** L'avis chiffre A11 à « une heure » et D14
  à « dix minutes » ; `Directory.Build.props` fait 10 lignes ; les 4 `GRANT USAGE ON SCHEMA` à ajouter
  sur 15 sont 4 lignes de SQL. En face, L0.2 doit poser un **renouvellement de jeton concurrent** sur
  30 fichiers et 7 022 lignes de `Desktop/Api`, éditer `MainWindow.xaml.cs` (1 340 lignes), et créer de
  zéro le premier projet de tests d'un client WPF qui n'en a jamais eu. La version antérieure chiffrait
  L0.1 à 9,5 et L0.2 à 6,5 — l'ordre était inversé. Le surcoût réel de L0.1 tient au test de couverture
  des schémas et au job PostgreSQL, et il est nommé comme tel : **A11 0,5 ; D14 0,25 ; C1a+C1b 0,75 ;
  A2 (4 GRANT + `SchemaGrantCoverageTests` en `[PostgresFact]` + gardes G4 et G5 dans le job) 3,75 ;
  CODEOWNERS, CONTRIBUTING et l'étape de la règle 8(b) 0,75.**
- **L0.2 traite aussi les 20 modules « Planifié ».** Il détient `ModuleCatalog.cs` : il applique
  l'arbitrage rendu en S1 (grisé sans date avec la mention « non prévu pendant la durée du pilote », ou
  retiré du catalogue pour la durée du pilote), **vérifie les cinq constantes `Expected*` contre le
  contenu réel du catalogue** et remplace le `Debug.Assert` — muet en Release — par un test xUnit.
- **Trois fragments de patch à prévoir.** (1) **L0.4b → L0.2** : les deux `TryParse` de
  `MainWindow.xaml.cs` (vérifié : 2 occurrences ; L0.2 détient le fichier). (2) **L0.2 → L0.1** :
  l'étape CI qui référence le nouveau projet `RaqmiSystem.Desktop.Tests` — le projet est créé par L0.2,
  mais `.github/workflows/dotnet.yml` est détenu par L0.1 ; **c'est ce fragment que la version
  antérieure omettait, et sans lui le garde G3 ne peut pas exister.** (3) **L0.4a → L0.3a/L0.3b** :
  *sans objet* — vérifié, `Infrastructure/Accounting/` ne porte **aucune** occurrence de
  `DateOnly.FromDateTime`, et l'exclusion « hors `Infrastructure/Accounting/` » de la version antérieure
  était vide.
- **A11 avant tout le reste, dès le premier jour.** Tant que `postgres-integration` n'a pas produit un
  run vert sur `reorg/phase-1`, aucun critère d'acceptation de ce document n'est opposable : les
  948 méthodes `[Fact]`/`[Theory]` et les 10 `[PostgresFact]` n'ont jamais tourné sur cet état de code.
- **Ne pas abaisser `AccessTokenMinutes` dans cette vague** (contre-recommandation 1 de l'avis) : tant
  que L0.2 n'est pas vert, cela transformerait une panne horaire en panne au quart d'heure.

### Critères d'acceptation

| Nº | Vérification | Valeur attendue | Valeur d'aujourd'hui |
|----|--------------|-----------------|----------------------|
| 1 | `gh run list --branch reorg/phase-1 --workflow dotnet.yml` | ≥ 1 run, conclusion `success` | 0 run |
| 2 | Dans ce run, étape « PostgreSQL integration tests » | ≥ 10 tests **exécutés**, 0 échec | jamais exécutée |
| 3 | `grep -c "needs: build-core" .github/workflows/dotnet.yml` sur `publish-api` | 0 (remplacé par `needs: [build-core, postgres-integration]`) | 1 (ligne 116) |
| 4a | `grep -cE "<Version>|<VersionPrefix>" Directory.Build.props` | ≥ 1 *(alternation non échappée : sous `-E`, `\|` désigne une barre verticale littérale et la commande renvoyait toujours 0)* | 0 sur tout le dépôt |
| 4b | `curl -s http://<api>/health` | renvoie le tag Git *(C1b, 3 lignes à `Program.cs:113`, détenu par L0.1 pour la vague)* | `{ status = "healthy" }` sans version |
| 5 | `grep -c 'GRANT USAGE ON SCHEMA' deploy/postgres/create-app-role.sql` | 19 | 15 |
| 6 | `pg_dump -U raqmi_app -Fc -f v.dump` puis `pg_restore -l v.dump \| grep -oE 'SCHEMA - [a-z]+' \| sort -u \| wc -l` | 19 | 15 |
| 7 | `pg_restore` sur base vierge, puis `select count(*) from information_schema.tables where table_schema not in ('pg_catalog','information_schema') and table_type='BASE TABLE' and table_name <> '__EFMigrationsHistory'` | 104 *(le filtre `table_type` et l'exclusion de l'historique sont indispensables : sans eux le compte inclut les vues et la table de migrations)* | non restaurable |
| 8 | Démonstration chronométrée : session ouverte à T0, écriture de folio à T0+65 min, T0+2 h et T0+3 h | 3 succès, 0 retour à l'écran de connexion, ≥ 3 appels `/auth/refresh` au journal | session morte à 60 min |
| 9 | `dotnet test tests/RaqmiSystem.Desktop.Tests --filter FullyQualifiedName~Session` | vert, ≥ 3 tests (renouvellement, 401 → connexion, refus si `IsLockedOut`) | projet inexistant |
| 10 | Démonstration : câble réseau débranché pendant un check-in | message de panne en moins de 15 s | 100 s (`new HttpClient()` sans `Timeout`) |
| 11 | `git grep -c 'DateOnly.FromDateTime(DateTime.UtcNow)' -- src/RaqmiSystem.Infrastructure src/RaqmiSystem.Api` | 0 | 16 occurrences dans 14 fichiers, nommés dans L0.4a |
| 12 | `dotnet test --filter FullyQualifiedName~BusinessClock` | vert, dont un cas 31/12 23 h 30 UTC → journée et série de l'année suivante | inexistant |
| 13 | `git grep -cE 'TryParse\(.*CultureInfo\.CurrentCulture' -- src/RaqmiSystem.Desktop` | 0 | 24 occurrences dans 17 fichiers — 16 vues (L0.4b) + `MainWindow.xaml.cs` (fragment vers L0.2) |
| 14 | `dotnet test --filter FullyQualifiedName~DecimalInput` | vert, « 1,500 » donne le même montant en `fr-FR`, `en-US`, `ar-DZ` ; l'ambiguïté est rejetée, pas silencieusement acceptée | inexistant |
| 15 | `dotnet test --filter FullyQualifiedName~AuxiliaryBalance` | `Outstanding == 500m` et `Reconciled == 1000m` pour un tiers à 1 500 facturé / 1 000 encaissé / 1 000 lettré | affiche « rien à recouvrer » |
| 16 | `git grep -c BeginTransactionAsync -- src/RaqmiSystem.Infrastructure/Accounting/AccountingCoreService.cs` | ≥ 1 | 0 |
| 17 | `dotnet test --filter 'Category=Postgres&FullyQualifiedName~Reconcile'` | deux lettrages concurrents : un passe, l'autre remonte 23505 | inexistant |
| 18 | `dotnet test --filter FullyQualifiedName~AccountingPeriodCreation` **et** `git grep -c 'MapPost("/fiscal-years/{id:guid}/periods"' -- src/RaqmiSystem.Api/Endpoints/AccountingEndpoints.cs` | test vert ; ≥ 1 | test inexistant ; 0. *La version antérieure mesurait `MapPost("/periods`, qui renvoie **déjà 1** sur `reorg/phase-1` : il matche `AccountingEndpoints.cs:42`, la **clôture** de période (`/periods/{id:guid}/close`), et non sa création. Le constat qualitatif — `IAccountingCoreService` porte 11 méthodes, aucune création de période — était juste ; la commande censée le mesurer ne le mesurait pas, et le critère était vert au départ.* |
| 19 | `psql -Atc "select conname from pg_constraint where conname like '%journal_entries%number%'"` | renvoie la contrainte `Posted ⇒ number IS NOT NULL` *(la convention `ck_journal_entries_*` est confirmée par les 4 contraintes existantes)* | vide |
| 20 | `POST /audit/purge` | écrit une entrée d'audit ; refuse de descendre sous le plancher de rétention | aucune trace, seule route mutante des 431 dans ce cas |
| 21 | `GET /hr/employees` | écrit une entrée d'audit ; la réponse ne porte plus `ActiveContractGrossSalary` | aucune trace, salaire exposé |
| 22 | `git status --porcelain` des 4 worktreees, après V0 | vide ; les lots ports, A6a et D2a sont fusionnés par PR | 113 fichiers non commités |
| 23 | `pwsh ./tools/check-module-readiness.ps1` avec `ModuleCatalog.cs` modifié | code 0 ; un test xUnit — et non un `Debug.Assert` muet en Release — vérifie les cinq constantes `Expected*` | `Debug.Assert` seul |

### Gardes automatiques à ajouter dans la vague

Un garde par acquis, écrit **dans le même lot que le correctif**. Critère d'acceptation du garde
lui-même : produire un commit qui annule le correctif, constater le job rouge, jeter le commit. *Un
garde qu'on n'a jamais vu échouer n'est pas un garde.*

| Garde | Acquis protégé | Écrit dans | Forme |
|-------|----------------|------------|-------|
| G1 | A11 | L0.1 | `postgres-integration` déclenché sur `reorg/**` ; `publish-api: needs: [build-core, postgres-integration]` |
| G2 | anti-désarmement (D10) | L0.1 | étape qui échoue si `RAQMI_TEST_POSTGRES` est vide ou si le nombre de tests **exécutés** du filtre `Category=Postgres` est < 10 |
| G3 | A1 | L0.2 **+ fragment (2) vers L0.1** | projet `RaqmiSystem.Desktop.Tests` créé par L0.2 **et référencé par une étape de `dotnet.yml`**, détenu par L0.1 |
| G4 | A2 | L0.1 | `[PostgresFact]` comparant les schémas créés par le modèle EF aux `GRANT USAGE ON SCHEMA` du `.sql` |
| G5 | A2 bis | L0.1 | `pg_dump` sous `raqmi_app` puis `pg_restore` sur base vierge dans le job PostgreSQL, assertion 19 schémas / 104 tables |
| G6 | A8 | L0.2 | test refusant tout `new HttpClient()` sans `Timeout` dans `src/RaqmiSystem.Desktop` |
| G7 | A9 | **L0.4a**, qui crée `tests/RaqmiSystem.Tests/Architecture/**` | test d'architecture à compteur zéro sur `DateOnly.FromDateTime(DateTime.UtcNow)` hors abstraction d'horloge |
| G8 | A10 | **L0.4b** | test à compteur zéro sur le motif de double `TryParse` |
| G9 | B7 | L0.3a | test chiffré `Outstanding == 500` + test de concurrence sur le lettrage |
| G10 | B6 | L0.3b | test d'endpoint sur la **création** de période + contrainte base |
| G11 | `documentationGrace` | L0.1 | job hebdomadaire rejouant `check-module-readiness.ps1 -AsOf` à J+180 |
| G12 | B12 / B14 | L0.5 | test échouant si une route mutante n'écrit aucune entrée d'audit |
| G24 | règle 8(b) | L0.1 | étape échouant si une PR ajoute un `*Configuration.cs` sous `Infrastructure/` sans toucher `Persistence/Migrations/` |

**Correction de portée sur G7 et G8.** Ils exigent un répertoire de tests d'architecture. La version
antérieure plaçait leur correctif en V0 (L0.4, dont le périmètre ne contenait aucun fichier de test) et
la création de `tests/RaqmiSystem.Tests/Architecture/**` en V2 (L2.5) : les deux gardes n'existaient
donc pas avant V2, alors que l'objectif de la vague promettait que « toute régression sur ces acquis rend
la CI rouge ». **Le répertoire est créé ici, par L0.4a** ; L2.5 ne fait plus que l'étendre.

---

## 3.1 Vague 1 — Périmètre et exploitation (5 à 6 semaines, 30 à 58 j-a + 7 j-h)

**Objectif.** Faire qu'un utilisateur affecté à une unité ne voie plus les données d'une autre, et
qu'une sauvegarde se restaure réellement.

**Ce qui devient possible à la fin.** Tout nouvel écran peut être livré sans être à reprendre : c'est
la condition posée par l'avis. Et le serveur d'un client peut être mis à jour, puis restauré.

**Cette vague a deux découpes, et on ne sait pas encore laquelle s'applique.** La charge d'A6b varie du
simple au double selon la stratégie d'application, et — c'est le point que la version antérieure de ce
document manquait — **les deux stratégies n'ont pas la même topologie de fichiers**. L'une se
parallélise sur trois agents ; l'autre ne se parallélise pas du tout. La durée de V1 n'est donc pas
publiable avant que la porte L1.0 soit franchie.

### L1.0 — porte de décision, à franchir à la fin de la première semaine de la vague

| Lot | Contenu | Charge | Compétence |
|-----|---------|--------|------------|
| **L1.0** | Prototype jetable sur 3 services représentatifs (Lodging, Billing, HumanResources), non fusionné, **et décision écrite et versionnée** | **3 j-h** (propriétaire/architecte) | Architecte |

**Ne lancer aucun agent sur A6b avant que cet arbitrage soit tranché et écrit.** Ce n'est pas un simple
prérequis : c'est une porte datée, et la durée de la vague est publiée *après* elle, pas avant.

### Découpe A — issue « contrôle explicite service par service » (5 semaines, 58 j-a)

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L1.1** | A6b — PMS | `src/RaqmiSystem.Infrastructure/Lodging/**`, `Housekeeping/**`, `Mice/**`, **à l'exclusion nominative des 31 `*Configuration.cs` de ces trois répertoires** (22 sous `Lodging/`, 4 sous `Housekeeping/`, 5 sous `Mice/`) ; `tests/.../UnitScopeLodgingTests.cs` | 15 j-a | L1.0 | Domaine PMS |
| **L1.2** | A6b — Finance | `src/RaqmiSystem.Infrastructure/Billing/**`, `Treasury/**`, `Accounting/**`, `Purchasing/**`, **à l'exclusion nominative des 14 `*Configuration.cs` de ces quatre répertoires** — `Accounting/AccountingCoreConfigurations.cs`, `AccountingJournalConfiguration.cs`, `ChartAccountConfiguration.cs`, `JournalEntryConfiguration.cs`, `JournalEntryLineConfiguration.cs` ; `Billing/CustomerConfiguration.cs`, `InvoiceConfiguration.cs`, `InvoiceLineConfiguration.cs` ; `Purchasing/PurchaseOrderConfiguration.cs`, `PurchaseOrderLineConfiguration.cs`, `SupplierConfiguration.cs` ; `Treasury/BankAccountConfiguration.cs`, `CashReceiptConfiguration.cs`, `PaymentOrderConfiguration.cs` ; `tests/.../UnitScopeFinanceTests.cs` | 12 j-a | L1.0 | Comptabilité |
| **L1.3** | A6b — Transverse | `src/RaqmiSystem.Infrastructure/Crm/**`, `Inventory/**`, `Kitchen/**`, `HumanResources/**`, `Kpi/**`, **à l'exclusion nominative des 29 `*Configuration.cs` de ces cinq répertoires** (7 sous `Crm/`, 5 sous `Inventory/`, 4 sous `Kitchen/`, 10 sous `HumanResources/`, 3 sous `Kpi/`) ; `tests/.../UnitScopeTransverseTests.cs` | 12 j-a | L1.0 | Domaine |
| **L1.4a** | A3, C9, **D13** | `deploy/onpremise/install-server.ps1`, `start-api.ps1`, `check-health.ps1` ; `deploy/onpremise/update-server.ps1` (nouveau) ; `docs/deployment-onpremise.md` | 9 j-a | A2 (V0) | Exploitation Windows |
| **L1.4b** | A12 | `deploy/onpremise/backup-raqmi.ps1` ; `deploy/onpremise/restore-raqmi.ps1` (nouveau) ; `deploy/backup/**` | 6 j-a | A2 (V0), L1.4a | Exploitation Windows |
| **H1.4** | Démonstrations sur VM et **2 PV** (restauration devant témoin, mise à jour + retour arrière) | — | **4 j-h** | L1.4a, L1.4b | Propriétaire + témoin |
| **L1.5** | D3 | `src/RaqmiSystem.Infrastructure/Billing/InvoiceConfiguration.cs` ; `Accounting/JournalEntryConfiguration.cs` ; `JournalEntryLineConfiguration.cs` ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` | 4 j-a | aucune | Données |

**Détenteur du jeton EF : L1.5.** *Dans cette découpe seulement* : A6b ne change pas le modèle, il pose
un filtrage dans les services existants. C'est ce qui permet de mener trois lots A6b en parallèle.

**Affectation et durée, découpe A**

| Agent | Lots | Charge | Semaines à 3,5 j-a |
|-------|------|--------|--------------------|
| 1 | L1.1 | 15 j-a | **4,3 — c'est la vague** |
| 2 | L1.2 → L1.5 | 16 j-a | **4,6 — c'est la vague** |
| 3 | L1.3 | 12 j-a | 3,4 |
| 4 | L1.4a → L1.4b | 15 j-a | 4,3 |

**Durée : 5 semaines.** *Six lots pour quatre agents : c'est la sérialisation L1.2 → L1.5 qui le rend
possible, et elle est nécessaire puisque L1.5 doit fusionner en deuxième position (règle 3) et que
personne d'autre n'est libre. La version antérieure publiait 70,5 j-a pour 5 semaines, soit 100,7 % de
la capacité annoncée — pas une heure de marge pour un rebasage, une revue ou un gate rouge, alors que
l'abattement de 30 % est justement censé les couvrir.*

### Découpe B — issue « filtre de requête global EF » (6 semaines, 30 à 35 j-a)

Si L1.0 retient le filtre global, il s'écrit dans `RaqmiDbContext.OnModelCreating` ou dans les
`*Configuration.cs` des agrégats porteurs de `HotelUnitCode`. Vérifié : `RaqmiDbContext.cs:273` appelle
`ApplyConfigurationsFromAssembly` — c'est le point d'entrée unique par lequel passerait un tel filtre, et
**aucun des six lots de la découpe A ne déclare `RaqmiDbContext.cs` dans son périmètre.**

| Lot | Points | Périmètre | Charge | Compétence |
|-----|--------|-----------|--------|------------|
| **L1.1′** | A6b **entier** + D3 | `Persistence/RaqmiDbContext.cs` ; **tous** les `*Configuration.cs` des agrégats porteurs de `HotelUnitCode` ; `Persistence/Migrations/**` ; les tests d'isolation des trois familles | 15 à 20 j-a | Architecte + données |
| **L1.4a / L1.4b / H1.4** | inchangés | inchangés | 15 j-a + 4 j-h | Exploitation |

**Dans cette découpe, A6b ne se parallélise pas et L1.5 n'existe pas** : D3 est absorbé par L1.1′, ou
glisse en V2. Durée imposée par le lot dominant : 20 / 3,5 = **5,7 semaines**, soit 6.

**Les deux énoncés de la version antérieure — « la stratégie n'est pas tranchée » et « A6b ne change pas
le modèle, donc trois lots en parallèle » — ne pouvaient pas être vrais en même temps.** Le plan
préjugeait de la réponse à la question qu'il déclarait ouverte, et toute la tenue de la vague en
dépendait.

### Parallélisme et sérialisation (commun aux deux découpes)

- **Aucun lot d'A6b n'ouvre un `*Configuration.cs`.** C'est la règle centrale de la vague, et elle est
  désormais **nominative et non plus implicite** : les jokers de répertoire sont interdits dès qu'un
  `*Configuration.cs` s'y trouve. *La version antérieure déclarait en convention générale que « les
  listes de fichiers d'une même vague ne se recoupent jamais », puis donnait à L1.2 le joker
  `Accounting/**` — qui contient `JournalEntryConfiguration.cs` et `JournalEntryLineConfiguration.cs`,
  deux des trois fichiers déclarés exclusifs à L1.5 (vérifié par `git ls-tree`) — et n'en signalait
  qu'un seul, celui de Billing. Le joker avalait en réalité **14** `*Configuration.cs`, et les jokers de
  L1.1 et L1.3 en avalaient 31 et 29 de plus.*
- **L'automatisme qui rend cette erreur impossible est livré en V0**, pas « un jour » : l'étape de CI de
  la règle 8(b), qui échoue si une PR ajoute un `*Configuration.cs` sous `src/RaqmiSystem.Infrastructure/`
  sans toucher à `Persistence/Migrations/`, est dans le périmètre de L0.1.
- **Attention — dans la découpe A, L1.2 et L1.5 se disputent trois fichiers**, pas un :
  `InvoiceConfiguration.cs`, `JournalEntryConfiguration.cs` et `JournalEntryLineConfiguration.cs`.
  **L1.5 les détient tous les trois.** L1.2 livre en fragment de patch toute clause de périmètre à y
  poser.
- **L1.4a et L1.4b en parallèle de tout, du début à la fin.** `deploy/` n'a aucune intersection avec `src/`.
  Le même agent tient `install-server.ps1` d'un bout à l'autre de la vague, puis en V2 avec L2.3 :
  **six** points écrivent dans ce fichier (A3, A12, C2, C9, C12, D13) et les grouper est la seule sortie
  propre.
- **Rechiffrage de `deploy/`, et séparation des jours-homme.** L'arbre `deploy/` pèse **1 307 lignes**
  sur 10 fichiers (mesuré : `install-server.ps1` 485, `create-app-role.sql` 204, `backup-raqmi.ps1` 193,
  `pg-backup.sh` 136, `check-health.ps1` 111, `RaqmiSystemDesktop.iss` 70, `start-api.ps1` 67,
  `raqmi-backup.service` 20, `raqmi-backup.timer` 14, `Caddyfile` 7). La version antérieure y facturait
  **43,5 j-h** sur trois vagues (L1.4 24,5 + L2.3 10 + L4.4 9) — ce qui, à l'étalon qu'elle se donnait
  elle-même, représenterait dix fois l'existant à réécrire — pendant qu'elle chiffrait A4, maillon
  central du chemin critique, à 6,5. Le poste est ramené à **15 j-a de script (L1.4a + L1.4b) + 4 j-h de
  démonstrations et de procès-verbaux**, qui sont du travail humain non parallélisable et qui figurent
  désormais dans la colonne des jours-homme, pas dans le compte agent.
- **D13 entre ici.** `docs/deployment-onpremise.md` exige aujourd'hui le SDK .NET et un clone du dépôt
  sur le PC du client, et `install-server.ps1:286` exécute `dotnet publish` sur place : le client
  recevrait le code source et l'historique Git, en contradiction avec l'EULA §2 qu'il signe. L1.4a
  publie en amont, livre un binaire, et retire les deux exigences du document. Sans cela, une sixième
  ouverture d'`install-server.ps1` serait garantie après coup.
- **A2 avant A12, non négociable.** Tant que les 4 GRANT manquent, `pg_dump` reste incomplet et la
  restauration de vérification validerait une sauvegarde tronquée.
- **Détenteur de la coquille WPF : aucun** — la vague ne touche pas `MainWindow*`.

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | `dotnet test --filter FullyQualifiedName~UnitScope` | vert ; au moins un test par famille de modules |
| 2 | Test d'isolation, pour chaque module : un utilisateur affecté à l'unité X interroge une liste de l'unité Y | 403 ou liste vide — jamais un filtrage côté client |
| 3 | `git grep -rl "IUnitScopeProvider" -- src/RaqmiSystem.Infrastructure \| wc -l` | ≥ 12 fichiers de service (aujourd'hui : 2, dont l'enregistrement) — *critère de la découpe A ; en découpe B, le critère devient la présence du filtre global et un test d'isolation par famille* |
| 4 | Garde H1 : un nouveau service accédant à `RaqmiDbContext` sans passer par le filtre de périmètre | CI rouge |
| 5 | `ls deploy/onpremise/update-server.ps1 deploy/onpremise/restore-raqmi.ps1` | les deux fichiers existent (aujourd'hui : `deploy/` = 10 fichiers, aucun `update`, aucun `restore`) |
| 6 | Démonstration sur VM : deux mises à jour successives, puis une troisième volontairement cassée | retour automatique à `api-previous\`, API redémarrée après reboot complet |
| 7 | Restauration **devant témoin** sur machine vierge, à partir du seul dump chiffré et de la sauvegarde de `raqmi.env.ps1` | `/health` répond `healthy`, un utilisateur se connecte et retrouve la réservation créée avant la sauvegarde. PV daté et signé, versionné dans `docs/exploitation/` |
| 8 | `git grep -c HasCheckConstraint -- src/RaqmiSystem.Infrastructure/Billing/InvoiceConfiguration.cs` | ≥ 4 (aujourd'hui : 1, contre 5 dans `FolioChargeConfiguration.cs`) |
| 9 | `dotnet test --filter 'Category=Postgres'` après fusion de L1.5 | vert, `HasPendingModelChanges()` compris |
| 10 | `grep -c 'dotnet publish' deploy/onpremise/install-server.ps1` | 0 ; `docs/deployment-onpremise.md` n'exige plus ni SDK ni clone du dépôt (D13) |

### Gardes automatiques

**H1 (A6b)** : test d'isolation par famille d'endpoints, plus un garde d'architecture qui échoue si un
service accède au `DbContext` sans filtre de périmètre. **H6a (D3)** : test comptant les
`HasCheckConstraint` par module et échouant si un module financier descend sous un seuil.
**G13 (A12)** : job hebdomadaire vérifiant qu'une restauration de vérification horodatée a bien eu
lieu dans les sept derniers jours.

---

## 3.2 Vague 2 — Identité fiscale et socle API (5 semaines, 52 j-a)

**Objectif.** Faire qu'une facture porte le NIF de l'établissement qui l'émet et un numéro tiré d'une
série cloisonnée par unité et par exercice.

**Ce qui devient possible à la fin.** A4 peut commencer sans risquer d'émettre des factures au mauvais
NIF et dans la mauvaise série — c'est-à-dire sans avoir à les reprendre.

**Prérequis externe.** La visite de site W8.1 (§3.8) doit avoir eu lieu **avant** cette vague : trois
exclusions du périmètre pilote — unité unique, hôtel vide, paie faite ailleurs — sont des hypothèses sur
un client qui n'est pas encore identifié, et B1 fige le cloisonnement des séries sur la première.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L2.1** | B1 | `src/RaqmiSystem.Domain/Organization/HotelUnit.cs` ; `Domain/Billing/Invoice.cs` ; `Domain/Settings/ApplicationSettings.cs` ; `Infrastructure/Billing/**` ; `Api/Endpoints/SettingsEndpoints.cs` ; `Desktop/Views/SettingsView.xaml(.cs)` ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` ; `tests/RaqmiSystem.Tests/Billing/InvoiceSeriesTests.cs` | 11 j-a | A6b (V1) | Fiscalité + données |
| **L2.2** | C3, C12 | `src/RaqmiSystem.Api/Program.cs` ; `Directory.Packages.props` ; `src/RaqmiSystem.Api/Middleware/**` (nouveau) ; `deploy/Caddyfile` | 10 j-a | aucune | Socle API |
| **L2.3** | C2, C5 | `deploy/onpremise/install-server.ps1`, `check-health.ps1` ; `deploy/backup/raqmi-backup.service` ; `docs/exploitation.md` (nouveau) | 10 j-a | A3, A12, D13 (V1) | Exploitation |
| **L2.4** | D2b | `src/RaqmiSystem.Desktop/Api/**` ; `src/RaqmiSystem.Application/Idempotency/IdempotencyOptions.cs` ; `docs/api-idempotence.md` (nouveau) | 12 j-a | **D2a fusionné (L0.8, V0)** | WPF + HTTP |
| **L2.5** | D1 | `Directory.Build.props` ; `tests/RaqmiSystem.Tests/RaqmiSystem.Tests.csproj` ; `.github/workflows/dotnet.yml` ; `tests/RaqmiSystem.Tests/Architecture/**` *(étendu — créé en V0 par L0.4b)* | 6 j-a | A11 (V0) | CI |
| **L2.6** | C13 (amorce) | `docs/guide/**` (nouveau) ; `docs/documentation-index.md` | 3 j-a | A1 (V0) | Rédaction |

### Parallélisme et sérialisation

- **L2.1 détient le jeton EF** : la bascule de l'index unique `(IssuedYear, IssuedSequence)` vers
  `(HotelUnitCode, IssuedYear, IssuedSequence)` est une migration sur index unique, donc à jouer seule.
  **Aucun autre lot de la vague ne porte de `*Configuration.cs`** — case (j) de la règle 7 vérifiée lot
  par lot.
- **Décision d'ordonnancement à retenir : la migration de L2.1 porte aussi les paramètres de taxe de
  séjour et de droit de timbre par unité.** Ils vivent sur la même entité `HotelUnit` (83 lignes,
  5 propriétés aujourd'hui, aucun champ fiscal). Les poser en même temps que l'identité fiscale évite
  deux migrations ultérieures et retire B4 et B5 de la file d'attente. C'est le modèle de la manœuvre que
  la règle 7(j) impose désormais de rejouer pour L4.2 et L6.2.
- **L2.2 détient `Program.cs` seul** pour toute la vague. Cinq points écrivent dans ce fichier de
  218 lignes (A6, D2, C1b, C3, C12) : **trois sont fusionnés en V0** — A6a par L0.7, D2a par L0.8, et
  C1b (exposition de la version sur `/health`, 3 lignes à `Program.cs:113`) par L0.1 — **les deux
  derniers, C3 et C12, sont ici.** *La version antérieure de ce document annonçait « trois sont déjà
  fusionnés en S1 » alors que S1 n'en fusionnait que deux, et rangeait simultanément C1 dans un lot de
  V0 déclaré « ne touche aucun fichier de `src/` » : ni la vague 0 ni la porte n° 8 du jalon ne pouvaient
  alors être cochées avant V2.*
- **L2.2 et L2.5 se disputent `Directory.Packages.props`** (C3 ajoute un paquet de journalisation, D1
  un paquet de tests d'architecture). L2.2 le détient ; L2.5 livre un fragment de patch.
- **L2.3 et L1.4a sont le même agent**, dans la continuité : `install-server.ps1` ne change pas de main.
  **Six points écrivent dans ce fichier sur l'ensemble du plan** — A3, A12, C2, C9, C12 et **D13** —
  et D13 est désormais dans L1.4a, ce qui évite la sixième ouverture que sa version antérieure
  garantissait.
- **Ne pas activer `TreatWarningsAsErrors` sur Desktop ni Infrastructure dans L2.5** — seulement sur
  Domain et Application, comme le prescrit l'avis. Épingler `LangVersion` au passage : `preview` sur
  toute la solution est inacceptable pour un logiciel comptable à dix ans de conservation.

### Affectation des agents et durée

| Agent | Lots | Charge cumulée | Semaines à 3,5 j-a |
|-------|------|----------------|--------------------|
| 1 | L2.1 → L2.6 | 14 j-a | 4,0 |
| 2 | L2.4 | 12 j-a | 3,4 |
| 3 | L2.2 | 10 j-a | 2,9 |
| 4 | L2.3 → L2.5 | 16 j-a | **4,6 — c'est la vague** |

**Durée : 5 semaines.** *Six lots pour quatre agents : la vague ne tient que par la sérialisation
explicite ci-dessus. La version antérieure publiait 4 semaines pour 52 j-a, soit 92,8 % d'une capacité
de 56 j-a, sans dire quel agent prenait les deux lots surnuméraires.*

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | Deux unités facturent le même jour | deux séries distinctes, sans trou et sans collision |
| 2 | `git grep -n 'NextIssueSequenceAsync' -- src/RaqmiSystem.Infrastructure/Billing/BillingService.cs` | signature portant l'unité (aujourd'hui : `NextIssueSequenceAsync(int year)`, ligne 673) |
| 3 | `psql -Atc "select indexdef from pg_indexes where schemaname='finance' and tablename='invoices' and indexname like 'ux_%'"` | un index **unique** sur `(hotel_unit_code, issued_year, issued_sequence)`. *La version antérieure interrogeait `\d accounting.invoices` avec des colonnes en PascalCase : la table vit dans le schéma `finance` (`InvoiceConfiguration.cs:12` — `ToTable("invoices", "finance", …)`), le schéma `accounting` porte `journal_entries`, et toutes les colonnes sont en snake_case (`hotel_unit_code`, `issued_year`, `issued_sequence`), l'index existant s'appelant `ux_invoices_issued_year_sequence`. Le critère échouait donc toujours et ne pouvait ni valider ni invalider B1.* |
| 4 | Facture émise | porte le NIF de l'unité, normalisé par la règle déjà écrite dans `Customer.NormalizeNif` (15 chiffres) |
| 5 | `git grep -cE 'UseSerilogRequestLogging\|UseExceptionHandler\|IExceptionHandler\|AddProblemDetails\|CorrelationId' -- src` | ≥ 4 (aujourd'hui : 0) |
| 6 | À partir de « ça a planté vers 10 h 15 » | l'appel exact est retrouvé au journal en moins de 5 minutes, démonstration chronométrée |
| 7 | `git grep -cE 'UseHttpsRedirection\|UseHsts\|AddRateLimiter' -- src` | ≥ 2 (aujourd'hui : 0) |
| 8 | 11ᵉ tentative de connexion en une minute sur `/auth/login` | 429 (la route coûte un PBKDF2 à 310 000 itérations : c'est aussi un amplificateur de déni de service) |
| 9 | `curl http://<serveur>/...` en on-premise | redirigé ou refusé |
| 10 | Rejeu du même `Idempotency-Key` depuis le client lourd sur un encaissement | un seul effet ; `IdempotencyOptions.Required` vaut désormais vrai |
| 11 | On arrête le service API | l'alerte sortante part en moins de 15 minutes ; la tâche `check-health` est **enregistrée** et non plus seulement copiée |
| 12 | `docs/exploitation.md` | couvre 10 pannes prévisibles symptôme → diagnostic → action, avec rollback et escalade |
| 13 | `dotnet build -warnaserror src/RaqmiSystem.Domain src/RaqmiSystem.Application` | vert ; `Directory.Build.props` ne porte plus `LangVersion=preview` |
| 14 | `dotnet test --filter FullyQualifiedName~Architecture` | vert ; plancher de couverture chiffré atteint via `coverlet.collector`, aujourd'hui référencé et jamais invoqué |

*Note de syntaxe, valable pour les critères 5 et 7 et pour les portes 5 et 7 du §5.1 : sous `-E`,
l'alternation s'écrit **sans** barre oblique inverse. Les cinq commandes correspondantes de la version
antérieure de ce document renvoyaient 0 quel que soit l'état du code (règle 9).*

### Gardes automatiques

**H2 (D2)** : test de rejeu du même `Idempotency-Key` sur chacune des 14 familles d'endpoints marquées.
**H3 (sécurité)** : test échouant sur tout `RequireAuthorization()` **vide** — il y en a 4 aujourd'hui
(`AccountEndpoints.cs:62`, `SyncEndpoints.cs:46` et `:60`, `/me` dans `Program.cs:181`) ; porter
l'attribut n'est pas être protégé. **H5 (D1)** : NetArchTest sur les dépendances proscrites.
l'attribut n'est pas être protégé. **H5 (D1)** : NetArchTest sur les dépendances proscrites.

---

## 3.3 Vague 3 — Identité du séjour (4 semaines, 37 j-a + 1 j-h)

**Objectif.** Savoir qui dort dans la chambre, et combien ils sont — **et savoir ce qu'on a le droit
d'en conserver.**

**Ce qui devient possible à la fin.** B5 (taxe de séjour par nuitée **et par personne**) devient
calculable, et B2b (registre de police) n'attend plus que la chaîne documentaire.

**Nouveau point de niveau (b) — B15, protection des données des hébergés.** Cette vague constitue un
fichier d'identité complet de tous les clients de l'hôtel : nationalité, type et numéro de pièce, et en
V6 un registre extractible et archivé. Ni ce plan ni `docs/security.md` ne portaient un mot sur le
régime algérien de protection des données à caractère personnel : aucune occurrence de « loi 18-07 »,
« données personnelles », « rétention », « chiffrement au repos » dans l'un ou l'autre. Le niveau (b)
de l'avis se déclare « bloquant pour la conformité légale algérienne » et ne couvrait que le fiscal et
la police. **B15 est ajouté, et il passe avant L3.1a**, puisque c'est la migration de L3.1a qui crée les
colonnes.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L3.0** | **B15 — protection des données** | `docs/security.md` ; `docs/juridique/donnees-personnelles.md` (nouveau) ; `Domain/Identity/PermissionCatalog.cs` *(fragment vers L3.1a)* ; `deploy/onpremise/support-bundle.ps1` *(fragment vers L4.4)* | 3 j-a + **1 j-h de décision** | **avant L3.1a** | Sécurité + juridique |
| **L3.1a** | B2a — occupants du séjour | `src/RaqmiSystem.Domain/Lodging/StayOccupant.cs` (nouveau) ; `Domain/Lodging/Reservation.cs` ; `Infrastructure/Lodging/LodgingService.Reservations.cs` ; `Infrastructure/Lodging/StayOccupantConfiguration.cs` (nouveau) ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` ; `tests/RaqmiSystem.Tests/Lodging/StayOccupantTests.cs` | 10 j-a | A6b (V1), L3.0 | Domaine PMS |
| **L3.1b** | B2a — identité de l'hébergé | `src/RaqmiSystem.Domain/Crm/GuestProfile.cs` ; `Infrastructure/Crm/**` ; `Desktop/Views/CrmView.*` ; référentiel nationalités ; `tests/RaqmiSystem.Tests/Crm/GuestIdentityTests.cs` | 8 j-a | **L3.1a fusionné** (jeton EF rendu) | Domaine PMS |
| **L3.2** | A4 | `src/RaqmiSystem.Infrastructure/Lodging/LodgingService.Folios.cs` ; `Domain/Lodging/Folio.cs` ; `Api/Endpoints/LodgingEndpoints.cs` ; `Infrastructure/Billing/BillingService.cs` ; `Desktop/Views/InvoicesView.*` ; `tests/RaqmiSystem.Tests/Billing/FolioInvoicingTests.cs` | 6,5 j-a | B1 (V2) | Facturation |
| **L3.3** | C13 | `docs/guide/**` ; `tools/generate-guide.ps1` (exécution) ; `docs/documentation-index.md` ; `tools/readiness/screens.json` (preuves `documentation`) | 6 j-a | A1 (V0), L2.6 | Rédaction |
| **L3.4** | C6, C11 | `docs/secours-reception.md` (nouveau) ; `docs/deployment-onpremise.md` | 3,5 j-a | aucune | Exploitation |

### Parallélisme et sérialisation

- **L3.1a détient le jeton EF** (entité occupant), puis le rend ; **L3.1b prend le jeton ensuite** pour
  les champs d'identité sur `GuestProfile`. Deux migrations sérialisées dans la vague, conformément à la
  règle 2 corrigée — l'une n'attend l'autre que le temps d'un rebasage sur le snapshot fusionné.
- **L3.0 avant L3.1a, sans exception.** La position sur la loi 18-07 (déclaration ou autorisation,
  mention d'information au check-in), la **durée de conservation des pièces d'identité et sa purge**, et
  la **permission de lecture dédiée** sur ces champs déterminent la forme des colonnes que L3.1a crée.
  Les poser après reviendrait à refaire la migration.
- **L3.1a et L3.2 se disputent `LodgingService.*`** — un type unique de 376 901 octets réparti sur
  15 partiels, que sept points du plan convoitent. Ils travaillent sur deux partiels **différents et
  nommément déclarés** : `LodgingService.Reservations.cs` pour L3.1a, `LodgingService.Folios.cs` pour
  L3.2. Aucun autre partiel n'est ouvert par cette vague. Si l'un des deux doit déborder, il livre un
  fragment de patch, il n'ouvre pas le fichier de l'autre.
- **A4 : trancher en conception, avant de lancer l'agent.** `CreateInvoiceRequest` exige un
  `CustomerCode` non nul alors que `Folio.BillToCustomerCode` est nullable : un client de passage n'a
  pas de fiche Customer. Deux issues — créer un client « passage » à la volée (retenue ici : 6,5 j-a,
  aucune migration), ou faire accepter à la facture un instantané d'hébergé (10 à 12 j-a et **une
  migration** de plus dans la vague). Le chiffrage suppose la première issue.
- **L3.3 et L3.4 en parallèle de tout** : documentation pure, aucune intersection avec `src/`.
  **Attention : L3.3 écrit dans `tools/readiness/screens.json`** (preuve `documentation`), comme la
  recette R30 de V7. Ces deux chantiers sont strictement sérialisés — L3.3 d'abord — et tous deux
  postérieurs au **gel de l'inventaire des écrans** de S1 (action 5).
- **Champs obligatoires de la fiche de police.** Ils ne sont pas dans le dépôt. Le chantier « textes »
  ouvert en S1 (action 9) doit les avoir fournis **avant le démarrage de V3** : sans eux, L3.1b
  invente une liste, et la reprise coûtera une migration.

### Affectation des agents et durée

| Agent | Lots | Charge cumulée | Semaines à 3,5 j-a |
|-------|------|----------------|--------------------|
| 1 | L3.0 → L3.1a | 13 j-a | **3,7 — c'est la vague** |
| 2 | L3.1b (après L3.1a) | 8 j-a | 2,3 |
| 3 | L3.2 | 6,5 j-a | 1,9 |
| 4 | L3.3 → L3.4 | 9,5 j-a | 2,7 |

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | `git grep -cE 'PassportNumber|Nationality|IdentityDocument' -- src` | ≥ 3 (aujourd'hui : 0) — *alternation non échappée sous `-E`* |
| 2 | Un séjour de 3 personnes créé au check-in | 3 occupants persistés, chacun avec nationalité, type et numéro de pièce ; `Reservation.GuestName` n'est plus le seul porteur d'identité |
| 3 | `git grep -n AttachInvoice -- src` | ≥ 1 appelant hors `Folio.cs:161` et hors `MiceService.cs:704` (aujourd'hui : la déclaration `Folio.cs:161` n'a aucun appelant) |
| 4 | `POST /lodging/.../folios/{id}/invoice` | existe ; la colonne `InvoiceId` du folio cesse d'être NULL après émission |
| 5 | Facture émise depuis un folio | montants HT, TVA et TTC repris de `FolioCharge` (`VatRate`, `AmountExclVat`, `VatAmount` existent déjà) ; NIF et série de l'unité corrects |
| 6 | `ls docs/guide/` | trois guides, un par rôle du pilote (aujourd'hui : `docs/guide` n'existe pas) |
| 7 | `docs/secours-reception.md` | procédure de bascule papier écrite, avec fiche d'arrivée et procédure de ressaisie |
| 8 | `docs/deployment-onpremise.md` | exige onduleur et disques en miroir ; l'arbitrage sur la reprise est **écrit**, pas implicite ; **n'exige plus le SDK .NET ni un clone du dépôt** (D13, L1.4a) |
| 9 | `docs/juridique/donnees-personnelles.md` | existe ; porte une position écrite sur la loi 18-07, une durée de conservation des pièces d'identité et sa purge, une permission de lecture dédiée |
| 10 | `dotnet test --filter FullyQualifiedName~IdentityRetention` | vert : un profil dont la pièce d'identité a dépassé la durée de conservation ne la restitue plus |
| 11 | Lecture d'un occupant par un compte sans la permission dédiée | numéro de pièce absent de la réponse, pas masqué côté client |

### Gardes automatiques

**G14 (B2a)** : test échouant si une réservation est confirmée sans au moins un occupant identifié.
**G15 (A4)** : test échouant si une facture d'hébergement est émise sans `HotelUnitCode` ou sans lien
retour vers son folio. **G23 (B15)** : test échouant si un numéro de pièce d'identité sort d'une route
sans la permission dédiée, ou apparaît dans le paquet de diagnostic C4.

---

## 3.4 Vague 4 — Encaissement et folio (7 semaines, 53 j-a)

**Objectif.** Rattacher l'argent du comptoir à une session de caisse et à une pièce de trésorerie.

**Ce qui devient possible à la fin.** La main courante du jour se recoupe avec la somme des règlements,
et le détournement de recette décrit par le risque R-B laisse une trace.

**Pourquoi sept semaines et non quatre.** A7 et la parade R-B forment une chaîne sérielle de
24 jours-agent chez un seul agent : la session de caisse doit exister et être fusionnée avant que le
rattachement du folio et le plafond d'ajustement puissent s'écrire. 24 j-a à 3,5 j-a par agent et par
semaine font 6,9 semaines, et aucun effectif supplémentaire ne les comprime. La version antérieure
divisait 53 j-a par quatre agents et publiait 4 semaines.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L4.1a** | A7 — session de caisse | `src/RaqmiSystem.Domain/Treasury/**` ; `Infrastructure/Treasury/TreasuryService.cs`, `Infrastructure/Treasury/*Configuration.cs` ; `Api/Endpoints/TreasuryEndpoints.cs` ; `Domain/Identity/PermissionCatalog.cs`, `PermissionRegistry.cs` ; `Desktop/Views/TreasuryView.*` ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` ; `tests/RaqmiSystem.Tests/Treasury/CashSessionTests.cs` | 13 j-a | A6b (V1) | Trésorerie |
| **L4.1b** | Rattachement folio ↔ caisse + parade R-B | `Infrastructure/Lodging/LodgingService.Folios.cs` ; `Api/Endpoints/LodgingEndpoints.cs` *(fragment)* ; `tests/RaqmiSystem.Tests/Treasury/FolioSettlementTests.cs`, `NegativeAdjustmentTests.cs` | 11 j-a | **L4.1a fusionné**, même agent | Trésorerie |
| **L4.2** | B5 | `src/RaqmiSystem.Domain/Lodging/ExtraItem.cs` ; `Infrastructure/Lodging/LodgingService.NightAudit.cs`, `LodgingService.Extras.cs` ; `Infrastructure/Tariffs/**` ; `Api/Endpoints/TouristTaxEndpoints.cs` (nouveau, état déclaratif communal) ; `tests/RaqmiSystem.Tests/Lodging/TouristTaxTests.cs` | 14 j-a | B2a (V3), B1 (V2, paramètres portés par `HotelUnit`), **taux obtenus (S1, action 9)** | Domaine PMS |
| **L4.3** | D1 (suite), gardes H | `tests/RaqmiSystem.Tests/Architecture/**` ; `.github/workflows/dotnet.yml` *(fragment)* | 6 j-a | L2.5 | CI |
| **L4.4** | C4, C7 | `deploy/onpremise/support-bundle.ps1` (nouveau) ; `deploy/installer/RaqmiSystemDesktop.iss` ; `.github/workflows/dotnet.yml` (job installeur) | 9 j-a | C1 (V0), C3 (V2), D13 (L1.4a, V1) | Exploitation |

### Parallélisme et sérialisation

- **L4.1a détient le jeton EF** (entité session de caisse) et **détient `PermissionCatalog.cs` +
  `PermissionRegistry.cs`** (61 Ko, référencés par 73 fichiers). La clé d'annulation d'encaissement
  doit être distincte de la clé de confirmation : aujourd'hui `PermissionRegistry.cs:174-181` donne à
  `LodgingCheckoutExecute`, `LodgingRoomMove` et au folio le même alias `LodgingCheckin`.
- **L4.1a puis L4.1b, même agent, deux PR.** L4.1b ne peut pas démarrer sur une entité non fusionnée, et
  la découper vers un second agent supposerait de partager `LodgingService.Folios.cs` avec L4.2, ce que
  la règle 1 interdit. La chaîne de 24 j-a est le plancher de la vague, et elle est publiée comme telle.
- **L4.1b et L4.2 se disputent `LodgingService.*`**, comme en V3 : partiels nommément séparés
  (`Folios.cs` pour L4.1b ; `NightAudit.cs` et `Extras.cs` pour L4.2). C'est le dernier passage sur ce
  type avant le pilote.
- **L4.2 — conception à trancher avant la vague (règle 7, case j).** Son périmètre contient des entités
  mappées alors qu'il ne détient pas le jeton : `ExtraItem` est configuré par
  `src/RaqmiSystem.Infrastructure/Lodging/ExtraItemConfiguration.cs`, et `Infrastructure/Tariffs/`
  porte trois configurations (`CustomerConventionConfiguration.cs`, `RatePeriodConfiguration.cs`,
  `RatePlanConfiguration.cs`) — vérifié. **Toute propriété ajoutée à `ExtraItem`, `RatePlan` ou
  `RatePeriod` pour porter le tarif par personne rend `HasPendingModelChanges()` rouge et fait entrer
  une deuxième migration dans la vague.** Deux issues, à trancher **avant** de lancer l'agent : *(i)* la
  taxe de séjour se pose sur les entités existantes sans nouvelle colonne — **à démontrer, pas à
  supposer** ; *(ii)* ses colonnes sont **pré-portées par la migration de L4.1a**, exactement la
  manœuvre que L2.1 a déjà réussie pour les paramètres de B4 et B5 sur `HotelUnit`. Défaut retenu :
  *(ii)*, parce qu'elle ne suppose rien.
- **L4.3 et L4.4 se disputent `.github/workflows/dotnet.yml`.** L4.4 le détient (nouveau job
  installeur) ; L4.3 livre son étape de garde en fragment de patch. Cinq points écrivent dans ce
  fichier sur l'ensemble du plan (A11, C1, D1, C7, D10) : ils ont été répartis sur trois vagues, avec
  un détenteur nommé à chaque fois.
- **Le night audit est le code le plus délicat du PMS.** `LodgingService.NightAudit.cs` fait 26 Ko et
  son idempotence repose sur une clé de geste déterministe : toute pose automatique de taxe doit rester
  rejouable. C'est le poste principal de L4.2, pas le calcul du taux.
- **Creux de capacité, à exploiter.** Sur 7 semaines, la capacité est de 98 j-a pour 53 j-a de charge.
  Deux usages, à choisir explicitement : avancer **L5.3 et L5.4** (aucune dépendance sur V4), ou
  absorber le levier de réduction de L4.1b si le propriétaire choisit de reporter la parade R-B — ce
  qui ramènerait la vague à 4 semaines, **au prix d'un risque R-B ouvert le jour de l'ouverture**.

### Affectation des agents et durée

| Agent | Lots | Charge cumulée | Semaines à 3,5 j-a |
|-------|------|----------------|--------------------|
| 1 | L4.1a → L4.1b | 24 j-a | **6,9 — c'est la vague** |
| 2 | L4.2 | 14 j-a | 4,0 |
| 3 | L4.4 | 9 j-a | 2,6 |
| 4 | L4.3 (+ L5.3, L5.4 avancés) | 6 j-a (+9) | 1,7 (4,3) |

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | `git grep -n 'new CashReceipt' -- src` | ≥ 2 sites (aujourd'hui : un seul, `TreasuryService.cs:257`) |
| 2 | Un règlement de folio | crée un `CashReceipt` rattaché à une session de caisse ouverte par caissier et par shift |
| 3 | Main courante du jour vs somme des règlements | égalité au dinar près |
| 4 | Ajustement négatif de folio au-delà du plafond | refusé sans second acteur ; entrée d'audit écrite dans tous les cas |
| 5 | `git grep -c 'PermissionCatalog.LodgingCheckin' -- src/RaqmiSystem.Domain/Identity/PermissionRegistry.cs` | l'annulation d'encaissement ne partage plus cet alias |
| 6 | Night audit rejoué deux fois sur la même journée | une seule taxe de séjour posée par nuitée et par personne |
| 7 | `dotnet test --filter FullyQualifiedName~TouristTax` | vert, dont un cas 3 occupants × 2 nuitées, aux **taux fournis par écrit** et référencés dans le test |
| 8 | État déclaratif communal | produit pour un mois donné, totaux recoupés avec les lignes `ChargeKind.Tax` |
| 9 | `unzip -l support-bundle-*.zip` | 5 catégories (journaux, health, inventaire des sauvegardes, version, migrations appliquées) ; `grep -r <mot-de-passe-applicatif> support-bundle/` ne renvoie rien **et aucun numéro de pièce d'identité d'hébergé n'y figure** (L3.0) |
| 10 | Job CI installeur | produit un artefact signé (SignTool), versionné ; `grep -i sign deploy/installer/RaqmiSystemDesktop.iss` ≥ 1, aujourd'hui 0 |
| 11 | `dotnet ef migrations list` sur la vague | **une seule** migration ajoutée par V4 — celle de L4.1a |

### Gardes automatiques

**G16 (A7)** : test échouant si un `CashReceipt` est créé hors session de caisse ouverte.
**G17 (R-B)** : test échouant si un ajustement négatif de folio dépasse le plafond sans second acteur.
**H4 (B12, suite)** : test échouant si une route mutante n'écrit aucune entrée d'audit — le périmètre
s'élargit ici aux gestes de caisse.
s'élargit ici aux gestes de caisse.

---

## 3.5 Vague 5 — Chaîne documentaire (6 à 8 semaines, 33 à 42 j-a + 2 j-h)

**Objectif.** Produire, archiver et remettre une pièce opposable.

**Ce qui devient possible à la fin.** L'hôtel peut présenter une facture à un contrôle. C'est le cœur
du risque R-E, et c'est le poste le plus lourd du niveau (a).

**Fourchette, et pourquoi elle est publiée.** Trois questions de conception ne sont pas tranchées, et
l'une d'elles — la facture bilingue — fait varier la charge de 30 à 50 %. Publier « 6 semaines fermes »
sur un sous-système qui n'existe pas d'une seule ligne serait une fausse précision. **L5.0 est une porte
de décision datée** : tant qu'elle n'est pas franchie, la durée de V5 reste une fourchette et le jalon
qui en dépend aussi.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L5.0** | **Porte de décision A5** | décision écrite, versionnée dans `docs/reorganisation/` | **2 j-h** | à franchir **avant** le démarrage de V5 | Propriétaire + architecte |
| **L5.1a** | A5 — moteur, archivage, **facture et note de séjour** | `src/RaqmiSystem.Documents/**` (nouveau projet de rendu) ; `RaqmiSystem.sln` ; `Directory.Packages.props` ; `Domain/Documents/**` ; `Api/Endpoints/DocumentsEndpoints.cs` (nouveau) ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` ; `tests/RaqmiSystem.Tests/Documents/**` | **18 j-a** (23 à 27 si bilingue) | A4 (V3), L5.0 franchie | Rendu documentaire |
| **L5.1b** | A5 — trois autres modèles (reçu, bon de commande, bulletin de paie) | `src/RaqmiSystem.Documents/Templates/**` | 9 j-a | L5.1a | Rendu documentaire |
| **L5.2** | D6 (amorce) | `tests/RaqmiSystem.Desktop.Tests/**` ; `src/RaqmiSystem.Desktop/Views/InvoicesView.*` ; `Desktop/ViewModels/**` (nouveau) | 6 j-a | A5 (partiel), L0.2 | WPF |
| **L5.3** | Réduction du garde de readiness | `tools/check-module-readiness.ps1` ; `tests/RaqmiSystem.Tests/Readiness/**` (nouveau) | 5 j-a | aucune | CI + WPF |
| **L5.4** | C8 | `src/RaqmiSystem.Api/Endpoints/SyncEndpoints.cs` ; `src/RaqmiSystem.Desktop/Api/RaqmiApiClient.cs` | 4 j-a | C1 (V0) | HTTP |

**L5.1b est hors périmètre pilote par défaut.** Les trois autres modèles suivent après le pilote — le
bulletin de paie en particulier, puisque la paie est hors périmètre (arbitrage 4). Il figure ici pour
être chiffré, pas pour être livré : **V5 est calculée sans lui**.

### Parallélisme et sérialisation

- **L5.1a détient le jeton EF** (entité de document archivé) **et `Directory.Packages.props`** : c'est
  le seul lot qui ajoute une bibliothèque PDF, absente des 21 paquets d'aujourd'hui (vérifié :
  `Directory.Packages.props` porte 21 `PackageVersion` et 0 occurrence de « pdf »).
- **L5.1a est un lot long et sériel, tenu par un seul agent** : 18 j-a chez un agent à 3,5 j-a par
  semaine, c'est **5,1 semaines**, et c'est ce chiffre qui fixe la durée de la vague — pas la charge
  totale divisée par quatre. Le découpage en agents ne s'applique pas à un sous-système dont les modèles
  de documents partagent les mêmes primitives de mise en page. Les trois autres lots occupent les trois
  autres agents.
- **L5.0 — trois questions à trancher, et une date pour le faire.** *(i)* Le choix du moteur PDF est une
  décision de **licence**, pas seulement technique — le produit est vendu. *(ii)* Le rendu vit-il côté
  serveur (le bon endroit) ou côté WPF (le seul précédent existant, `MainWindow.xaml.cs:833`, un
  `FlowDocument` imprimé par `PrintDialog`) ? *(iii)* La facture doit-elle être bilingue
  français/arabe ? Si oui, compter **+30 à 50 % sur L5.1a** et traiter D11 ici plutôt qu'après.
  **Ces trois questions sont des décisions, pas des estimations : elles se tranchent, et V5 n'a pas de
  durée avant qu'elles le soient.** Le modèle de facture doit en outre être relu par un comptable ou un
  juriste (chantier « textes », S1 action 9) **avant** que L5.1a ne fige le gabarit.
- **Levier de réduction de périmètre, si une date doit être tenue :** c'est la découpe L5.1a / L5.1b
  elle-même — deux modèles au lieu de cinq. **Hypothèse** d'économie : 6 à 9 j-a, soit environ deux
  semaines sur le chemin critique. *Ce n'est pas une « économie mesurée » : elle porte sur le retrait de
  trois gabarits qui n'ont jamais été écrits, dans un sous-système qui n'existe pas encore. Rien n'y est
  mesurable, et la version antérieure de ce document le présentait à tort comme une mesure.*
- **L5.3 doit précéder toute extraction de `MainWindow`** (contre-recommandation 7 de l'avis).
  `tools/check-module-readiness.ps1` fait 623 lignes d'expressions régulières sur du C# : il lit
  `MainWindow.xaml` (l. 17), `MainWindow.xaml.cs` (l. 18) et tous les partiels `MainWindow*.cs`
  (l. 33), et compte les `new ModuleCatalogEntry(` (l. 57, 78, 232). Tout renommage rend la CI rouge.
  Réduire le garde au rendu du tableau **avant** d'ouvrir D6, sinon la refonte WPF sera bloquée par son
  propre garde.
- **L5.2 est délibérément minuscule.** Contre-recommandation 3 : des ViewModels **uniquement** sur les
  écrans que la vague touche de toute façon. Aucune refonte MVVM générale.
- **L5.3 et L5.4 n'ont aucune dépendance sur V4** et peuvent être avancés dans le creux de capacité de
  V4 (§3.4), ce qui allège V5 d'autant.

### Affectation des agents et durée

| Agent | Lots | Charge cumulée | Semaines à 3,5 j-a |
|-------|------|----------------|--------------------|
| 1 | L5.1a | 18 j-a (23-27 si bilingue) | **5,1 — c'est la vague** (6,6 à 7,7 si bilingue) |
| 2 | L5.2 | 6 j-a | 1,7 |
| 3 | L5.3 | 5 j-a | 1,4 |
| 4 | L5.4 | 4 j-a | 1,1 |

**Durée publiée : 6 semaines si la facture est monolingue, 8 si elle est bilingue.** Charge :
33 j-a (monolingue) à 42 j-a (bilingue), hors L5.1b.

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | `grep -ci pdf Directory.Packages.props` | ≥ 1 (aujourd'hui : 0 sur 21 paquets) |
| 2 | Facture émise depuis un folio | produit un PDF archivé portant son numéro légal et le NIF de l'unité émettrice |
| 3 | Redémarrage du serveur, puis recherche par numéro de facture | le PDF est retrouvé et réimprimé à l'identique |
| 4 | `dotnet test --filter FullyQualifiedName~DocumentRendering` | vert ; le test relit le numéro dans le fichier produit et vérifie qu'il n'est pas vide |
| 5 | `pwsh ./tools/check-module-readiness.ps1` après L5.3 | code 0, et le garde ne lit plus `MainWindow*.cs` par expression régulière ; les preuves sont portées par des tests xUnit compilés |
| 6 | Un poste en version N-2 se connecte à un serveur en version N | refus ou avertissement fort, sur la base d'une version réelle issue du tag Git |
| 7 | `dotnet test tests/RaqmiSystem.Desktop.Tests` | vert, ≥ 6 tests |
| 8 | La décision L5.0 | existe, est datée, est versionnée, et nomme le moteur, l'emplacement du rendu et la réponse à la question bilingue |

### Gardes automatiques

**G18 (A5)** : test d'intégration produisant un document et vérifiant qu'il est archivé, non vide, et
relisible par son numéro. **G19 (C8)** : test de compatibilité client ↔ serveur sur trois couples de
versions. **H7 (D8, amorce)** : test échouant si une route de liste accepte une requête sans borne de
pagination — `GET /accounting/entries` en tête, seul `AuditEndpoints.cs` portant un `pageSize`
aujourd'hui.

---

## 3.6 Vague 6 — Conformité de la facture (8 semaines, 45 j-a)

**Objectif.** Rendre la correction d'une facture émise possible **et** traçable : par un avoir, plus
par une annulation.

**Ce qui devient possible à la fin.** La réduction rétroactive d'une TVA déjà déclarée devient
impossible. C'est la dernière porte ouverte du risque R-E.

**Pourquoi huit semaines et non quatre.** B3 et B4 écrivent tous deux dans `BillingService.cs`, le
fichier le plus disputé de la facturation, et ne peuvent donc pas être tenus par deux agents en
parallèle. Leur chaîne — 29 jours-agent en série chez un seul agent — impose 8,3 semaines à 3,5 j-a par
agent et par semaine. La version antérieure divisait la charge de la vague par quatre agents et
publiait 4 semaines, alors que le lot dominant en imposait plus du double.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L6.1a** | B3 — avoir | `src/RaqmiSystem.Domain/Billing/**` ; `Infrastructure/Billing/BillingService.cs`, `InvoiceConfiguration.cs`, `CreditNoteConfiguration.cs` (nouveau) ; `Api/Endpoints/BillingEndpoints.cs` ; `Infrastructure/Reporting/ReportingService.cs` ; `Desktop/Views/InvoicesView.*` ; `Persistence/Migrations/**` ; `Persistence/RaqmiDbContext.cs` ; `tests/RaqmiSystem.Tests/Billing/CreditNoteTests.cs` | 16 j-a | A4 (V3), B1 (V2) | Fiscalité |
| **L6.1b** | B4 — droit de timbre | `src/RaqmiSystem.Domain/Billing/StampDuty.cs` (nouveau) ; `Infrastructure/Billing/BillingService.cs` *(repris après L6.1a, même agent)* ; `Desktop/Views/InvoicesView.*` ; `tests/RaqmiSystem.Tests/Billing/StampDutyTests.cs` | 13 j-a | **L6.1a fusionné** ; barème obtenu (S1, action 9) | Fiscalité |
| **L6.2** | B2b | `src/RaqmiSystem.Api/Endpoints/PoliceRegisterEndpoints.cs` (nouveau) ; `Infrastructure/Crm/PoliceRegisterService.cs` (nouveau) ; `Desktop/Views/CrmView.*` ; modèle de document du registre dans `RaqmiSystem.Documents` ; `tests/RaqmiSystem.Tests/Crm/PoliceRegisterTests.cs` | 11 j-a | B2a (V3), A5 (V5), position 18-07 écrite (L3.0) | Domaine PMS |
| **L6.3** | Gardes structurels | `tests/RaqmiSystem.Tests/Architecture/**` ; `tests/RaqmiSystem.Tests/Postgres/**` | 5 j-a | V0 à V5 | CI |

### Parallélisme et sérialisation

- **L6.1a détient le jeton EF** (série d'avoirs, nature de document). **L6.1b n'ajoute rien au modèle** :
  ses paramètres par unité ont été portés par la migration de L2.1, et son barème est un objet de
  domaine sans persistance. *Case (j) de la règle 7 : cochée « non », et le périmètre de L6.1b ne
  contient aucun `*Configuration.cs` — c'est ce qui rend la découpe possible.*
- **L6.1a puis L6.1b, même agent, deux PR, deux fusions.** Les séparer en deux agents garantirait deux
  ouvertures concurrentes de `BillingService.cs`, ce que la règle 1 interdit. Les séparer en deux
  **lots séquentiels du même agent** ne coûte rien et rend la découpe lisible.
- **Ce qui ne se sépare pas, c'est B3 lui-même.** Restreindre `Invoice.Cancel` au seul statut `Draft`
  sans fournir l'avoir revient à retirer une échappatoire sans donner la pièce corrective : les deux
  moitiés de B3 sont dans L6.1a, et L6.1a ne se livre pas à moitié.
- **Levier de réduction de calendrier, à arbitrer par le propriétaire.** Livrer L6.1a seul en V6 ramène
  la vague à **5 semaines** et reporte B4 après le pilote. Mais le droit de timbre est dû sur les
  règlements en espèces, et l'avis le range au niveau (b) — bloquant pour la conformité légale.
  **Le reporter est un refus à écrire au contrat pilote, pas une optimisation de planning.** Défaut
  retenu : les deux en V6, 8 semaines.
- **B4 est plus lourd qu'il n'y paraît.** Le droit de timbre algérien est un **barème par tranches**
  avec plancher et plafond, pas un taux : c'est un objet de domaine avec ses invariants et ses tests.
  Il modifie le total de la facture tout en restant hors base de TVA, donc il touche l'invariant
  `total_incl_vat = total_excl_vat + total_vat` durci par D3 en V1 — les deux doivent se recouper.
  **Le barème lui-même n'est pas dans le dépôt** : il est obtenu par le chantier « textes » ouvert en S1,
  **avant** le démarrage de L6.1b. Sans le texte, L6.1b ne démarre pas.
- **Point de vigilance sur les données, à arbitrer par le propriétaire :** la migration de L6.1a doit
  statuer sur les factures déjà passées de `Issued` à `Cancelled` dans les bases existantes. C'est une
  question de données, pas de code.
- **L6.2 — conception à trancher avant la vague (règle 7, case j).** Le garde G22 (« un séjour clos
  produit sa ligne de registre ») suppose une ligne de registre. **Défaut retenu : le registre est
  *dérivé* à l'extraction des données de séjour et d'occupant livrées par L3.1, sans nouvelle entité et
  sans nouvelle colonne** — à démontrer avant de lancer l'agent, pas à supposer. Si la démonstration
  échoue, ses colonnes sont pré-portées par la migration de L6.1a, qui détient le jeton.
- **L6.2 dépend de deux vagues antérieures** (B2a pour la donnée, A5 pour la remise imprimée). C'est le
  seul lot du plan dont les deux dépendances sont dans des branches différentes du graphe.

### Affectation des agents et durée

| Agent | Lots | Charge cumulée | Semaines à 3,5 j-a |
|-------|------|----------------|--------------------|
| 1 | L6.1a → L6.1b | 29 j-a | **8,3 — c'est la vague** |
| 2 | L6.2 | 11 j-a | 3,1 |
| 3 | L6.3 | 5 j-a | 1,4 |
| 4 | *libre* — absorbe les réserves de V5 et prépare L7.2 | — | — |

### Critères d'acceptation

| Nº | Vérification | Valeur attendue |
|----|--------------|-----------------|
| 1 | `POST /invoices/{id}/cancel` sur une facture au statut `Issued` | 409 (aujourd'hui : accepté — `Invoice.Cancel` autorise `Draft or Issued`) |
| 2 | Création d'un avoir partiel | numéro tiré d'une **série distincte**, cloisonnée par unité et par exercice |
| 3 | `ReportingService.BuildInvoicedVatAsync` | la TVA collectée tient compte des avoirs ; un avoir ne peut plus faire disparaître une TVA déclarée |
| 4 | `git grep -ci timbre -- src` | ≥ 1 (aujourd'hui : 0 — le terme n'existe que dans `docs/legacy/`) |
| 5 | `dotnet test --filter FullyQualifiedName~StampDuty` | vert, dont un cas au plancher, un au plafond et un dans chaque tranche du barème **fourni par écrit**, référencé dans le test |
| 6 | Règlement en espèces | ligne de droit de timbre posée, hors base de TVA, total recalculé et cohérent avec la contrainte D3 |
| 7 | Registre de police | extractible sur une période, une ligne par occupant, imprimable et archivé via la chaîne A5 |
| 8 | `dotnet test --filter 'Category=Postgres'` | ≥ 10 tests exécutés, 0 échec |
| 9 | `psql -Atc "select count(*) from pg_class c join pg_namespace n on n.oid=c.relnamespace where n.nspname='finance' and c.relname like '%credit%'"` | ≥ 1 — la série d'avoirs est bien persistée dans le schéma `finance`, pas supposée |

### Gardes automatiques

**G20 (B3)** : test échouant si une facture émise peut être annulée. **G21 (B4)** : test de barème sur
les bornes de chaque tranche. **G22 (B2b)** : test échouant si un séjour clos ne produit pas sa ligne
de registre.

---

## 3.7 Vague 7 — Recette et build candidate (6 semaines, 28 j-a + 18 j-h)

**Objectif.** Produire une build candidate qui passe toutes les portes du §5.1.

**Ce qui devient possible à la fin.** L'installation chez le client peut être planifiée — pas encore
faite : c'est V8.

**Capacité, exception assumée.** Pendant V7 le propriétaire **cesse d'intégrer** et consacre ses 3,5 j-h
hebdomadaires à la recette. Seuls deux lots agents tournent (L7.2, L7.4), et leur intégration est
volontairement lente.

### Lots

| Lot | Points | Périmètre de fichiers exclusif | Charge | Dépendances | Compétence |
|-----|--------|--------------------------------|--------|-------------|------------|
| **L7.1** | Recette R30, **deux passes** | `tools/readiness/screens.json` ; `docs/stabilization/module-readiness.md` | **12 j-h** (passe 1 : 7 ; passe 2 après reprise : 5) | A1 (V0), toutes les vagues, inventaire figé en S1 | Recette (humain) |
| **L7.2** | Job `pilot-gate` | `.github/workflows/pilot-gate.yml` (nouveau) | 3 j-a | V0 à V6 | CI |
| **L7.3** | Démonstration intégrée + 3 PV | `docs/exploitation/**` | 6 j-h | toutes | Propriétaire + témoin |
| **L7.4** | Reprise des réserves de recette | selon les constats de recette | **20 à 30 j-a** | L7.1 passe 1 | tous |

**Aucune migration EF dans cette vague** : le jeton est délibérément libre pour absorber une correction
de dernière minute sans décaler le jalon.

### Parallélisme et sérialisation

- **L7.1 est strictement séquentiel et humain.** 30 écrans × 2 profils = 60 ouvertures, chacune
  assortie de 9 contrôles (ouverture depuis l'accueil, depuis la barre latérale, fil d'Ariane cohérent
  quel que soit le chemin, chargement sans exception, absence d'écran vide, F5, navigation clavier,
  déconnexion/reconnexion avec changement de profil, aucun droit conservé de l'ancien jeton) —
  **540 contrôles par passe**. La version antérieure les budgétait à 4 j-h, soit 3,5 minutes par
  contrôle, ressaisie, changement de profil et journalisation des anomalies comprises, et une seule
  passe alors que le critère 3 impose de rejouer après chaque correction. Le chiffre retenu ici est
  **6 minutes par contrôle et deux passes**. **Cette recette est physiquement impossible avant A1** :
  une session qui meurt à 60 minutes ne permet pas d'aller au bout d'une passe.
- **Le nombre d'écrans n'est pas acquis.** `screens.json` en déclare 30 sur `reorg/phase-1`, mais la
  branche accueil/POS supprime `PmsTabItem`/`PmsView` et ajoute `PosTabItem` sans toucher `tools/`.
  **L'inventaire est figé en S1 (action 5) et L7.1 n'est chiffré qu'après ce gel.**
- **L7.4 n'est pas une réserve ronde, c'est une extrapolation.** Le seul point de données du dépôt est
  `9bfc4c3` — « corriger les 11 trouvailles de la revue adverse de l'accueil » : **15 fichiers,
  699 insertions, 197 suppressions, pour un seul écran, déjà refondu et déjà relu**. Les 30 écrans de
  `screens.json` portent tous `"smoke": null` : **aucun n'a jamais été ouvert de bout en bout par un
  humain**. En supposant que la moitié seulement remonte quelque chose et un quart du volume par écran,
  on obtient 20 à 30 jours-agent. C'est une estimation basse, et elle est publiée comme telle.
- **La phrase « si la recette ne remonte rien, la vague se termine en deux semaines » est retirée.**
  Ce n'était pas une hypothèse basse, c'était une promesse — et précisément la promesse que l'avis
  reproche au projet de faire : une recette qui ne trouve rien.
- **L7.1 et L3.3 écrivent dans le même `tools/readiness/screens.json`.** L3.3 (preuves
  `documentation`) est en V3, L7.1 (preuves `smoke`) est ici : les deux chantiers sont séparés par
  quatre vagues, ce qui suffit. Le lot de documentation de S1 est le troisième écrivain du fichier, et
  il passe en premier.

### Critères d'acceptation

| Nº | Vérification | Valeur attendue | Valeur d'aujourd'hui |
|----|--------------|-----------------|----------------------|
| 1 | `grep -c '"smoke": null' tools/readiness/screens.json` | 0 | 30 **avant** le gel de l'inventaire de S1 |
| 2 | Profil `system.administrator` | tous les écrans de l'inventaire figé ouvrables | à valider |
| 3 | Profil restreint | chaque écran non autorisé est visible et cadenassé sur l'accueil, absent de la barre latérale, impossible à ouvrir par `Ctrl+Tab` / `Ctrl+Shift+Tab`, sans aucun repli transitoire observable | à valider |
| 4 | `pwsh ./tools/check-module-readiness.ps1 -AsOf 2027-01-01` | code 0 | échoue (21 écrans rétrogradés) — levé dès S1 |
| 5 | `pwsh ./tools/check-module-readiness.ps1 -MarkdownSummaryPath readiness-summary.md` | code 0 ; la colonne Smoke ne porte plus « à valider » | — |
| 6 | Job `pilot-gate` | vert (voir la liste de contrôle du §5.1, **dont chaque commande a été jouée une fois avant d'être opposée**) | inexistant |
| 7 | Démonstration intégrée, en une séance et sur un seul jeu de données | déroulée sans reprise manuelle (scénario au §5.1) | — |
| 8 | PV de restauration, PV de mise à jour et retour arrière, PV de bascule papier | trois documents datés, signés, versionnés dans `docs/exploitation/` | aucun |
| 9 | Deuxième passe de recette, après reprise des réserves | 0 constat bloquant restant, et le journal des deux passes est versionné | — |

**Rappel à porter au procès-verbal.** Cette recette lève le gel de stabilisation — **si et seulement si
la décision écrite de S1 (action 6) l'a amendé pendant V2 à V6** ; elle ne rend **aucun** écran
Production Ready. Ce niveau exige en plus `productionReady.postgresqlCi` et `productionReady.e2e`,
aujourd'hui `null` pour les 30 écrans. L'arbitrage sur ce point est dû à la fin de V4 (§5.1).

---

## 3.8 Vague 8 — Mise en service chez le pilote (6 à 7 semaines, 31 j-h + 10 j-a réservés)

**Objectif.** Faire qu'un hôtel s'en serve. C'est une vague de terrain, pas de dépôt.

**Pourquoi elle existe.** La version antérieure de ce document appelait « premier client pilote » un
jalon dont les quatre lots avaient pour périmètre des fichiers du dépôt. Il n'y avait aucun client
identifié, aucune visite de site, aucun contrat, aucune installation, aucun paramétrage initial, aucune
formation, aucun accompagnement, aucun modèle de support et aucun scénario d'échec. Le plan livrait un
logiciel qui passe ses portes automatiques ; il ne livrait pas un hôtel qui s'en sert.

**Capacité.** Pendant V8, **aucun agent ne tourne** : le propriétaire est chez le client, à plein temps,
5 j-h par semaine sans abattement d'intégration. Les 10 jours-agent réservés servent aux correctifs
remontés par l'exploitation, et sortent des totaux de développement du §2.2.

### Lots — et leurs dates, qui ne sont pas toutes en V8

| Lot | Contenu | Charge | **Quand** | Compétence |
|-----|---------|--------|-----------|------------|
| **W8.1** | Identification du pilote et **visite de site** : nombre de chambres, volumétrie, unité unique ou non, hôtel vide ou en exploitation, qui fait la paie, qui fait la compta | 3 j-h | **avant V2** | Propriétaire |
| **W8.2** | **Contrat et EULA réécrite** (D12), avec les exclusions du §5.2 en annexe, la clause de double saisie et les critères d'arrêt | 5 j-h + délai externe | **signé avant V5** | Propriétaire + conseil juridique |
| **W8.3** | Installation sur site + **paramétrage initial** : chambres, catégories, tarifs, plan comptable SCF, exercice, journaux, utilisateurs et rôles, unité et NIF | 8 j-h | V8 | Propriétaire |
| **W8.4** | **Formation par rôle** (réception, comptabilité, direction), avec **attestation écrite signée** par chaque personne formée | 5 j-h | V8 | Propriétaire |
| **W8.5** | **Mois de double saisie** et accompagnement des premiers jours | 15 j-h sur 4 semaines | V8 | Propriétaire |
| **W8.6** | **Modèle de support** : horaires, canal, délai de réponse, qui répond, escalade ; création de la branche `release/x.y` et de la procédure de correctif (§6) | 3 j-h | V8 | Propriétaire |

**W8.1 avant V2, ce n'est pas une commodité.** Trois exclusions du §5.2 — l'unité unique, l'hôtel vide,
le refus de la paie — sont des **hypothèses sur ce client**. Si l'une tombe, le périmètre change : un
hôtel déjà ouvert rend C10 (import et soldes d'ouverture) bloquant, et le plan le range après le pilote.
**W8.2 avant V5**, parce que ses exclusions décident du périmètre de la chaîne documentaire.

**Le paramétrage initial ne s'improvise pas.** Vérifié sur `reorg/phase-1` : aucune route
`import|bulk|batch` dans les 33 fichiers d'endpoints, aucun `HasData` dans `ChartAccountConfiguration.cs`
— le plan comptable SCF n'est pas pré-alimenté — et `tools/demo-seed/seed-demo.ps1` porte en tête « A NE
PAS LANCER SUR UNE BASE DE PRODUCTION ». Tout se saisit à la main, écran par écran, et c'est chiffré ici
sur le volume réel du pilote relevé par W8.1.

### Critères d'arrêt — écrits avant l'ouverture, pas après

| Nº | Constat | Décision |
|----|---------|----------|
| 1 | Une facture émise se révèle non conforme (numéro, NIF, série, TVA, timbre) | **arrêt immédiat de la facturation depuis le produit**, bascule sur la procédure papier de C6, correction, PV de reprise |
| 2 | Deux jours consécutifs sans que la main courante se recoupe au dinar près | arrêt des encaissements depuis le produit, retour caisse manuelle, analyse |
| 3 | Une restauration de vérification hebdomadaire échoue | arrêt des livraisons, priorité absolue à R-A, le client reste en double saisie |
| 4 | Le personnel formé ne parvient pas à tenir une journée sans assistance à la fin de W8.5 | prolongation de la double saisie, et **la sortie du pilote est reportée**, pas déclarée |
| 5 | Trois de ces constats sur le même mois | **repli complet sur le système antérieur**, et arbitrage écrit sur la suite |

**La procédure de repli papier de C6 (L3.4, V3) est la sortie de crise de tous ces critères.** C'est la
raison pour laquelle elle est livrée en V3 et non près du pilote.

---

## 4. S1 — Récupération et décisions (3 semaines, propriétaire seul)

**C'est la toute première étape du plan, et elle passe avant l'écriture de la moindre ligne de code.**

**Pourquoi trois semaines et non une.** La version antérieure de ce document plaçait 12,5 jours-homme
sur une semaine de cinq jours ouvrés, « intégrateur seul, aucun agent en vol », et l'écrivait noir sur
blanc sans voir la contradiction : au rendement qu'elle retenait elle-même (3,5 j-h par personne et par
semaine), cette semaine en valait 3,6. Tout le calendrier partait de là et était donc en retard dès sa
première ligne. La correction est double : **S1 est ramenée à ce qui est irréversible ou décisionnel**
— sauvetage git, réconciliation des lignées de migration, décisions écrites — et **les trois actions de
développement qu'elle contenait (ports, A6a, D2a) deviennent des lots nommés de V0**, confiés à un
agent, parce que ce sont du développement et non de l'intégration.

### 4.1 Ce qui est en jeu

Quatre lots totalisant environ **7 600 lignes de travail réel** dorment dans `.claude/worktrees/agent-*`.
Ce ne sont **pas des commits** : ce sont des répertoires de travail sales.
`git rev-list --count cbd5e6a..worktree-agent-a7246a035c2ecd403` renvoie **0** pour les quatre branches
— aucun `git push` ne peut les sauver, il faut d'abord `git add` puis `git commit`.

Aggravant : `.claude/worktrees/` est exclu par `.git/info/exclude`, un fichier d'exclusion **local**,
lui-même non versionné et partagé avec personne. Ces lots sont invisibles à `git status` du dépôt
principal et à toute sauvegarde fondée sur git. Ils n'ont pas été touchés depuis le 02/09 à 14 h 35.

**Le document fondateur est dans le même état.** `docs/reorganisation/08-avis-etat-du-projet.md`
(63 130 octets, 545 lignes) n'existe sur **aucune branche** :
`git ls-tree -r --name-only reorg/phase-1 -- docs/reorganisation/` ne remonte que les fiches 01 à 07
et le README. L'avis d'architecte, référentiel de tout ce plan, existe en un unique exemplaire sur un
disque. **Le commiter est le premier geste**, avant même les worktrees.

**Et il y a un cinquième lot, que ce plan ignorait : une branche poussée, en avance sur `reorg/phase-1`.**
`origin/feature/personalized-homepage` porte **9 commits d'avance** sur `reorg/phase-1` (vérifié :
`git rev-list --count reorg/phase-1..origin/feature/personalized-homepage` = 9) et un diff de
**26 fichiers, +20 883 / −136**. Elle contient un **module POS complet** (`Domain/Pos/PosEntities.cs`,
`Infrastructure/Pos/PosService.cs`, `PosConfigurations.cs`, `Api/Endpoints/PosEndpoints.cs`,
`Desktop/Views/PosView.*`, `Desktop/Api/RaqmiApiClient.Pos.cs`, `tests/RaqmiSystem.Tests/PosDomainTests.cs`),
une refonte de l'accueil (`MainWindow.xaml` +378, `MainWindow.xaml.cs` +70, `Themes/RaqmiTheme.xaml`
+403, `ModuleCatalog.cs`, `SidebarLayout.cs`, `ModuleTile.cs`), 8 clés dans `PermissionCatalog.cs`, et
surtout **deux migrations EF commitées**.

**Deux lignées de migration ont déjà bifurqué.** Depuis le tronc commun `20260901130213_WaveKpi` :
`20260901154839_AccountingScfCore` puis `20260901163333_AccountingAuxiliaryLedger` d'un côté ;
`20260903235736_WavePointOfSale` puis `20260904000822_PosOutletWarehouses` de l'autre. **Deux snapshots
de 401 Ko ont été réécrits séparément** — exactement la situation que l'arbitrage 2 est censé rendre
impossible, et qui rendait plusieurs affirmations de ce plan fausses : l'indicateur « migrations en vol
simultanément = 2 » (en réalité 4, dont 2 déjà commitées) ; « `Program.cs` : c'est la seule collision
réelle » (il y a un troisième écrivain) ; « le POS restaurant est exclu du périmètre » et « aucun lot du
plan n'ouvre un module Planifié » (le POS est déjà écrit) ; et la règle 1, qui attribue un détenteur
unique par vague à six fichiers-carrefours que cette branche touche tous.

**Correction au cadrage.** `reorg/phase-1` **est** poussée sur `origin` : `git branch -r` liste bien
`origin/reorg/phase-1`, et le SHA distant `da196a75` est identique au local. Ce qui n'est poussé nulle
part, ce sont les **dix branches locales `worktree-agent-*` / `worktree-wf_*`** — c'est exactement ce
que vise le point D14. Le risque de perte porte sur les quatre lots non commités, pas sur la branche.

### 4.2 Ordre d'exécution

| Nº | Action | Charge | Risque si omis |
|----|--------|--------|----------------|
| 0 | `git add docs/reorganisation/ && git commit` sur `reorg/phase-1`, puis pousser | 0,1 j-h | perte de l'avis et de ce plan |
| 1 | **Arbitrage écrit sur `origin/feature/personalized-homepage` et sur le POS** : fusion, rebasage ou abandon des 9 commits. Si fusion : régénérer **un seul** snapshot et rejouer les deux `Up()` contre un modèle qu'aucune des deux branches ne connaissait, `HasPendingModelChanges()` vert, gate PostgreSQL vert. Statuer sur le POS : au périmètre (et §5.2 réécrit) ou mis de côté (et on dit **où** son code vit) | **4 j-h** | deux lignées de migration divergentes ; six fichiers-carrefours détenus par personne ; un module « exclu du pilote » déjà écrit et poussé |
| 2 | Dans chacun des 4 worktrees : `git add -A && git commit` en Conventional Commits, puis `git push -u origin` les 10 branches `worktree-*` | 0,1 j-h | perte de ~7 600 lignes, dont deux décisions de conception coûteuses à retrouver |
| 3 | Rebaser les 4 branches sur la tête issue de l'action 1 | 0,5 j-h | conflits croissants à chaque semaine de dormance |
| 4 | **Fusionner le lot documentation** `agent-a3b07db6dff7d6ed5` — 30 fichiers, aucun fichier de `src/`, aucune migration | 1,5 j-h | l'échéance du 31/12/2026 reste armée |
| 5 | **Geler l'inventaire des écrans** : régénérer `tools/readiness/screens.json` depuis le `MainWindow.xaml` issu de l'action 1, et **recompter** avant de chiffrer la recette R30 | 0,5 j-h | la recette, la porte n° 1 du jalon et le premier indicateur hebdomadaire portent tous les trois sur un inventaire périmé (la branche accueil supprime `PmsTabItem`/`PmsView` et ajoute `PosTabItem` sans toucher `tools/`) |
| 6 | **Lever ou amender le gel fonctionnel**, par une décision écrite dans `docs/stabilization/module-readiness.md`, en disant ce qui le remplace pendant V2 à V6 | 0,5 j-h | six lots (L3.1a, L4.1a, L4.2, L5.1a, L6.1a, L6.2) livrent du fonctionnel neuf sous un gel qui l'interdit, pendant 24 semaines, et le plan ne lève ce gel qu'à sa dernière vague |
| 7 | **Décider la politique de version livrée** : quand `reorg/phase-1` revient sur `main`, quelle branche est publiée, quelle branche sera figée au jalon (§6) | 0,5 j-h | `update-server.ps1` livrerait au client la tête du tronc |
| 8 | **D12 — réécrire l'EULA** pour retirer la promesse de licence à durée et clé de renouvellement, qu'aucune entité du domaine ne porte | 0,5 j-h | risque juridique inutile, alors que l'issue est gratuite et sans dépendance technique |
| 9 | **Ouvrir le chantier « textes et validation externe »**, avec un responsable et des dates opposables : champs obligatoires de la fiche de police **avant V3**, taux de taxe de séjour par catégorie **avant V4**, barème du droit de timbre **avant V6**, relecture du modèle de facture par un comptable ou un juriste **avant que L5.1a ne fige le gabarit** | 0,5 j-h d'ouverture, puis délais externes | quatre points sont chiffrés et calendarisés alors que le plan déclare lui-même ignorer la règle de droit qui les commande |
| 10 | **Ouvrir le chantier commercial et contractuel** : identification du pilote, prise de rendez-vous pour la visite de site W8.1 (à tenir **avant V2**), et cadrage du contrat W8.2 (à signer **avant V5**) | 0,5 j-h d'ouverture | tout le §5.2 repose sur des hypothèses non validées à propos d'un client qui n'est pas identifié |

**Total : ≈ 9,2 jours-homme, propriétaire seul, aucun agent en vol — soit 3 semaines à 3,5 j-h par
semaine.** Aucun jour-agent en S1.

**Ce qui a quitté S1 et où c'est allé.** Trois actions de la version antérieure étaient du développement
et non de l'intégration ; elles deviennent des lots de V0, avec un agent nommé :

| Ancienne action de S1 | Devient | Charge |
|-----------------------|---------|--------|
| Fusionner le lot ports `agent-a763d009c908f5cde` **en y ajoutant des tests de caractérisation sur les 7 ports** | **L0.6** (V0) | 5 j-a |
| Compléter puis fusionner **A6a** : `DbSet<UserUnitAssignment>`, migration générée seule | **L0.7** (V0) | 3 j-a |
| Compléter puis fusionner **D2a** : rebasage sur A6a, `DbSet<IdempotencyRecord>`, migration générée seule, reprise de `Program.cs` | **L0.8** (V0) | 3 j-a |

### 4.3 Deux points de vigilance sur les jetons partagés

**Dérogation 1 — plusieurs migrations dans la même vague.** Ce n'est plus une dérogation : la règle 2 a
été corrigée en « une migration **en vol** à la fois », qui est la contrainte réelle. L0.7, L0.8, L0.3a
et L0.3b portent chacun la sienne à l'intérieur de V0, sérialisées par le registre du détenteur, avec
rebasage sur le snapshot fusionné entre deux. **Ne jamais inverser L0.7 et L0.8** : les deux créent une
entité et D2a rebase sur A6a.

**Dérogation 2 — `Program.cs` détenu successivement par trois lots en V0.** Les deux worktrees écrivent
une ligne chacun, au même endroit (`+builder.Services.AddRaqmiUnitScope();` chez A6a,
`+builder.Services.AddRaqmiIdempotency();` chez D2a, tous deux insérés après `AddRaqmiInfrastructure`
ligne 38, vérifié) ; C1b y ajoute trois lignes pour exposer la version sur `/health`. Ordre imposé, sans
exception : **L0.7, puis L0.8, puis L0.1**. Résolu par la sérialisation, pas par un merge à trois
branches. *Et il faut compter la branche accueil/POS comme un quatrième écrivain tant que l'action 1
n'est pas tranchée.*

### 4.4 Ce que chaque lot vaut réellement

**`agent-a3b07db6dff7d6ed5` — documentation. Le seul complet.** 30 fichiers, 21 fiches
`docs/modules/*.md`, `documentationGrace.screens` ramené à `[]`, les 21 `"documentation": null`
remplacés par des chemins qui existent tous sur disque. **Fusionner en premier, dès S1.**
Charge restante : rejouer `check-module-readiness.ps1` pour confirmer le vert.

**`agent-a763d009c908f5cde` — ports. Propre, mais à dater.** 7 ports déclarés dans Application,
7 implémentations, cycles Lodging ↔ Housekeeping/Mice/Crm et Revenue ↔ Closing cassés,
`IDailyClosingReadService` marqué `[Obsolete]`. **Aucun test nouveau**, pour une refactorisation de
`LodgingService` (376 901 octets sur 15 partiels), `HousekeepingService`, `DailyClosingService` et
`DailyRevenueService`. **Conditions de fusion : par pull request (pour déclencher
`postgres-integration`) et avec ses tests de caractérisation** — c'est ce dernier point, et lui seul,
qui en fait un lot de développement de 5 jours-agent et non une fusion d'intégrateur. Contrainte de
calendrier : il touche 15 fichiers au centre du PMS et se périmera contre A4 (V3), A7 et B5 (V4).
**Soit il est fusionné en V0, soit il faut assumer de le jeter** plutôt que d'entretenir une dette de
fusion croissante.

**`agent-a7246a035c2ecd403` — A6a. Bon, mais ce n'est pas A6.** Voir l'arbitrage 1 du §1. Ce qui est
livré est solide et bien argumenté, en particulier la sémantique « aucune affectation = périmètre
global », qui rend le lot rétro-compatible et la restriction opt-in. Les 16 tests couvrent l'objet-valeur,
l'entité, le fournisseur, le jeton, les routes d'administration et le rôle ; **aucun n'affirme qu'un
compte restreint ne peut pas lire les réservations d'une autre unité.** Ces tests-là sont A6b, en V1.
Ne pas cocher A6 au tableau de bord à la fusion : cocher A6a.

**`agent-a7e76acfc1693b771` — D2a. Le mieux conçu côté serveur, mais inerte.** 101 routes d'écriture
marquées sur 275, la mise en tampon du corps **avant** la liaison des paramètres (détail subtil et
juste, sans lequel « même clé, corps différent » serait indétectable), 10 tests d'intégration. Mais
`IdempotencyOptions.Required` vaut **faux** par défaut, choix de compatibilité assumé et documenté
« parce que le client WPF en service n'envoie pas encore l'en-tête », et le lot ne touche **aucun**
fichier Desktop. **Tant que le client n'émet pas la clé, les 101 routes marquées ne protègent
personne.** La moitié cliente est ouverte comme lot L2.4 en V2 (12 j-a), avec la reprise bornée sur
les abandons 40001 (`EnableRetryOnFailure` est à 0) et la rédaction de `docs/api-idempotence.md`,
référencé par le code et absent du dépôt.

**`origin/feature/personalized-homepage` — le cinquième lot, et le seul qui soit poussé.** Il n'a pas
été évalué par ce plan et ne peut pas l'être depuis un dossier d'instruction en lecture seule : il faut
l'ouvrir, décider, et chiffrer la réconciliation. Ce qui est certain et mesuré : il porte deux migrations
commitées sur une lignée divergente, il touche six des huit fichiers-carrefours, il modifie l'inventaire
des écrans sans toucher `tools/readiness/screens.json`, et il livre un module que le §5.2 déclare hors
périmètre. **C'est l'action 1 de S1, et rien ne se lance avant.**

### 4.5 Une action d'hygiène à ne pas oublier

Supprimer le clone imbriqué `C:\Users\HP\Desktop\Raqmi_System\Raqmi_System\` — 132 Mo, dépôt git
complet à part entière, visible comme `?? Raqmi_System/` dans `git status`. Un second dépôt à
l'intérieur du répertoire de travail est un piège : un agent ou un script qui s'y positionne commitera
dans le mauvais dépôt sans qu'aucune alerte ne se déclenche.

---

## 5. Jalon « premier client pilote »

**Ce jalon est technique. Il n'ouvre pas l'hôtel.** Franchir les portes ci-dessous signifie qu'une build
candidate est livrable ; la mise en service chez le client est une vague à part entière, V8 (§3.8), avec
ses propres charges, ses propres délais externes et ses propres critères d'arrêt. Confondre les deux est
l'erreur que la version antérieure de ce document commettait : elle appelait « premier client pilote »
un jalon dont les quatre lots avaient pour périmètre des fichiers du dépôt.

### 5.1 Liste de contrôle de mise en service

Le jalon est franchi quand **toutes** les portes suivantes sont vertes **simultanément, sur la même
build candidate**. Aucune exception, aucune compensation entre portes.

**Portes automatiques — job `pilot-gate` (L7.2).** *Avant d'être opposée à qui que ce soit, cette liste
doit être jouée une fois sur `reorg/phase-1` et sa sortie réelle consignée en regard (règle 9). La
version antérieure de ce document portait deux portes — les n° 5 et 7 ci-dessous — dont la syntaxe
d'expression régulière les rendait inatteignables par construction : le job `pilot-gate` n'aurait
jamais pu passer au vert.*

| # | Commande | Attendu |
|---|----------|---------|
| 1 | `grep -c '"smoke": null' tools/readiness/screens.json` | 0 |
| 2 | `pwsh ./tools/check-module-readiness.ps1 -AsOf 2027-01-01` | code 0 |
| 3 | `dotnet test --filter 'Category=Postgres'` | ≥ 10 tests **exécutés**, 0 échec |
| 4 | `git ls-tree -r --name-only HEAD -- deploy \| grep -c 'update-server.ps1\|restore-raqmi.ps1'` | 2 *(BRE : l'alternation échappée est ici correcte, car il n'y a pas de `-E`)* |
| 5 | `git grep -cE 'UseHttpsRedirection\|UseHsts\|AddRateLimiter' -- src` | ≥ 2 |
| 6 | `git grep -c 'GRANT USAGE ON SCHEMA' deploy/postgres/create-app-role.sql` | 19 |
| 7 | `git grep -cE '<Version>\|<VersionPrefix>' -- '*.props'` | ≥ 1 |
| 8 | `curl -s http://<serveur>/health` | renvoie la version issue du tag Git *(livré par C1b, L0.1, V0 — pas par C1a)* |
| 9 | `dotnet build -warnaserror src/RaqmiSystem.Domain src/RaqmiSystem.Application` | vert |
| 10 | `ls docs/guide/ \| wc -l` | 3 (un guide par rôle) |
| 11 | `ls docs/exploitation.md docs/secours-reception.md` | les deux existent |
| 12 | `ls docs/juridique/eula.md docs/juridique/contrat-pilote.md` | les deux existent et sont signés (D12, W8.2) |
| 13 | `grep -c 'dotnet publish' deploy/onpremise/install-server.ps1` | 0 — l'installeur pose un binaire, il ne compile plus sur le poste du client (D13) |

**Portes humaines — trois procès-verbaux datés, signés, versionnés dans `docs/exploitation/`.**

1. **Restauration complète devant témoin.** Sur une machine vierge, à partir du seul dump chiffré sorti
   de la machine d'origine et de la sauvegarde de `raqmi.env.ps1` : le service remonte, `/health`
   répond, un utilisateur se connecte et retrouve la réservation créée avant la sauvegarde.
   *Ce scénario échoue aujourd'hui pour deux raisons cumulées : le dump ne contient pas 4 schémas sur
   19, et `backup-raqmi.ps1` ne sauvegarde que la base — les secrets générés une fois et jamais
   affichés ne sont nulle part.*
2. **Mise à jour et retour arrière.** Deux mises à jour successives via `update-server.ps1`, puis une
   troisième volontairement cassée qui restaure seule `api-previous\`, l'API redémarrant après un
   reboot complet du serveur.
3. **Bascule papier.** Coupure simulée de 30 minutes en service, arrivées traitées sur fiche papier,
   ressaisie ensuite, et contrôle qu'aucune arrivée n'a été saisie deux fois.

**Démonstration intégrée** — en une seule séance, sur un seul jeu de données, enregistrée :

> réservation → check-in avec saisie d'identité des occupants → nuitée avec taxe de séjour posée
> automatiquement → charges de folio → règlement en espèces créant un `CashReceipt` rattaché à une
> session de caisse et portant le droit de timbre → facture émise avec le NIF de l'unité et un numéro
> de la série de cette unité → PDF archivé et réimprimable par son numéro → avoir partiel dans sa
> propre série → check-out refusé tant que le folio n'est pas soldé → night audit rejoué deux fois sans
> doublon → clôture de la journée → restauration de la base sur une machine vierge et reprise de la
> même séance.

**Cette démonstration est le scénario E2E qui manque, et elle ne doit pas rester manuelle.** Le modèle
de readiness du projet réserve le niveau « Production Ready » aux écrans qui portent
`productionReady.postgresqlCi` **et** `productionReady.e2e` — aujourd'hui `null` pour les 30 écrans.
Sans automatisation, le pilote ouvre en production avec trente écrans que la gouvernance du projet
qualifie explicitement de non exploitables en production, et aucune vague ne prévoit d'y remédier.
**Arbitrage à rendre, au plus tard à la revue de fin de V4, et à écrire :**
*(i)* automatiser ce scénario en V5 ou V6 comme lot nommé, et renseigner `postgresqlCi` et `e2e` au
moins sur la chaîne réservation → folio → facture → document ; **ou** *(ii)* amender le modèle de
readiness pour que « Production Ready » désigne quelque chose d'atteignable avant le pilote. Un
procès-verbal ne résout pas la contradiction entre « non exploitable en production » et « mis en
production ».

### 5.2 Ce qui est explicitement exclu du périmètre pilote

À condition de l'écrire dans le contrat — **contrat que le lot W8.2 (§3.8) rédige et fait signer, et
qui est une dépendance de calendrier de V5, pas une formalité de fin** :

- **B10 — déversement comptable automatique.** Uniquement si le pilote est une **unité unique**, que le
  comptable exporte factures et encaissements une fois par mois, et que l'export des livres est livré.
  *Hypothèse sur le client, à valider par la visite de site W8.1, avant V2.*
- **B11 — états financiers SCF** et **B13 — immobilisations** : pas dus avant la première clôture
  annuelle.
- **B8 + D4 — la paie** : hors périmètre pilote, module grisé (arbitrage 4 du §1). *Hypothèse sur le
  client — il faut qu'il accepte de faire sa paie ailleurs — à valider par W8.1.*
- **B9 — G50 et liasse fiscale.**
- Channel manager, moteur de réservation directe, MICE, PortMaster.
- **POS restaurant — statut suspendu.** Il figurait dans cette liste alors qu'il est **déjà écrit** sur
  `origin/feature/personalized-homepage`, avec ses entités, ses permissions, son écran et deux
  migrations. L'arbitrage du §4.2 action 1 tranche : s'il entre au périmètre, cette ligne disparaît et
  le POS reçoit ses propres critères de recette ; s'il est mis de côté, il faut dire **où** son code va
  vivre en attendant.
- Mode dégradé hors ligne : le refus de la file de rejeu est le bon arbitrage, compensé par C6.
- **D11 — l'arabe et le RTL.**
- **Le multi-unités réel.**
- **Toute reprise de données de l'exploitation antérieure** — voir §5.3 point 3, et W8.5 (mois de double
  saisie), qui en est la contrepartie chiffrée.
- Les 20 modules « Planifié » de `ModuleCatalog.cs`. **Leur traitement est un lot nommé (L0.2, V0), pas
  une intention** : la version antérieure exigeait deux fois qu'ils soient « grisés avec une date » sans
  confier `ModuleCatalog.cs` à aucun lot, sans charge et sans critère — et la date exigée n'existe nulle
  part, le §6 conditionnant ces modules à « tout ce qui précède ». **Arbitrage à rendre en S1, entre deux
  options seulement :** *grisé sans date, avec la mention « non prévu pendant la durée du pilote »*, ou
  *retiré du catalogue pour la durée du pilote*. Une date inventée n'est pas une troisième option : ce
  serait un engagement commercial envers le pilote.

### 5.3 Ce qu'il faut refuser de promettre par écrit

Repris de l'avis, et complété.

1. **Le G50 et la liasse fiscale (B9).** Non productibles, même à la main, faute de TVA déductible :
   `PurchaseOrderLine` porte 9 propriétés et aucune n'est une TVA ; `SupplierInvoice` n'existe pas.
2. **Le multi-unités.** Le tableau de bord groupe existe à l'écran, mais l'architecture est **une base
   PostgreSQL par site, sans réplication**. Livrer A6 rend le multi-unités *sûr*, pas *possible*.
3. **Toute reprise de données.** Zéro route `import|bulk|batch` sur les 33 fichiers d'endpoints, aucun
   solde d'ouverture, aucun `HasData` dans `ChartAccountConfiguration.cs` — **le plan comptable SCF
   n'est même pas pré-alimenté** — et la date d'arrivée d'un walk-in n'est pas saisissable. **Ce PMS ne
   sait pas démarrer dans un hôtel déjà ouvert** : exiger une mise en service sur un hôtel vide, ou un
   mois de double saisie assumé et facturé. Ce mois est désormais chiffré et calendarisé : c'est W8.5.
4. **La paie**, tant que B8 n'est pas fait et validé par un expert-comptable agréé.
5. **Une licence** avec date d'expiration et clé de renouvellement : l'EULA la promet, aucune entité de
   licence n'existe dans le domaine. **D12 est traité en S1** (§4.2, action 8) : réécrire l'EULA n'a
   aucune dépendance technique, coûte une demi-journée, et supprime le risque juridique sans attendre
   C1. La version antérieure de ce plan citait cette issue sans jamais lui donner de ligne, de charge ni
   de critère — D12 n'apparaissait dans aucun lot et dans aucune vague.
6. **Tout engagement de disponibilité** : PC serveur unique, sans onduleur exigé, sans failover.
7. **La livraison du code source — jusqu'à D13, désormais traité.** `docs/deployment-onpremise.md`
   exige aujourd'hui le SDK .NET et un clone du dépôt sur le PC du client, et `install-server.ps1:286`
   exécute `dotnet publish` sur place : le client pilote recevrait le code source complet et
   l'historique Git le jour de l'installation, en contradiction directe avec l'EULA §2 qu'il signe.
   **D13 entre dans L1.4a** (publication en amont, livraison d'un binaire, retrait de l'exigence de SDK
   et de clone), avec le job d'installeur signé de L4.4. La version antérieure de ce plan faisait
   réécrire `install-server.ps1` **deux fois** — L1.4 puis L2.3 — sans jamais y faire entrer D13, ce qui
   garantissait une sixième ouverture du fichier après coup.
8. **Toute promesse de parité fonctionnelle avec le produit legacy.** Voir §7.3.
9. **Tout délai de correction ou de réponse** avant que le modèle de support de W8.6 soit écrit et que
   la branche de publication existe : sans branche `release/x.y` figée, `update-server.ps1` livrerait au
   client la tête du tronc, c'est-à-dire les vagues suivantes en cours (§3.8, W8.6).

---

## 6. Après le pilote

**Rien de ce qui suit ne démarre le lendemain de l'ouverture.** Le pilote consomme de la capacité :
V8 (§3.8) occupe le propriétaire à plein temps pendant six à sept semaines, et le support du client en
exploitation consomme ensuite **une réserve permanente de 10 jours-agent par mois et d'un jour-homme par
semaine**, qui sort des totaux de développement et n'est pas récupérable. La version antérieure de ce
document enchaînait P1 immédiatement après le jalon, avec les mêmes quatre agents et le même
intégrateur, et réservait zéro capacité pour exploiter ce qu'elle venait de livrer.

**Politique de version livrée — à décider en S1, à mettre en œuvre au jalon.** Ce plan n'en avait
aucune : il ne disait ni quand ni comment `reorg/phase-1` revient sur `main` (35 commits d'avance, zéro
en sens inverse, alors que la CI de référence sur `push` est cadrée sur `main`), ne créait aucune branche
de maintenance, et ne décrivait aucun chemin de correctif pour un client en production pendant que le
tronc avance de plusieurs vagues. Les trois décisions à écrire :

1. **Branche de publication.** Quand `reorg/phase-1` fusionne vers `main`, et sous quelles conditions
   (au plus tard à la fin de V0, pour que le gate PostgreSQL de référence tourne sur le tronc).
2. **Branche figée au jalon.** Une branche `release/x.y` créée à la build candidate du §5.1, avec la
   règle écrite de ce qui a le droit d'y être rétroporté (correction de données, correction de sécurité,
   correction bloquant l'exploitation — rien d'autre), **qui le décide**, et comment le gate PostgreSQL
   est rejoué dessus.
3. **Ce que livre `update-server.ps1`.** Le binaire de `release/x.y`, jamais la tête du tronc. C8 (L5.4)
   détecte l'incompatibilité poste N-2 / serveur N, C1 pose le numéro de version, A3 pose le retour
   arrière — mais aucun des trois ne dit d'où sort le binaire qu'on livre. C'est cette décision qui le
   dit.

Ordre recommandé pour la suite, en moins de détail. Chaque étape suppose la précédente.

**P1 — D2 achevé et B10, le déversement comptable automatique (30 à 45 j-h).** Prérequis technique
réel : la clé d'idempotence, sinon un événement rejoué produit une écriture en double, ce qu'une
comptabilité ne tolère pas. Le coffre-fort est aujourd'hui vide : `IAccountingService` n'est référencé
que par 4 fichiers et **aucun module métier ne l'injecte**. Bloquant à la première clôture mensuelle,
pas le jour de l'ouverture. **Contre-recommandation 4 de l'avis : nouveau `DbContext` borné pour
l'outbox, ne pas étendre `RaqmiDbContext`.**

**P2 — B8 + D4 + la reprise du module RH (26 j-h de développement, plus un délai calendaire externe).**
Les trois défauts convergent sur la même donnée et doivent tenir dans **un seul lot**, comme l'impose
le risque R-D. **Lancer la validation par l'expert-comptable agréé au début du lot, pas à la fin :**
c'est elle qui fixe le délai, pas le code. Critère de fini particulier : les 8 tests actuels de
`AlgerianPayrollEngineTests.cs` encodent la formule fausse dans leurs commentaires — **ils doivent
échouer avant réécriture**, ce qui prouve que la correction change bien le résultat.

**P3 — B11, les états financiers SCF et la clôture d'exercice réelle (28 à 40 j-h).** Le danger n'est
pas le rendu des états mais la clôture : `CloseFiscalYearAsync` ne fait aujourd'hui qu'un changement de
statut, donc sans génération d'à-nouveaux le bilan de la **deuxième** année est faux par construction.
Bombe à retardement à douze mois.

**P4 — B13, les immobilisations (20 à 28 j-h).** Greenfield complet : aucun répertoire `Assets` dans
aucune des quatre couches, donc aucune collision hors migration et catalogue de permissions. Un hôtel
est une activité capitalistique : sans dotations, le résultat comptable **et** fiscal sont faux.

**P5 — C10, l'import et les soldes d'ouverture.** C'est la condition pour vendre à un hôtel déjà
ouvert, c'est-à-dire à tous sauf le pilote. Dépend de B6, livré en V0.

**P6 — D5 et D6, la couche Application et l'architecture de présentation.** 503 fichiers dans
`Application/`, 451 `sealed record`, 45 interfaces, **0 cas d'usage** ; et `LodgingService` reste un
type unique de 376 901 octets. À mener **après** le pilote, ou strictement au fil des lots qui touchent
déjà les fichiers — jamais pendant une vague qui livre du métier sur le PMS.

**P7 — D7 (OpenAPI publié et découplage binaire du client) et D8 (volumétrie).** `GET
/accounting/entries` n'est pas paginé et matérialise le grand livre entier ; le tape chart n'a aucune
virtualisation. « Le système marchera parfaitement la première année. »

**P8 — D11, l'arabe et le RTL.** Aucun `.resx` dans le dépôt, aucun `FlowDirection`. **C'est le seul
point dont le coût croît avec le report** : quasi nul aujourd'hui, des mois dans deux ans. Argument
pour poser dès maintenant la convention — un dictionnaire, un `FlowDirection` unique — sans attendre
D6.

**P9 — l'élargissement du catalogue.** POS restaurant, channel manager, moteur de réservation directe,
MICE, PortMaster. **Aucun avant que tout ce qui précède soit livré**, et aucun des 20 modules
« Planifié » ouvert entre-temps.

---

## 7. Gouvernance

### 7.1 Rythme et capacité

- **Vague de 4 à 8 semaines**, 4 agents productifs, 1 intégrateur — le propriétaire.
- **Le propriétaire est une ligne de capacité, pas une ressource infinie.** Il est simultanément
  l'unique décideur, l'unique intégrateur, l'architecte, le recetteur et le témoin. Sa capacité est
  plafonnée à **3,5 jours-homme nets par semaine**, dont **2 j-h vont à l'intégration continue et à la
  revue de fin de vague** et **1,5 j-h restent pour l'arbitrage et les postes humains**. Cette ligne
  n'est **jamais** additionnée aux jours-agent (§2.1). Deux vagues font exception et le disent : S1 (pas
  d'agent en vol, donc 3,5 j-h disponibles) et V7 (le propriétaire cesse d'intégrer et consacre ses
  3,5 j-h à la recette).
- **Ce qui s'arrête quand le propriétaire s'arrête.** Une semaine d'absence arrête : l'intégration (donc
  toutes les fusions, donc tous les agents au bout de leur lot courant), le jeton EF (dont il tient le
  registre), les trois arbitrages ouverts, la recette et les procès-verbaux. **Le calendrier s'interrompt
  à chaque absence, et se décale d'autant.** Deux issues, à trancher : désigner un second intégrateur
  formé aux six vérifications de la règle 6, ou acter par écrit que congés, maladie et déplacements
  décalent le jalon. Ne pas trancher revient à choisir la seconde sans le dire.
- **Fusion en continu, jamais de « big bang » de fin de vague.** La vague 1 du projet s'est intégrée
  lot par lot (quatre commits de fusion successifs dans l'historique) et a réussi ; la vague 2 a gardé
  quatre lots ouverts simultanément et n'a rien intégré. **Aucun agent ne démarre un nouveau lot tant
  qu'un lot précédent du même périmètre n'est pas fusionné.** La vague se ferme quand les PR sont
  fusionnées, pas quand les agents ont fini d'écrire.
- **Revue de fin de vague, une demi-journée**, avec quatre sorties écrites : les critères d'acceptation
  cochés ou non (et pourquoi), la mise à jour de `docs/stabilization/module-readiness.md`, la
  réattribution nominative des **huit** fichiers-carrefours pour la vague suivante, et **le relevé du
  temps réellement consommé par lot**, qui est la seule façon de remplacer l'hypothèse de rendement du
  §2.1 par une mesure.
- **Jours non ouvrés.** Les durées de ce document sont des semaines **de travail**. Le calendrier réel
  doit en plus décompter les jours fériés algériens, la période du Ramadan et l'Aïd, et intercaler
  **une semaine de marge nommée après V0, V2, V4 et V6** — quatre semaines, écrites, plutôt qu'une marge
  cachée dans une vague comme l'était la trêve de fin d'année dans la version antérieure.

### 7.2 Règles d'intégration

**Règle 1 — propriété exclusive, écrite et affichée.** Huit fichiers-carrefours ont un détenteur nommé
par vague. Tout lot qui en veut deux attend. **Une branche non fusionnée compte comme un détenteur** :
`origin/feature/personalized-homepage` détient aujourd'hui six des huit, ce qui est la raison pour
laquelle son arbitrage passe avant tout (§4.2, action 1).

| Fichier | Points qui le convoitent | Mesure |
|---------|--------------------------|--------|
| `Persistence/Migrations/RaqmiDbContextModelSnapshot.cs` | 14 | 401 133 octets |
| `Persistence/RaqmiDbContext.cs` | 14 | point d'entrée `ApplyConfigurationsFromAssembly` (l. 273) |
| `Infrastructure/Lodging/LodgingService.*` | 7 | 376 901 octets, 15 partiels d'un seul type |
| `Infrastructure/Billing/BillingService.cs` | 7 | — |
| `Api/Program.cs` | 5 | 218 lignes |
| `Domain/Identity/PermissionCatalog.cs` + `PermissionRegistry.cs` | 4 | 61 Ko, référencés par 73 fichiers |
| `Desktop/ModuleCatalog.cs` + `MainWindow*` + `Themes/RaqmiTheme.xaml` | 4 | coquille WPF, 50 entrées de catalogue |
| `deploy/onpremise/install-server.ps1` | 6 | 485 lignes |

**Règle 2 — une migration EF *en vol* à la fois, une seule par PR.** Le fait établi est qu'une seule
migration peut être **générée** à la fois, parce que le snapshot de 401 Ko entre en conflit. Ce n'est pas
une contrainte de fréquence : l'historique du dépôt porte **22 migrations en cinq jours**, dont deux
séparées de 19 secondes et deux autres portées par des commits de 16 h 51 et 17 h 41 le même après-midi,
chacune avec son `Designer.cs` de ~9 500 lignes. **Sérialiser deux migrations coûte une file d'attente de
quelques heures, pas un créneau de vague.** La version antérieure de ce document convertissait la
contrainte de simultanéité en « une migration par vague de 4 à 6 semaines » — un facteur d'environ 30 —
et facturait cinq semaines de calendrier pour cela ; ces cinq semaines sont retirées, et l'arbitrage 3
du §1 est rejoué en conséquence. La règle opérationnelle est donc :

1. un **registre écrit** dit qui détient la migration, à l'heure près, et pour combien de temps ;
2. le détenteur génère, ouvre sa PR, la fait passer au gate, fusionne, puis **rend le jeton** ;
3. la PR suivante **rebase sur le snapshot fusionné** avant de générer la sienne ;
4. une PR porte **une migration ou zéro**, jamais deux.

C'est ce qui rend possibles les découpes séquentielles de ce plan — L0.3a/L0.3b, L0.7/L0.8,
L3.1a/L3.1b, L6.1a/L6.1b — dont chacune porte sa propre migration à l'intérieur d'une même vague.
Rappel mécanique inchangé : `RaqmiDbContext.cs:273` appelle `ApplyConfigurationsFromAssembly`, donc
**ajouter un `*Configuration.cs` sous Infrastructure suffit à faire échouer `postgres-integration`**,
même sans toucher au `DbContext`.

**Règle 3 — ordre de fusion par contention croissante, jamais par importance.**
(1) lots sans aucun fichier sous `src/` (documentation, exploitation, CI) — ils ne peuvent casser que
le garde de readiness ; (2) le lot détenteur du jeton EF, avec sa migration unique ; (3) les lots
Domain/Application/Infrastructure sans jeton ; (4) les lots API ; (5) la coquille WPF ; (6) les écrans,
qui rebasent dessus. Rationnel : tout lot fusionné après le lot EF doit rebaser sur un snapshot de
401 Ko ; le faire passer tôt réduit le nombre de rebases coûteux par vague.

**Règle 4 — fusionner par pull request, jamais par poussée directe.** Levier gratuit vérifié :
`dotnet.yml` porte `pull_request:` sans filtre de branche et `postgres-integration` n'a aucune
condition `if:`. Une PR vers `reorg/phase-1` déclenche donc dès aujourd'hui readiness + build WPF +
tests + build-core + postgres-integration + build-desktop, **sans modifier un octet de workflow**.

**Règle 5 — conflits sur un fichier partagé : ne jamais merger, toujours réordonnancer.** L'agent non
détenteur produit un **fragment de patch** (bloc + emplacement) dans son message de livraison ;
l'intégrateur l'applique dans le lot du détenteur.

**Règle 6 — vérifications obligatoires avant fusion, dans cet ordre.**
(1) `git status --porcelain` du worktree **vide** — s'il ne l'est pas, le lot n'existe pas.
(2) `pwsh ./tools/check-module-readiness.ps1` code 0 — il tourne avant toute compilation et échoue
vite. (3) Build WPF + suite complète. (4) `postgres-integration` vert, `HasPendingModelChanges()`
compris. (5) **Contrôle manuel : `git diff --name-only <base>..<lot>` ne contient aucun fichier hors du
périmètre déclaré** — c'est ce contrôle, absent lors de la vague 2, qui aurait révélé la double
détention de `Program.cs`. **Corollaire, à vérifier au moment d'écrire la consigne et non au moment de
fusionner : le périmètre déclaré doit contenir tous les fichiers que les critères d'acceptation et les
gardes du lot obligent à écrire** — fichiers de migration et `RaqmiDbContext.cs` pour un détenteur du
jeton, chemins de test pour tout lot dont un critère est un `dotnet test`. Un lot dont le périmètre est
incomplet est refusé par cette règle alors qu'il a bien fait son travail. (6) Une seule migration dans
la PR, ou zéro.

**Règle 7 — ce que toute consigne d'agent doit contenir.** (a) La liste **exhaustive et nominative**
des fichiers détenus, chemin par chemin — pas « le module sécurité », **et pas non plus « les 15
fichiers portant tel motif » : la caractérisation est remplacée par la sortie de la commande, collée
telle quelle**. (b) La liste nominative des fichiers **interdits**, avec le nom de l'agent qui les
détient cette vague. (c) Le SHA du commit de base. (d) Détient-il le jeton EF, oui ou non — si non,
interdiction d'ajouter tout `*Configuration.cs` sous Infrastructure ; si oui, obligation d'ajouter le
`DbSet` **et** de générer la migration dans la même PR, et son périmètre **doit** inclure
`src/RaqmiSystem.Infrastructure/Persistence/Migrations/**` et `Persistence/RaqmiDbContext.cs`.
(e) L'obligation de **commiter au moins une fois avant de rendre la main**, sur sa propre branche, en
Conventional Commits. (f) Pour toute nouvelle route, la clé `PermissionCatalog` à livrer en fragment de
patch. (g) Le niveau de test exigé selon la table de `07-plan-migration.md`. (h) S'il touche un écran,
la ligne de `tools/readiness/screens.json` à mettre à jour. (i) **L'interdiction de reformater** : dans
`agent-a7246a0`, cinq fichiers ont vu leurs fins de ligne réécrites en LF, ce qui transforme un diff de
quelques lignes en diff de fichier entier et rend la revue impossible. (j) **Case obligatoire : « ce lot
modifie-t-il une propriété d'une entité mappée ? »**, accompagnée de la liste des `*Configuration.cs`
présents dans son périmètre, produite par `git ls-tree` sur les chemins du lot. Si la case est cochée et
que le lot ne détient pas le jeton, **la conception est reprise avant de lancer l'agent** : soit le
changement se pose sans nouvelle colonne (à démontrer, pas à supposer), soit ses colonnes sont
pré-portées par la migration du détenteur du jeton de la vague — la manœuvre déjà réussie pour B4 et B5
par L2.1.

**Règle 8 — instrumenter plutôt que répéter.** Trois automatismes, moins d'une journée à eux trois,
qui adressent exactement les trois causes mesurées de l'échec de la vague 2 : (a) un fichier `OWNERS`
par vague et un script d'intégrateur qui compare `git diff --name-only` à ce fichier et refuse le lot
en cas de débordement ; (b) une étape de CI qui échoue si une PR ajoute un `*Configuration.cs` sous
`src/RaqmiSystem.Infrastructure/` sans toucher à `Persistence/Migrations/` ; (c) un `CODEOWNERS`
rendant les huit fichiers-carrefours visibles dans l'interface de revue. **Les trois sont livrés en V0
par L0.1, pas « un jour »** : (b) en particulier est la seule protection automatique contre la classe
de collision la plus coûteuse du projet, et son absence est ce qui a permis à un joker de répertoire
d'avaler quatorze `*Configuration.cs` dans la version antérieure de la vague 1.

**Règle 9 — aucune « valeur d'aujourd'hui » n'est publiée sans avoir été jouée une fois.** Tout critère
de ce document qui annonce un chiffre de départ doit avoir vu sa commande exécutée sur la révision
citée, et sa sortie réelle consignée en regard. Deux corollaires appris à nos dépens.
**(a) Une commande qui ne sait pas distinguer « rien à faire » de « erreur de syntaxe » est interdite** :
cinq critères de la version antérieure utilisaient une alternation échappée dans une expression
régulière **étendue**, où la séquence barre-oblique + barre-verticale désigne une barre verticale
**littérale** ; ils renvoyaient donc 0 quel que soit l'état du code, ce que leur « valeur d'aujourd'hui :
0 » rendait invisible. Deux d'entre eux étaient des portes du jalon pilote, que le job `pilot-gate`
n'aurait jamais pu passer au vert. La forme correcte est l'alternation **non échappée** sous `-E`, ou
l'alternation échappée **sans** `-E`.
**(b) Un critère vert au départ n'est pas un jalon de sortie** : `grep -c 'MapPost("/periods'` renvoie
déjà **1** sur `reorg/phase-1`, parce qu'il matche la *clôture* de période (`/periods/{id}/close`) et
non sa *création*. Quand une commande ne sait pas discriminer, elle est remplacée par un test compilé
nommé.

### 7.3 Gestion des risques

| Risque | Traité dans | Parade livrée | Preuve d'extinction |
|--------|-------------|---------------|---------------------|
| **R-A** — la sauvegarde qui n'a jamais existé | **L0.1** (A2) puis **L1.4a et L1.4b** (A3, A12, C9, D13) | 19 GRANT, test de couverture des schémas, dump chiffré hors machine, `restore-raqmi.ps1`, restauration de vérification hebdomadaire | `pg_restore` sur base vierge → 104 tables ; PV de restauration signé ; gardes G4 et G5 |
| **R-B** — le détournement de recette que rien ne peut constater | **L0.5** (audit de la purge, plancher de rétention, REVOKE) puis **L4.1a et L4.1b** (session de caisse, plafond et second acteur sur l'ajustement négatif, clé d'annulation distincte) | l'audit devient inaltérable, et tout geste de caisse laisse une trace | `select privilege_type … table_name='audit_logs'` ne contient plus UPDATE ni DELETE ; gardes G12, G17, H4 |
| **R-C** — la première mise à jour tue le serveur du client | **L1.4a** (A3) | `update-server.ps1` idempotent, sauvegarde préalable, sonde `/health`, rollback automatique ; correction du bug d'écriture du mot de passe applicatif (`install-server.ps1` écrit `$appPassword` dans `raqmi.env.ps1` avant de savoir si le rôle existe déjà) | PV de mise à jour et retour arrière ; troisième mise à jour volontairement cassée qui revient seule |
| **R-D** — la paie fausse, découverte rétroactivement | **hors pilote — P2**, module RH grisé, refus écrit au contrat | A2 + B8 + D4 dans un lot unique, validation par un expert-comptable agréé **en entrée** de lot | les 8 tests actuels échouent avant réécriture ; 8 scénarios signés par l'expert |
| **R-E** — le contrôle qui trouve un hôtel sans pièces | **L2.1** (B1), **L3.2** (A4), **L5.1a** (A5), **L6.1a + L6.1b** (B3, B4), **L6.2** (B2b) | la chaîne facture → document → registre est livrée entière, et l'annulation d'une facture émise devient impossible | démonstration intégrée du §5.1 ; `POST /invoices/{id}/cancel` sur `Issued` → 409 |

**Risque à ouvrir, et que personne ne prendra à la place du propriétaire.**
`docs/reorganisation/06-risques.md` contient 25 risques, **tous techniques**, et pas un seul sur la
régression fonctionnelle vis-à-vis du produit legacy ni sur la reprise de ses données ; le seul qui
mentionne le legacy (R17) le formule à l'envers. **Ouvrir R26 — parité fonctionnelle et reprise des
données — et l'arbitrer par écrit** : soit une réécriture longue sans client entre-temps, soit une date
de parité fonctionnelle. Cette question ne se laissera pas résoudre par une commande, et elle décide du
calendrier de tout le reste. *Réserve : les 25 entrées du registre n'ont pas été relues une par une
pour ce plan ; vérifier avant d'arbitrer.*

### 7.4 Indicateurs à suivre semaine après semaine

Neuf chiffres, tous obtenus par une commande, relevés à la revue hebdomadaire. Un tableau, pas un
rapport. **Chaque « valeur de départ » ci-dessous doit être rejouée une fois en S1 avant d'être opposée
à quoi que ce soit** (règle 9) : l'inventaire des écrans, en particulier, change avec l'arbitrage sur
`origin/feature/personalized-homepage`.

| Indicateur | Commande | Valeur de départ | Cible au pilote |
|------------|----------|------------------|-----------------|
| Écrans sans smoke validé | `grep -c '"smoke": null' tools/readiness/screens.json` | 30 **avant** l'arbitrage POS — à figer en S1 après régénération de `screens.json` | 0 |
| Écrans sous clause de grâce | longueur de `documentationGrace.screens` | 21 | 0 dès S1 |
| Schémas non sauvegardés | 19 − `grep -c 'GRANT USAGE ON SCHEMA' …` | 4 | 0 |
| Services sans filtre de périmètre | fichiers d'`Infrastructure/` avec un `DbSet` et sans `IUnitScopeProvider` | ~50 | 0 |
| Tests PostgreSQL réellement exécutés | log du job `postgres-integration` | 0 (jamais lancé sur la branche) | ≥ 10, et croissant |
| Lots non commités dans les worktrees | somme des `git status --porcelain` des worktrees | 113 fichiers | 0 en permanence |
| **Lignées de migration divergentes** | `git branch -r` × dernier horodatage de `Persistence/Migrations/` par branche | **2** (`reorg/phase-1` et `origin/feature/personalized-homepage` ont bifurqué après `20260901130213_WaveKpi`) | 1 |
| Migrations en vol simultanément | registre écrit du détenteur du jeton EF | **4** — 2 dormantes non commitées (A6a, D2a) et 2 déjà commitées sur la branche accueil/POS | 1 au maximum |
| **Capacité propriétaire consommée** | j-h d'intégration + arbitrage + recette de la semaine, relevés à la main | non mesurée | ≤ 3,5 j-h/semaine, et le dire quand c'est dépassé |

Quatre d'entre eux sont des indicateurs de **discipline**, pas d'avancement : lots non commités,
migrations en vol, lignées divergentes, et débordements de périmètre détectés par la règle 6.5. Ce sont
eux qui ont fait échouer la vague 2 ; ce sont eux qu'il faut regarder en premier. Le dernier — la
capacité du propriétaire — est le seul qui puisse arrêter le plan sans qu'aucune commande ne devienne
rouge.

---

## 8. Ce que ce plan ne fait pas

### 8.1 Les huit contre-recommandations de l'avis, reprises et appliquées

1. **Ne pas réduire `AccessTokenMinutes` à 10-15 minutes.** Tant que le client ne renouvelle rien, cela
   transforme une panne horaire en panne au quart d'heure. *Application :* le durcissement des durées
   n'apparaît nulle part avant que L0.2 (A1) soit vert, et c'est écrit dans la vague 0.
2. **Ne pas ajouter de paquet d'analyseurs (StyleCop, Roslynator, Sonar).** `AnalysisLevel=latest` est
   déjà actif et les analyseurs .NET tournent à chaque build ; le problème est qu'ils n'ont aucune dent.
   *Application :* L2.5 durcit ce qui existe (`TreatWarningsAsErrors` sur Domain et Application,
   `LangVersion` épinglé) et n'ajoute aucune seconde couche de bruit.
3. **Ne pas lancer la refonte MVVM complète du client WPF.** *Application :* L5.2 introduit des
   ViewModels sur le seul écran que V5 touche de toute façon, et le projet de tests Desktop est créé
   dès L0.2, en vague 0.
4. **Ne pas partir sur un `DbContext` par contexte borné en rétro-adaptation.** Découper les
   99 `DbSet` sur 22 migrations existantes coûterait des mois sans gain visible. *Application :* la
   règle ne vaut que pour les **nouveaux** contextes — l'outbox de P1 en aura un — et le plan pose à la
   place une convention de propriété du snapshot.
5. **Ne développer aucun des 20 modules « Planifié ».** Plus d'un tiers des portes de l'application
   s'ouvrent sur rien. *Application :* aucun lot de développement de ce plan n'ouvre un module Planifié.
   **Réserve mesurée, à lever en S1 :** cette contre-recommandation est **déjà enfreinte hors du plan**.
   La branche `origin/feature/personalized-homepage` (9 commits d'avance sur `reorg/phase-1`,
   26 fichiers, +20 883 lignes, vérifié) livre un **module POS complet** — `Domain/Pos/PosEntities.cs`,
   `Infrastructure/Pos/PosService.cs` et `PosConfigurations.cs`, `Api/Endpoints/PosEndpoints.cs`,
   `Desktop/Views/PosView.*`, 8 clés dans `PermissionCatalog.cs`, et **deux migrations EF**
   (`20260903235736_WavePointOfSale`, `20260904000822_PosOutletWarehouses`). Le POS est simultanément
   exclu du périmètre pilote au §5.2 et déjà écrit sur une branche poussée. **Le §4.2 action 1 tranche :
   soit le POS entre au périmètre et le §5.2 est réécrit, soit il est mis de côté et on dit où.** Tant
   que cet arbitrage n'est pas écrit, la contre-recommandation 5 est appliquée par ce plan et démentie
   par le dépôt. Le grisage daté des modules restants est exécuté par L0.2 (V0), qui détient
   `ModuleCatalog.cs` et la coquille WPF pour la vague.
6. **Ne pas construire la file de rejeu hors ligne.** Le refus explicite de `SyncSupervisionService.cs`
   est le bon arbitrage. *Application :* D2 d'abord (L0.8 en V0, puis L2.4 en V2), puis C6 (L3.4) comme contrepartie
   assumée. Le mode dégradé viendra après, ou jamais.
7. **Ne pas remplacer le garde de readiness par un garde plus gros.** *Application :* L5.3 le réduit
   progressivement au rendu du tableau et déplace ses preuves dans des tests xUnit compilés — **avant**
   d'ouvrir D6, jamais après.
8. **Ne pas investir dans la sur-couverture de tests SQLite.** 795 tests sur 957 ne voient jamais
   PostgreSQL et 10 seulement tournent sur la vraie base. *Application :* aucun lot n'ajoute de test
   SQLite ; les gardes G4, G5, G9 et G17 sont écrits comme `[PostgresFact]`, et les parcours argent,
   stock et inventaire PMS basculent vers `postgres-integration` au fil des vagues.

### 8.2 Ce que ce plan écarte de lui-même

**Il n'écrit pas les règles de droit algérien — et il ouvre désormais le chantier qui les procure.**
Aucune règle n'a pu être vérifiée : ni le barème IRG et ses plancher/plafond d'abattement (B8), ni le
barème du droit de timbre (B4), ni la liste des champs obligatoires de la fiche de police (B2), ni les
taux de taxe de séjour par catégorie (B5), ni la forme en vigueur du G50 (B9). Elles ne sont pas dans le
dépôt. Les charges de ces cinq points supposent une règle d'une complexité ordinaire. **Le chantier
« textes et validation externe » ouvert en S1 (§4.2, action 9) en fait des dépendances de calendrier
datées, et non des hypothèses de chiffrage** : sans le texte à la date dite, la vague concernée ne
démarre pas. Il y ajoute une relecture du modèle de facture par un comptable ou un juriste **avant** que
L5.1a ne fige le gabarit.

**Il ne retient pas l'index unique sur `JournalEntryLineId`** proposé en parade de B7. Une ligne peut
légitimement porter **plusieurs** allocations (lettrage partiel successif) et l'index unique les
interdirait. La bonne parade, retenue dans L0.3a, est la **transaction Serializable avec re-validation**,
pas l'unicité. Un index unique reste posé sur le couple qui doit l'être, pas sur la ligne.

**Il ne parallélise pas A6b au-delà de trois lots**, et seulement dans l'issue « contrôle explicite » de
L1.0. Le découpage par famille de modules est propre jusqu'à trois ; au-delà, les périmètres
d'infrastructure commencent à se recouper et le coût d'intégration dépasse le gain. C'est la seule limite
de parallélisme de ce plan qui ne soit pas mesurée : elle est reprise de l'expérience du propriétaire,
pas d'un chiffre. Dans l'issue « filtre de requête global EF », elle tombe à un seul lot (§3.1).

**Il ne promet aucune date, et n'en publie plus aucune.** Le rendement de 14 jours-agent par semaine est
une **hypothèse de travail, pas une mesure**. La version antérieure de ce document affirmait qu'il était
« calibré sur l'étalon mesuré dans les quatre worktrees » : c'était faux, et la phrase est retirée. Les
quatre worktrees ont été écrits en quelques minutes d'agent chacun, mais **sans revue, sans build, sans
test d'intégration et sans fusion** — ils ne mesurent donc précisément aucune des quatre choses que
l'unité « jour-agent livré et intégré » prétend contenir ; deux d'entre eux sont d'ailleurs déclarés
incomplets par ce plan lui-même (§4.4). Un horodatage de fichier n'est pas un étalon de vélocité, et
l'ancien chiffre ne l'était pas davantage. Les durées du §2.2 sont donc **relatives et conditionnelles**,
jamais calendaires : aucune date de jalon pilote ne sera écrite avant que le cycle complet de V0 ait été
mesuré, une fois, sur le dépôt réel (§2.1).

**Il n'a été validé par aucune exécution.** Aucun `dotnet build`, aucun `dotnet test`, aucun
`check-module-readiness.ps1`, aucun `pg_dump`. Tous les critères de ce document sont formulés comme des
commandes à jouer, avec la valeur lue statiquement dans le dépôt en point de comparaison. Cette révision
a purgé les commandes dont la syntaxe était fautive — cinq `grep -E '…\|…'` qui, en expression régulière
étendue, cherchaient une barre verticale littérale et renvoyaient donc **toujours 0** ; un
`MapPost("/periods` qui matchait déjà la clôture de période et renvoyait donc **déjà 1** ; un
`\d accounting.invoices` interrogeant une table qui vit dans le schéma `finance` — mais **aucune n'a été
jouée**. La **règle 9** du §7.2 l'impose désormais avant toute publication d'une « valeur d'aujourd'hui ».
En particulier, l'affirmation selon laquelle `HasPendingModelChanges()` échouerait sur A6a et D2a est une
déduction solide — fondée sur `ApplyConfigurationsFromAssembly` et sur l'absence de fichier de
migration — mais elle n'est pas une observation. **Le premier geste de l'intégrateur, en semaine 1, doit
être d'ouvrir une PR de contrôle pour vérifier ces déductions.**

**Il ne tranche pas six questions qui appartiennent au propriétaire :** la stratégie d'application d'A6b
(§3.1, L1.0), la lecture de la dépendance B2 → A5 (§2.3), l'arbitrage sur la parité fonctionnelle avec le
produit legacy (§7.3), **le sort de `origin/feature/personalized-homepage` et du module POS** (§4.2,
action 1), **le moteur PDF et l'emplacement du rendu** (§3.5, L5.0), et **la fenêtre de bascule chez le
pilote** (§3.8). Les six sont signalées à l'endroit où elles bloquent, et quatre d'entre elles sont
désormais des **portes datées**, pas des réserves de bas de page.

---

*Document établi le 04/09/2026 sur `reorg/phase-1` @ `da196a7`, en lecture seule. Révisé après revue
adverse : chiffrage, exécutabilité des critères, périmètres de lots, angles blancs de mise en service.
Référentiel : `08-avis-etat-du-projet.md`. Gouvernance héritée de `07-plan-migration.md`.*
