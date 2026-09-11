# 01 — Avis technique global

**Révision analysée :** `b412b8c` (11 septembre 2026)
**Méthode :** analyse statique uniquement — aucun build, aucun test, aucune exécution (voir
[`README.md`](./README.md)).

---

## 1. Le projet en chiffres

| Mesure | Valeur |
|---|---:|
| Commits | 90, du 30 août au 9 septembre 2026, **un seul auteur** |
| Lignes totales (`src` + `tests`) | 389 157 |
| dont migrations EF **générées** (57 fichiers) | ≈ 183 000 |
| **Code écrit à la main** | **≈ 206 000 lignes en 11 jours** |
| `Domain` | 23 493 |
| `Application` | 17 731 |
| `Infrastructure` (hors migrations) | 46 607 |
| `Api` | 8 390 |
| `Desktop` (WPF) | **71 214** |
| `Tests` | 38 782 |
| Fichiers `Domain` | 218 |
| `DbSet` / configurations EF | 110 / 104 |
| Contraintes `CHECK` | 175 |
| Schémas PostgreSQL | 22 |
| Routes HTTP | **469** |
| Routes portant `RequireAuthorization` | **469 (100 %)** |
| Tests (`[Fact]` + `[Theory]`) | **1 092** |
| `TODO` / `FIXME` / `NotImplementedException` | **0** |
| Modules déclarés | **50** — 32 disponibles, 18 planifiés |

**Le fait qui commande tout le reste :** 206 000 lignes écrites à la main en 11 jours par une
personne, soit ≈ 17 000 lignes par jour. Ce code est produit par assistance IA à cadence
industrielle. Il est de bonne facture — la section 2 le montre — mais **il n'a pas été relu
ligne à ligne par un humain, et cela ne peut pas l'avoir été.** La dette de ce projet n'est pas
une dette de code : c'est une dette de *vérification*.

---

## 2. Ce qui est solide

### 2.1 Le domaine est riche, pas anémique

`DailyRevenue` (`src/RaqmiSystem.Domain/Revenue/DailyRevenue.cs`) porte ses invariants lui-même :
constructeur privé pour EF, propriétés à setter privé, workflow Brouillon → Soumise → Validée /
Rejetée avec exception sur transition illégale, refus des lignes en double, projection des quatre
colonnes historiques recalculée dans **un seul** endroit qui les écrit. C'est du DDD correct.

### 2.2 La sécurité est traitée sérieusement

- **PBKDF2-SHA256, 310 000 itérations** (`Pbkdf2PasswordHasher.cs`).
- Protection explicite contre l'oracle temporel : le chemin « compte inexistant » exécute
  quand même un hash complet (`AuthenticationService.cs:22`, `AccountService.cs:52`).
- Verrouillage de compte 15 minutes (`User.cs:138`).
- Refresh tokens de 14 jours stockés en base et révocables.
- **100 % des 469 routes** portent une politique d'autorisation.
- Registre de permissions avec couvertures historiques, pour ne pas fermer du jour au lendemain
  un écran chez une installation déjà en service (`Program.cs`, `PermissionRegistry`).
- Périmètre utilisateur ↔ unité posé en filtre sur le **groupe entier** `/api/v1`
  (`UnitScopeEndpointFilter`), donc une route ajoutée demain est couverte sans y penser.

### 2.3 Les données sont traitées sérieusement

175 contraintes `CHECK`, colonnes monétaires en `numeric(p,s)`, colonnes temporelles en
`timestamptz`, 22 schémas séparés, rôle applicatif à moindre privilège
(`deploy/postgres/create-app-role.sql`), 1 320 appels `HasColumnName` qui découplent le nom C# du
nom SQL.

### 2.4 Le gate PostgreSQL en CI est la bonne décision

La suite principale tourne sur SQLite/InMemory et ne verrait ni les contraintes réelles, ni
l'isolation, ni les migrations Npgsql. Un job dédié (`.github/workflows/dotnet.yml`,
`postgres-integration`) rejoue les migrations **depuis zéro et depuis N-1** contre un conteneur
PostgreSQL 16, avec tests de contraintes, de GRANT et de concurrence anti-survente.

### 2.5 L'exploitation existe

Sauvegarde (`deploy/backup/pg-backup.sh` + timer systemd), Dockerfile multi-étages avec
utilisateur non-root et `HEALTHCHECK`, Caddy en terminaison TLS, logs Serilog en JSON compact sur
stdout, installeur Inno Setup pour le poste client.

### 2.6 La documentation est au-dessus de la moyenne du marché

137 fichiers markdown, dont des avis d'architecte qui se corrigent eux-mêmes en annexe
(`docs/reorganisation/08`, `10`, `11`). C'est un actif réel — avec une réserve, section 4.4.

---

## 3. Ce qui ne va pas, par ordre de gravité

### F-01 — Le client WPF est le passif principal

| Mesure | Valeur |
|---|---:|
| Lignes de WPF | 71 214 (34 % du code écrit à la main) |
| ViewModels | **0** |
| Gestionnaires d'événements en code-behind | **396** |
| Fichiers implémentant `INotifyPropertyChanged` | 9 |
| Tests couvrant le projet Desktop | **0** — il n'est référencé par aucun projet de test |
| Fichiers `.resx` | **0** |

`LodgingView.xaml.cs` fait 2 061 lignes, `CrmView.xaml.cs` 1 946. Toute règle métier qui vit dans
un `private async void Button_Click` est invérifiable, non réutilisable par un futur client web ou
mobile, et invisible pour l'assistance IA qui ne peut pas la tester.

`MainWindow.xaml` porte **32 `TabItem`**, relus **par position** (`TabIndex`) par un garde CI :
retirer un écran renumérote tout le catalogue.

### F-02 — Rien n'a jamais tourné devant un utilisateur réel

1 092 tests, mais **aucun test E2E**, **aucun smoke test WPF**, et aucun client identifié.
`docs/stabilization/module-readiness.md` l'énonce honnêtement : **aucun écran n'est
Production Ready**, par construction. Les 1 092 tests prouvent que le code fait ce que le code a
dit qu'il ferait — pas qu'un réceptionniste peut faire un check-in.

### F-03 — Aucune spécification OpenAPI

469 routes, **zéro** génération de spécification (ni `Microsoft.AspNetCore.OpenApi`, ni
Swashbuckle). Conséquences : pas de second client possible sans réécrire à la main un client HTTP,
pas d'intégration tierce, pas de contrat testable, pas de documentation d'API. **C'est le gap le
moins cher à combler et celui qui débloque le plus.**

### F-04 — 253 matérialisations non bornées

`ToArrayAsync` / `ToListAsync` apparaissent **253 fois** dans `Infrastructure`, `Take()`
**13 fois**. L'audit est correctement paginé (`AuditEndpoints.cs`), mais pas les listes de
réservations, de mouvements de stock, d'écritures comptables ni de folios. Invisible sur un hôtel
pendant six mois ; inévitable sur un groupe multi-sites à trois ans.

### F-05 — Verrouillage de compte sans limitation de débit

Il y a un lockout après N échecs, mais **aucun rate limiting par IP** (`AddRateLimiter` absent de
`Program.cs`). Quelqu'un qui connaît les identifiants de connexion peut verrouiller toute la
réception en boucle depuis l'extérieur. Correctif : ≈ 20 lignes.

### F-06 — Aucune internationalisation, et c'est un sujet légal

**0 fichier `.resx`**, tous les libellés en dur dans le XAML, aucun `FlowDirection` RTL. Or le
produit livre un module Fiscalité DGI/SIFEC et une facture PDF. En Algérie, les pièces
commerciales sont attendues en arabe ou en bilingue. Voir aussi
[`03-avis-ui-ux-design.md`](./03-avis-ui-ux-design.md) § 3.6 : la marque elle-même est bilingue.

### F-07 — Le noyau lit les paquets métier

`KpiFactLoader.cs` importe `Domain.Lodging` et `Domain.Housekeeping` ; `Crm` lit `Lodging`.
**Désactiver l'hôtel casse aujourd'hui la bibliothèque KPI et le cockpit DEC.** Tant que ce n'est
pas inversé, « ERP généraliste » veut dire « menu masqué ». Ce point était déjà mesuré au
document 11 ; il est toujours vrai à `b412b8c`.

### F-08 — Réglages de compilation inadaptés à un produit de gestion

- `<LangVersion>preview</LangVersion>` dans `Directory.Build.props`. Sur un ERP comptable destiné
  à tourner dix ans chez un client, une fonctionnalité de langage retirée casse le build sans
  qu'une ligne ait été touchée.
- `<TreatWarningsAsErrors>false</TreatWarningsAsErrors>`. Avec 206 000 lignes générées, les
  avertissements du compilateur sont la seule relecture automatique disponible.
- `"AllowedHosts": "*"` en production.

### F-10 — Le module Fiscalité est déclaré disponible sans aucune preuve

**Découvert après coup, par le garde de readiness du dépôt lui-même** — pas par cet audit, qui
avait classé le module 5.4 « disponible » sans vérifier ses preuves (correction consignée au
document 02).

Le module **Fiscalité DGI & SIFEC** (`437af45`, 9 septembre) est déclaré `Disponible` dans
`ModuleCatalog.cs` et sert **21 routes**, dont le calcul des déclarations de TVA et l'**export
G50**. Or :

| Preuve exigée par le modèle de readiness | État |
|---|---|
| Domain / Application / Infrastructure | ✅ présents |
| API (`FiscaliteEndpoints.cs`) | ✅ 21 routes |
| Migration PostgreSQL | ✅ `20260905214542_FiscaliteModule.cs` |
| RBAC (`finance.fiscal.read`, `.declare`, `.sifec.manage`) | ✅ présent |
| Desktop (`FiscaliteView.xaml`) | ✅ présent |
| **Tests** | ❌ **aucun fichier de test dédié** — `VatRegisterService` n'apparaît que comme dépendance construite par les tests d'achats et de vente |
| **Documentation** (`docs/modules/*.md`) | ❌ **aucune fiche** |
| **Fiche de preuves** (`tools/readiness/screens.json`) | ❌ **absente** |

Le garde `tools/check-module-readiness.ps1` échoue donc sur `main` :

```
ECHEC: screens.json: l'onglet Disponible 'FiscaliteTabItem' (ordres 5.4) n'a pas de fiche de preuves.
```

**Pourquoi personne ne l'a vu.** Le workflow `stabilization.yml` ne se déclenche que sur
`pull_request` et sur les poussées vers `stabilization/**` et `reorg/**`. Le lot Fiscalité a
rejoint `main` sans passer par une PR : le garde n'a jamais tourné dessus. Il a échoué à la
première PR ouverte depuis — celle de cet audit.

**Deux conclusions.** D'abord, le garde de readiness fait exactement son travail et mérite d'être
étendu à `push: main`. Ensuite, un module qui calcule des déclarations fiscales et produit un
export G50 **sans un seul test** est le risque le plus concret de tout ce document : une erreur y
est découverte par l'administration, pas par le client. Chantier **A-07** du plan 04.

---

### F-09 — Code parqué hors solution

`staging/wave-e2/` et `staging/wave-hr/` : ≈ 500 lignes non compilées, non testées, non
référencées. Soit intégrées, soit supprimées.

---

## 4. Notation par dimension

| Dimension | Note | Commentaire |
|---|:---:|---|
| Modèle de domaine | **A−** | Riche, invariants portés par les entités |
| Modèle de données / PostgreSQL | **A** | 175 CHECK, `numeric`, `timestamptz`, 22 schémas, moindre privilège |
| Sécurité applicative | **A−** | PBKDF2 310k, 100 % des routes autorisées ; manque le rate limiting |
| Architecture en couches | **B** | La logique métier vit dans `Infrastructure` (46k) et non dans `Application` (17k) : l'inversion de dépendance est nominale |
| Tests | **B+** | 1 092 tests + gate PostgreSQL réel ; **0 test UI, 0 E2E** |
| Client desktop | **D** | 71k lignes, 0 ViewModel, 0 test, Windows seul, 0 i18n |
| API | **B−** | Discipline d'autorisation exemplaire ; pas d'OpenAPI, pagination partielle |
| Exploitation / déploiement | **B** | Tout est là, rien n'a été éprouvé |
| Conformité algérienne | **C+** | Fiscalité et SCF livrés ; aucune homologation, aucune relecture d'expert-comptable, pas d'arabe |
| Documentation | **A+** | Au-dessus du marché (voir réserve 4.4) |
| Processus de développement | **C** | Cadence ×50 sur la relecture humaine, sans garde-fou de régression |

### 4.4 La réserve sur la documentation

≈ 137 documents, dont plusieurs avis d'architecte de 30 Ko qui se contredisent et s'auto-corrigent.
À un moment, **produire le plan devient un substitut à exécuter le plan**. Les documents 08, 10 et
11 disent tous la même chose sous trois angles : *aucun client n'est identifié*. Aucun document
supplémentaire ne changera cela — celui-ci pas davantage.

---

## 5. Le risque principal n'est pas technique

- **469 routes ne se maintiennent pas seul.** Pas par incompétence : par arithmétique. La charge
  de support, d'évolution fiscale, de migration et de correction dépasse une personne dès le
  deuxième client.
- **Aucun client n'est identifié.** 206 000 lignes bâties sur une hypothèse non validée.
- **38 % du catalogue est vide** (18 modules planifiés sur 50) : c'est le seul endroit où le
  produit contredit son vendeur en démonstration.

Le conseil unique, s'il ne devait en rester qu'un, n'est pas technique : **obtenir un client
pilote, même gratuit, même partiel — un seul hôtel de trente chambres.** Un mois d'usage réel
réordonnera la moitié des priorités ci-dessus.

---

## 6. Le choix du langage de programmation

### 6.1 Recommandation

**Conserver C# / .NET pour le noyau. Ne pas changer.** Non par coût d'abandon : parce que pour un
ERP, c'est objectivement l'un des deux meilleurs choix disponibles.

**La vraie question de langage n'est pas le backend — elle est côté client**, et la réponse y est
**TypeScript / React pour un client web**, pas WPF.

### 6.2 Pourquoi C# est le bon choix pour le noyau

1. **`decimal` natif, exact, 128 bits.** Décisif pour un ERP comptable. Python et JavaScript n'ont
   pas de décimal natif performant ; les 111 colonnes monétaires en `numeric(p,s)` mappent
   directement.
2. **Typage statique + `Nullable` activé.** Seule barrière disponible, en solo, contre la
   régression silencieuse sur 469 routes et 218 entités.
3. **EF Core + Npgsql mature** : migrations versionnées, contraintes, concurrence optimiste — tout
   ce dont le gate PostgreSQL dépend.
4. **LINQ** sur un modèle à 110 tables : avantage de productivité qu'aucun autre langage ne
   réplique exactement.
5. **Un seul langage** backend + client + tests + scripts. En solo, c'est décisif.
6. Gratuit, multiplateforme, LTS 3 ans, performances largement suffisantes.

### 6.3 Les alternatives, examinées

| Langage | Verdict |
|---|---|
| **Java / Spring Boot** | Seul vrai équivalent. Écosystème ERP plus riche, vivier de développeurs supérieur. **Aucun gain qui justifie de réécrire 206 000 lignes.** À la création du projet : pile ou face avec C#. |
| **TypeScript full-stack** (Node + Prisma/Drizzle) | Excellent pour le front, **inadapté au noyau** : pas de décimal natif, invariants et transactions plus faibles, encapsulation du domaine intenable sur 218 entités. |
| **Python / Django** | Typage insuffisant à ce volume en solo. Et surtout : vouloir du Python signifie en réalité **partir d'Odoo** — décision produit, pas décision de langage. |
| **Go** | Bon pour des services, mauvais pour un domaine métier riche : pas d'ORM sérieux, verbosité intenable sur 110 entités. |
| **Rust** | Coût de développement ×2 à ×3 pour un bénéfice nul sur cette charge. |
| **PHP / Laravel** | Défendable (rapidité, hébergement trivial, très large vivier en Algérie), mais rigueur comptable et sûreté de refactoring nettement inférieures. |

### 6.4 La couche cliente : le seul choix technique à corriger

WPF pose quatre problèmes structurels :

1. **Windows uniquement**, alors qu'un directeur d'hôtel veut son tableau de bord sur téléphone ;
2. **multi-sites = déploiement d'un `.exe`** sur N postes, à chaque mise à jour ;
3. **code-behind non testable** et non réutilisable ;
4. écosystème en déclin, et — argument de premier ordre à cette cadence de développement — **les
   modèles d'IA sont nettement plus performants en React/TypeScript qu'en WPF**.

**Séquence recommandée :**

1. **Ne pas réécrire le WPF.** 71 000 lignes ; il reste le client des postes d'exploitation
   (réception, caisse), là où il est bon.
2. **Geler sa croissance** : aucun nouvel écran en WPF.
3. **Générer l'OpenAPI** (chantier C-03 du plan 04) — prérequis de tout le reste.
4. **Sortir les nouveaux écrans en web (React + TypeScript)**, en commençant par ceux qui ont le
   plus besoin de mobilité : pilotage direction, KPI, validation des recettes, approbations. Ce
   sont aussi les plus simples et les plus démonstratifs en rendez-vous commercial.
5. Migrer ensuite écran par écran, sans jamais casser l'existant.

**Alternative si le mono-langage est non négociable : Blazor.** C# partout, réutilisation directe
des DTO `Application`, un seul modèle mental. Choix défendable en solo. Il n'est pas recommandé en
premier parce que l'écosystème de composants est plus pauvre, le recrutement plus difficile et
l'assistance IA sensiblement moins bonne.

---

## 7. Synthèse en une phrase

Un socle technique de qualité supérieure à ce que produisent la plupart des éditeurs, bâti seul et
en onze jours ; le langage est le bon et ne doit pas changer ; ce qui manque n'est pas du code,
c'est **un utilisateur, un client web, et la preuve que ce qui est écrit fonctionne ailleurs que
dans ses propres tests**.
