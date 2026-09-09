# PostgreSQL

## Local database

A local PostgreSQL instance is declared in docker-compose.yml.

~~~bash
docker compose up -d postgres
~~~

Connection defaults for development:

| Setting | Value |
|---|---|
| Host | localhost |
| Port | 5432 |
| Database | raqmi_system |
| User | raqmi |

The password is a local development value only and must be replaced in production.

## Schemas

| Schema | Purpose |
|---|---|
| security | Users, roles and permissions |
| audit | Audit trail |
| organization | Hotel units |
| exploitation | Daily revenue and operational data |

## SQL scripts

| Script | Purpose |
|---|---|
| database/postgres/001_security_schema.sql | Creates schemas, security tables, hotel units and daily revenue tables |
| database/postgres/002_security_seed.sql | Inserts permissions, roles and role-permission mappings |
| database/postgres/003_organization_revenue_module.sql | Adds or updates hotel units and daily revenue structures, then seeds the first EGT units |

## Current business tables

| Table | Purpose |
|---|---|
| organization.hotel_units | Operational hotel units with code, name, type, display order and active state |
| exploitation.daily_revenues | One daily revenue entry per date and unit, with categories, workflow status and validation trace |

## Schema migrations (source of truth)

Starting with the `InitialSchema` migration, the database schema is owned by EF Core
migrations under `src/RaqmiSystem.Infrastructure/Persistence/Migrations/`, generated from
the current EF model (`RaqmiDbContext` and `Persistence/Configurations/`). The
`database/postgres/001` to `004` SQL scripts remain in the repository as historical record
of how the schema was originally built by hand, but they must **not** be run against a new
environment anymore — applying them alongside the EF migrations would fight over the same
tables. From now on, `dotnet ef database update` is the only supported way to provision or
upgrade the schema.

The repository uses a local, versioned `dotnet-ef` tool (see `.config/dotnet-tools.json`)
so the CLI version always matches `Microsoft.EntityFrameworkCore.Design` from
`Directory.Packages.props`. Restore it once per clone/CI run, then use `dotnet ef` as usual:

~~~bash
dotnet tool restore

dotnet ef database update \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj
~~~

To add a new migration after changing the EF model:

~~~bash
dotnet ef migrations add <MigrationName> \
  --project src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj \
  --startup-project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj \
  --output-dir Persistence/Migrations
~~~

## Rôle applicatif `raqmi_app`

L'API et, sur site, `pg_dump` (`deploy/onpremise/backup-raqmi.ps1`) se connectent avec le rôle
`raqmi_app`, créé par `deploy/postgres/create-app-role.sql` **après** `dotnet ef database update`.
Ce rôle n'est ni propriétaire ni superutilisateur : `USAGE` sur chaque schéma, `SELECT/INSERT/
UPDATE/DELETE` sur toutes les tables, `USAGE/SELECT` sur les séquences, `SELECT` sur
`public."__EFMigrationsHistory"`, et des `ALTER DEFAULT PRIVILEGES` pour que les tables des
migrations futures soient couvertes sans re-grant — à condition que ces migrations tournent sous le
même rôle administrateur que le script.

Les migrations créent aujourd'hui **20 schémas** : `accounting`, `approvals`, `audit`, `budgeting`,
`crm`, `exploitation`, `finance`, `fiscalite`, `housekeeping`, `hr`, `inventory`, `kitchen`, `kpi`,
`lodging`, `organization`, `purchasing`, `reporting`, `security`, `settings`, `tariffs`. Le script doit citer
**exactement** cet ensemble dans chacun de ses cinq blocs : un schéma migré mais non accordé rend ses
tables inaccessibles à l'API et **absentes de chaque sauvegarde** (`pg_dump` s'arrête sur la première
table interdite) ; un schéma accordé mais inexistant fait échouer le script lui-même (`ON_ERROR_STOP`).
`crm`, `housekeeping`, `hr` et `kpi` ont manqué de la première version du script — 25 tables sur 104
jamais sauvegardées, dont les 11 tables RH — pendant que `/health/database` répondait « healthy ».

Le test `PostgresSchemaGrantTests` (collection « Postgres », voir ci-dessous) compare le script à la base
migrée et rejoue ses `GRANT` sur un rôle jetable pour vérifier la lecture de chaque table : **toute
migration qui ajoute un schéma doit être livrée avec la mise à jour des cinq blocs du script**, sinon le
job `postgres-integration` est rouge. Sur une installation existante, rejouez le script (idempotent, le
mot de passe d'un rôle existant n'est pas modifié) pour accorder les schémas ajoutés.

## Tests sur PostgreSQL réel

La suite de tests (`dotnet test`) tourne sur SQLite et InMemory : rapide, sans dépendance, mais
aveugle à tout ce qui n'existe que chez le vrai fournisseur — les migrations Npgsql, les noms de
contraintes et d'index, l'isolation `Serializable` et ses erreurs de sérialisation (risque R10 du
dossier de réorganisation). Le dossier `tests/RaqmiSystem.Tests/Postgres/` porte les tests qui
exercent PostgreSQL lui-même. Ils forment la collection xUnit « Postgres » (exécutée en série) et
portent le trait `Category=Postgres`.

### Ce qui est couvert

| Test | Ce qu'il prouve |
|---|---|
| Migrations depuis une base vide | toutes les migrations du dépôt s'appliquent sur Npgsql et produisent le schéma attendu |
| **Garde anti-dérive** | `GetPendingMigrations()` est vide **et** `Database.HasPendingModelChanges()` est faux |
| Migration depuis N-1 | toutes les migrations sauf la dernière, puis la dernière (le chemin d'une installation en production) |
| Retour arrière | la dernière migration se retire (`Down`) puis se réapplique |
| Contraintes réelles | unicité de l'email (`security.users`), du numéro de facture émise (`finance.invoices`), du couple (date, unité) de `exploitation.daily_revenues` ; clé étrangère et contrainte `CHECK` de `accounting.journal_entry_lines` — chaque violation est provoquée et l'exception Npgsql (SQLSTATE, nom de contrainte) est vérifiée, telle que `DbUpdateExceptionExtensions` la lit |
| Concurrence | deux ventes simultanées de la dernière chambre d'un type via `LodgingService` : une seule aboutit, l'autre reçoit un conflit rejouable (transaction `Serializable` réelle) |
| **Couverture du rôle applicatif** | `PostgresSchemaGrantTests` : l'ensemble des schémas cités dans `deploy/postgres/create-app-role.sql` est **égal** à l'ensemble des schémas migrés, dans chacun des cinq blocs de `GRANT` ; et les `GRANT` du script, rejoués sur un rôle jetable, donnent `SELECT` sur chaque table — ce que `pg_dump` exige. Une migration qui ajoute un schéma sans compléter le script rend ce test rouge |

### La variable `RAQMI_TEST_POSTGRES`

C'est le seul interrupteur. Elle contient la chaîne de connexion Npgsql d'un rôle autorisé à
créer des bases (`CREATEDB`, ou superutilisateur). La base qu'elle nomme n'est qu'un point
d'entrée administratif : la fixture crée pour chaque exécution une base `raqmi_test_main_<suffixe>`
(plus des bases vierges `raqmi_test_nminus1_*` et `raqmi_test_rollback_*` pour les scénarios de
migration), y applique les migrations, et **les supprime en sortie, même en cas d'échec**. La base
de développement `raqmi_system` n'est jamais modifiée. Seuls l'hôte, le port, le rôle et le mot de
passe sont lus dans la variable ; la chaîne finale est composée par `ConnectionStringFactory`,
exactement comme pour l'API.

- **Variable absente** : les tests de la collection sont marqués *Skipped* avec un motif explicite.
  `dotnet test` reste vert sur un poste sans PostgreSQL ; rien n'est tenté.
- **Variable définie** : les tests s'exécutent contre le serveur indiqué.

### En local

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/run-postgres-tests.ps1
~~~

Le script démarre `docker compose up -d postgres` (identifiants du `docker-compose.yml`), attend
que le serveur accepte une connexion, définit `RAQMI_TEST_POSTGRES` et lance
`dotnet test --filter "Category=Postgres"`. Le conteneur reste en marche ensuite ; `-StopContainer`
l'arrête sans toucher à son volume. Si `RAQMI_TEST_POSTGRES` est déjà définie dans votre session,
le script ne démarre rien et utilise votre chaîne telle quelle.

### En CI

Le job `postgres-integration` de `.github/workflows/dotnet.yml` démarre un service
`postgres:16-alpine` (mêmes identifiants que le compose), attend son *healthcheck*, restaure les
outils `dotnet` du manifeste, construit le projet de tests et exécute la collection filtrée avec
`RAQMI_TEST_POSTGRES` pointant sur le service. Le job `build-core` (SQLite/InMemory) reste inchangé.

### Ce que le garde anti-dérive impose

Toute modification d'une entité ou d'une configuration EF (`IEntityTypeConfiguration<>`) qui
change le modèle relationnel — colonne, index, contrainte, type, longueur, relation — **doit être
livrée avec sa migration dans le même commit**. SQLite ne s'en aperçoit pas (`EnsureCreated` suit
toujours le modèle) ; PostgreSQL, si : le test `Le_modele_EF_ne_derive_pas_de_la_derniere_migration`
échoue en indiquant la commande `dotnet ef migrations add` à exécuter. Le commit est bloqué en CI
tant que le snapshot ne décrit pas le modèle. Une fois la migration ajoutée, les scénarios
« depuis zéro », « depuis N-1 » et « retour arrière » la valident automatiquement.
