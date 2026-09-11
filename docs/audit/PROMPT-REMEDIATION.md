# Prompt de remédiation — à coller dans une autre session Claude

Ce fichier contient un prompt prêt à l'emploi. **Copier tout le bloc ci-dessous** (entre les deux
lignes de séparation) et le coller comme premier message d'une session Claude Code ouverte sur le
dépôt `Raqmi_System`.

Trois variantes sont proposées : le **prompt complet** (§ 1), un **prompt par lot** si l'on
préfère avancer chantier par chantier (§ 2), et un **prompt de reprise** pour les sessions
suivantes (§ 3).

---

## 1. Prompt complet

```text
Tu travailles sur Raqmi System, un ERP hôtelier et de gestion pour le marché algérien.

PILE TECHNIQUE
- Backend : ASP.NET Core sur .NET 10, API minimale (469 routes sous /api/v1)
- Données : PostgreSQL 16, EF Core, 110 DbSet, 22 schémas, 57 migrations
- Client : WPF Windows (net10.0-windows), 71 000 lignes, sans MVVM (code-behind)
- Tests : xUnit, 1 092 tests, plus un job CI dédié sur du vrai PostgreSQL
- Solution : RaqmiSystem.sln — Domain, Application, Infrastructure, Api, Desktop, Tests

TA MISSION
Exécuter le plan de remédiation issu d'un audit externe. Il est dans le dépôt :

    docs/audit/04-plan-de-remediation.md

Commence par lire ces quatre documents, dans cet ordre :
    docs/audit/README.md                  (méthode et limites de l'audit)
    docs/audit/04-plan-de-remediation.md  (LE plan — 18 chantiers)
    docs/audit/01-avis-technique-global.md
    docs/audit/03-avis-ui-ux-design.md

Puis lis la charte UI, qui est la loi du projet pour tout ce qui touche au client WPF :
    docs/charte-ui-desktop.md

AVANT TOUTE MODIFICATION
1. Lance `dotnet build RaqmiSystem.sln -c Release` et `dotnet test RaqmiSystem.sln`.
   Note le point de départ exact : nombre d'avertissements, tests passants, tests échouants.
   L'audit n'a PAS pu compiler le projet — tous ses chiffres de volume sont fiables, mais rien
   dans l'audit ne prouve que la base est verte aujourd'hui. C'est à toi de l'établir.
2. Si quelque chose est déjà cassé, dis-le avant de commencer, et ne l'attribue pas à ton travail.
3. Crée une branche dédiée. Ne travaille jamais directement sur main.

ORDRE D'EXÉCUTION — non négociable
Lot A (gardes) → Lot B (déblocage) → Lot C (ergonomie) → Lot D (robustesse)

Le lot A passe en premier parce que ce sont les gardes automatiques qui empêchent les corrections
suivantes de se redégrader. Corriger les 7 couleurs en dur sans poser d'abord le test qui les
interdit, c'est les recorriger dans trois semaines.

RÈGLES DE TRAVAIL
- Un chantier = un commit. Le message référence l'identifiant : « fix(ui): A-03 … ».
- Après chaque chantier : build + tests. Tu ne passes au suivant que sur du vert.
- Tu ne modifies jamais un test pour le faire passer. Si un test échoue, soit le code est faux,
  soit le test l'est — tu tranches en le disant, tu ne le supprimes pas.
- Tu respectes les conventions du dépôt : commentaires de code SANS accents, libellés visibles
  en français accentué, aucune couleur ni taille de police en dur dans les vues (tokens du thème
  uniquement), documentation à jour quand tu changes une règle.
- Tout nouveau token de couleur reçoit une valeur dans LES DEUX palettes (RaqmiTheme.xaml ET
  ThemePalette.Sombre) et son contraste est vérifié : 4,5:1 pour du texte, 3:1 pour un trait.
- Tu ne réécris rien qui fonctionne. Pas de migration vers MVVM, pas de refonte d'architecture,
  pas de changement de bibliothèque. Le plan ne demande aucune réécriture.
- Tu n'ajoutes aucun module fonctionnel nouveau. Le dépôt est en gel fonctionnel
  (docs/stabilization/module-readiness.md).

CE QUE TU NE DÉCIDES PAS
Le lot P du plan (P-01 conformité hôtelière, P-02 arabe/i18n, P-03 client web, P-04 modules
planifiés) contient des DÉCISIONS PRODUIT, pas des chantiers techniques. Tu ne les tranches pas.
Tu les documentes, tu poses la question au propriétaire, et tu attends.

ATTENTION PARTICULIÈRE SUR A-07
Le garde `Module readiness gate` est DÉJÀ ROUGE sur main, avant toute modification de ta part :
le module Fiscalité est déclaré disponible sans test, sans documentation et sans fiche de preuves.
Ne le fais jamais passer au vert en ajoutant l'écran à `documentationGrace` dans
tools/readiness/screens.json. Les tests d'abord, la fiche de documentation ensuite, la fiche de
preuves en dernier.

ATTENTION PARTICULIÈRE SUR A-04
Passer TreatWarningsAsErrors à true va probablement révéler un volume important d'avertissements
sur 206 000 lignes générées. Corrige-les réellement. Ne mets un NoWarn que fichier par fichier,
avec un commentaire qui justifie pourquoi. Jamais de suppression globale. Si le volume est tel que
le chantier dépasse la journée, arrête-toi, rapporte le nombre et la nature des avertissements, et
demande comment procéder.

CE QUE JE VEUX EN RETOUR, À CHAQUE ÉTAPE
- Ce que tu as changé, et pourquoi
- La preuve que ça marche (sortie de build et de tests, pas une affirmation)
- Ce que tu as trouvé en chemin et qui n'était pas au plan
- Ce qui reste

Commence maintenant : lis les documents, établis le point de départ par un build et un test, et
rapporte-moi l'état réel avant de toucher quoi que ce soit.
```

---

## 2. Prompt par lot

Si l'on préfère une session par lot, remplacer le bloc « ORDRE D'EXÉCUTION » par :

```text
PÉRIMÈTRE DE CETTE SESSION
Tu ne traites QUE le lot A du plan (chantiers A-01 à A-07). Tu ne commences aucun chantier des
lots B, C ou D, même si tu en as le temps. À la fin, tu rapportes l'état et tu t'arrêtes.
```

*(remplacer « lot A » et « A-01 à A-06 » par le lot voulu)*

Les six lots et leurs chantiers :

| Lot | Chantiers | Effort indicatif |
|---|---|---:|
| **A** — gardes et fondations | A-01 à A-07 | ≈ 4 j |
| **B** — déblocage | B-01 à B-03 | ≈ 3 j |
| **C** — ergonomie | C-01 à C-03 | ≈ 5 j |
| **D** — robustesse | D-01, D-02 | ≈ 5 j |
| **P** — décisions produit | P-01 à P-04 | arbitrage, pas de code |

---

## 3. Prompt de reprise (sessions suivantes)

```text
Tu reprends l'exécution du plan de remédiation de Raqmi System.

Lis d'abord :
    docs/audit/04-plan-de-remediation.md   (le plan)
    docs/charte-ui-desktop.md              (la loi du projet côté UI)

Puis établis l'état réel :
- `git log --oneline -20` pour voir ce qui a déjà été fait
- `dotnet build RaqmiSystem.sln -c Release` et `dotnet test RaqmiSystem.sln`

Dis-moi quels chantiers sont déjà faits, lesquels restent, et si la base est verte. Ne touche à
rien avant que je te dise par où continuer.

Les mêmes règles qu'avant s'appliquent : un chantier = un commit, build + tests verts entre
chaque, aucune réécriture, aucun nouveau module, et les décisions du lot P ne se tranchent pas
sans moi.
```

---

## 4. Notes d'usage

- **Le prompt suppose que la session a accès au dépôt.** En local, ouvrir Claude Code dans le
  dossier du dépôt. Sur claude.ai/code, sélectionner le dépôt `soft4dz/Raqmi_System`.
- **Le prompt suppose que le SDK .NET 10 est installé.** C'est ce qui manquait à l'audit : sans
  lui, aucun chantier de ce plan ne peut être vérifié.
- **Le client WPF ne se compile que sous Windows.** Les chantiers A-01, A-02, A-03, B-02, C-01,
  C-02 et C-03 touchent au Desktop : ils demandent une machine Windows, ou au minimum le job CI
  `build-desktop`.
- Si une session part dans une direction non prévue au plan, la phrase qui recadre le mieux est :
  *« relis docs/audit/04-plan-de-remediation.md, dis-moi quel identifiant de chantier tu es en
  train de traiter, et si la réponse n'est aucun, arrête-toi. »*
