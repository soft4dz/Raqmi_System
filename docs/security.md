# Security baseline

## Implemented foundation

The repository now contains the first security foundation:

- Users
- Roles
- Permissions
- Role-permission mapping
- User-role mapping
- Audit log
- JWT authentication
- PostgreSQL schema preparation
- Initial security seeding command

## Rules from day one

- No default administrator password is committed to the repository.
- No API key, database password or license secret should be committed.
- Production configuration must be injected by environment variables or a secure secret store.
- Passwords are hashed with PBKDF2-SHA256 and a per-password salt.
- JWT signing requires a key of at least 32 bytes.
- Every authentication attempt is written to the audit log.
- Permissions are checked server-side through JWT claims and authorization policies.

## Environment variables

Use double underscores for nested .NET configuration keys:

~~~bash
RAQMI_POSTGRES__HOST=localhost
RAQMI_POSTGRES__PORT=5432
RAQMI_POSTGRES__DATABASE=raqmi_system
RAQMI_POSTGRES__USER=raqmi
RAQMI_POSTGRES__PASSWORD=change-me

RAQMI_JWT__ISSUER=RaqmiSystem
RAQMI_JWT__AUDIENCE=RaqmiSystem.Client
RAQMI_JWT__SIGNINGKEY=replace-with-a-random-secret-of-at-least-32-bytes
RAQMI_JWT__ACCESSTOKENMINUTES=60
~~~

## Database preparation

Start PostgreSQL locally:

~~~bash
docker compose up -d postgres
~~~

Apply the security SQL scripts if you want to initialize the database manually:

~~~bash
psql -h localhost -U raqmi -d raqmi_system -f database/postgres/001_security_schema.sql
psql -h localhost -U raqmi -d raqmi_system -f database/postgres/002_security_seed.sql
~~~

## Initial administrator

The first administrator is optional and must be provided through environment variables:

~~~bash
RAQMI_INITIAL_ADMIN_EMAIL=admin@example.local
RAQMI_INITIAL_ADMIN_PASSWORD=replace-with-a-strong-temporary-password
~~~

Then run the seed command:

~~~bash
dotnet run --project src/RaqmiSystem.Api/RaqmiSystem.Api.csproj -- --seed-security
~~~

The created administrator is marked with MustChangePassword = true.

## Anti-lockout guards on user administration

An administration screen must never be able to put the installation into a state nobody can get it
out of. Three rules are enforced by `UserAdministrationService` - in the service, not in the user
interface, so no HTTP client can go around them. Each one is refused with `400 Bad Request` and an
explicit message:

1. A user cannot deactivate their own account.
2. A user cannot remove from their own roles the one that carries `users.write`.
3. The last ACTIVE holder of `users.write` can be neither deactivated nor stripped of it, by
   anyone. In other words, the installation always keeps at least one active account able to
   administer users.

Rule 3 is not theoretical: an access token is a permission snapshot taken at sign-in and is not
revoked when the account behind it is deactivated, so a just-deactivated administrator keeps a
usable token until it expires - and is exactly the caller able to close the door behind them.

Administrators never choose another person's password. Creating an account and resetting a password
both generate a CSPRNG temporary password, persist only its hash, flag the account
`MustChangePassword`, and return the secret exactly once in the HTTP response (there is no
email/SMTP infrastructure in this repository yet). It is never written to the audit log.

## API endpoints

| Endpoint | Protection |
|---|---|
| GET /health | Public |
| GET /health/database | Public health check |
| POST /api/v1/auth/login | Public login |
| GET /api/v1/me | Authenticated |
| GET /api/v1/security/permissions | users.read |
| GET /api/v1/security/roles | users.read |
| GET /api/v1/security/permission-migration-report | roles.read (ou admin.role.read) |
| GET /api/v1/security/users | users.read |
| GET /api/v1/security/users/{id} | users.read |
| POST /api/v1/security/users | users.write |
| PUT /api/v1/security/users/{id} | users.write |
| POST /api/v1/security/users/{id}/activate | users.write |
| POST /api/v1/security/users/{id}/deactivate | users.write |
| PUT /api/v1/security/users/{id}/roles | users.write |
| POST /api/v1/security/users/{id}/unlock | users.write |
| POST /api/v1/security/users/{id}/reset-password | users.write |
| POST /api/v1/auth/refresh | Public (valid refresh token required) |
| GET /api/v1/audit | audit.read |
| POST /api/v1/audit/purge | security.seed |
| GET /api/v1/revenue/sample-summary | revenue.read |

Les routes des domaines P0 (Finance, PMS, Achats/Stocks, RH) exigent depuis le lot 2.1 la clé cible
`domaine.ressource.action` correspondante ; la clé historique reste acceptée (voir ci-dessous).

## Modèle de permissions `domaine.ressource.action`

Lot 2.1 de la réorganisation fonctionnelle (`docs/reorganisation/07-plan-migration.md`, phase 2). La
table de correspondance est le code : `src/RaqmiSystem.Domain/Identity/PermissionRegistry.cs`.

### Principe

- Chaque clé cible s'écrit `préfixe.ressource.action` (`finance.entry.post`) et porte l'identifiant
  stable de son domaine fonctionnel (`"03"`), sa ressource, son action, une description et la liste des
  **clés historiques qui la couvrent**.
- Les 83 clés historiques restent dans `PermissionCatalog`, avec leur valeur et leur constante : le
  client WPF et le garde de readiness les référencent par nom. Le registre s'y ajoute (92 constantes
  nouvelles ; `hr.payroll.close`, déjà au format cible, est sa propre cible).
- Conventions d'action : `read` consulter ; `manage` créer, modifier, activer ou désactiver un
  référentiel ou un document en brouillon (le `write` historique) ; puis un verbe propre pour chaque
  acte qui engage l'établissement — `post`, `close`, `reverse`, `approve`, `issue`, `validate`,
  `execute`, `decide`, `reconcile`, `inspect`, `process`, `export`, `record`, `remind`, `overbook`,
  `override`, `move`, `change_rate`, `seed`, `admin`.
- Une politique d'autorisation par clé du catalogue, générée dans `Program.cs` à partir de
  `PermissionRegistry.AcceptedClaims` ; `SecurityContextExtensions.HasPermission` (leviers optionnels
  comme la surréservation) applique la même règle. Le JWT ne change pas : un claim `permission` par
  clé détenue, aucun claim dérivé.

### Règle d'équivalence

| Clé demandée par la route | Claims acceptés | Exemple |
|---|---|---|
| Clé cible | elle-même **ou** une clé historique qui la couvre | `finance.entry.post` ← `accounting.post` |
| Clé historique **1:1** (ne couvre qu'une cible) | exactement la politique de sa cible | `accounting.post` ↔ `finance.entry.post` |
| Clé historique **composite** (couvre plusieurs cibles) | elle-même seulement | `users.write` n'est satisfaite ni par `admin.user.create` seule ni par les trois clés fines |

Une clé historique vaut donc toutes les clés fines qu'elle couvre ; une clé fine seule ne vaut jamais
la clé composite. C'est ce qui interdit l'extension silencieuse : sur une route restée sur
`users.write` (le socle n'est pas dans le lot P0), un rôle qui ne détient que `admin.user.create` reçoit
403. Les huit alias PMS que `Program.cs` déclarait un par un (`lodging.reserve` acceptait
`lodging.write`, `lodging.checkout` acceptait `lodging.checkin`…) sont exprimés comme des couvertures
ordinaires — `lodging.write` et `lodging.reserve` couvrent tous deux `lodging.reservation.create` — avec
le même effet ; `lodging.change_rate`, `lodging.override_restriction` et `lodging.overbooking` n'héritent
toujours de rien.

### Mappings composites (1:n)

| Clé historique | Clés cibles couvertes |
|---|---|
| `users.write` | `admin.user.create`, `admin.user.update`, `admin.user.deactivate` |
| `accounting.write` | `finance.chart.manage`, `finance.entry.manage`, `finance.party.manage` |
| `treasury.write` | `finance.bank_account.manage`, `finance.receipt.manage`, `finance.payment_order.manage` |
| `lodging.write` | `lodging.reservation.create`, `lodging.reservation.cancel`, `lodging.reservation.noshow`, `lodging.room.manage`, `lodging.rate.manage`, `lodging.night_audit.execute` |
| `lodging.checkin` | `lodging.checkin.execute`, `lodging.checkout.execute`, `lodging.stay.move`, `lodging.folio.manage` |
| `inventory.write` | `inventory.item.manage`, `inventory.movement.record`, `inventory.count.manage` |
| `purchasing.write` | `purchasing.supplier.manage`, `purchasing.order.manage` |
| `hr.write` | `hr.employee.manage`, `hr.time.manage` |

### Alias 1:1

| Préfixe historique | Clés cibles (domaine) |
|---|---|
| `users.read`, `roles.*`, `security.seed`, `units.*`, `settings.*` | `admin.user.read`, `admin.role.read`, `admin.role.update`, `admin.security.seed`, `admin.unit.read`, `admin.unit.manage`, `admin.settings.read`, `admin.settings.update` (02) |
| `revenue.*` | `finance.revenue.read`, `finance.revenue.record`, `finance.revenue.validate` (03) |
| `treasury.read`, `treasury.approve` | `finance.treasury.read`, `finance.payment_order.approve` (03) |
| `accounting.read/post/reverse/reconcile/close/admin` | `finance.accounting.read`, `finance.entry.post`, `finance.entry.reverse`, `finance.party.reconcile`, `finance.period.close`, `finance.accounting.admin` (03) |
| `budget.*`, `receivables.*` | `finance.budget.read`, `finance.budget.manage`, `finance.budget.approve`, `finance.receivable.read`, `finance.receivable.remind` (03) |
| `customers.*`, `crm.*` | `crm.customer.read`, `crm.customer.manage`, `crm.guest.read`, `crm.guest.manage`, `crm.loyalty.post` (04) |
| `invoices.*` | `billing.invoice.read`, `billing.invoice.manage`, `billing.invoice.issue` (05) |
| `lodging.read`, clés fines PMS, `closing.*` | `lodging.front_office.read`, `lodging.reservation.create/cancel/noshow/overbook`, `lodging.room.manage`, `lodging.rate.manage`, `lodging.night_audit.execute`, `lodging.checkout.execute`, `lodging.stay.move`, `lodging.stay.change_rate`, `lodging.restriction.override`, `lodging.closing.read/close/reopen` (06) |
| `tariffs.*` | `revenue.rate.read`, `revenue.rate.manage` (07) |
| `housekeeping.*` | `housekeeping.task.read`, `housekeeping.task.manage`, `housekeeping.room.inspect` (08) |
| `mice.*` | `mice.event.read`, `mice.event.manage` (09) |
| `kitchen.*` | `fnb.kitchen.read`, `fnb.kitchen.manage` (10) |
| `inventory.read`, `inventory.validate` | `inventory.stock.read`, `inventory.count.validate` (11) |
| `purchasing.read/approve/receive` | `purchasing.order.read`, `purchasing.order.approve`, `purchasing.receipt.execute` (12) |
| `hr.read`, `hr.payroll`, `hr.payroll.close` | `hr.employee.read`, `hr.payroll.process`, `hr.payroll.close` (13) |
| `approvals.*` | `workflow.request.read`, `workflow.request.decide` (01), `workflow.circuit.manage` (02) |
| `dashboard.read`, `reports.*`, `kpi.admin` | `pilotage.dashboard.read`, `pilotage.report.execute`, `pilotage.report.export`, `pilotage.kpi.admin` (20) |
| `audit.read`, `maintenance.*`, `sync.read` | `audit.log.read`, `system.backup.read`, `system.backup.execute`, `system.workstation.read` (22) |

### Rôles système

`SecuritySeeder` accorde à chaque rôle système une clé cible **si et seulement si** l'une de ses clés
historiques la couvre — équivalence stricte, aucune extension, et les clés historiques restent
accordées. Les clés cibles sont des lignes de `security.permissions` insérées par le seeder sur une base
déjà en service (aucune migration de schéma) ; un second passage ne change rien. La règle
`RoleCatalog.ApprovalDeciderRoles` tient sur `approvals.decide` et sur `workflow.request.decide`.

### Rôles personnalisés : rapport puis migration

Le seeder ne touche jamais un rôle personnalisé (risque R02). Procédure :

1. `GET /api/v1/security/permission-migration-report` (`roles.read`) : pour chaque rôle non système,
   les clés historiques détenues, les clés cibles déjà détenues et les **clés cibles manquantes**
   (strictement celles que ses clés historiques couvrent).
2. Accorder les clés manquantes au rôle, sans retirer les clés historiques.
3. Vérifier `IsMigrated = true` dans le rapport.

Ne pas retirer les clés historiques d'un rôle personnalisé tant que le client WPF évalue
`PermissionCatalog.<ConstanteHistorique>` pour ses boutons (`HasPermission`) : le serveur accepterait la
clé cible, l'écran resterait en lecture seule. Le garde anti-verrouillage de l'administration des
utilisateurs raisonne aussi sur `users.write` ; il suivra le retag du socle.

### Règle de retrait

Une clé historique ne peut être retirée du catalogue, des politiques et des rôles qu'après **une version
de compatibilité** complète pendant laquelle elle était marquée obsolète, avec une **télémétrie d'usage
nulle** sur cette version (aucun JWT émis ne la portait, aucune route ne l'exigeait, aucun rôle
personnalisé ne la détenait d'après le rapport). Le retrait est un lot dédié qui met à jour le registre,
le client WPF, le garde de readiness et cette page. Les tests `RbacPermissionRegistryTests`,
`RbacPolicyMatrixTests`, `SecuritySeederTests` et `PermissionCatalogTests` fixent l'état courant.

## Next security tasks

- Retaguer les domaines hors P0 (socle, CRM, MICE, Housekeeping, F&B, Pilotage, Système) vers les clés
  cibles, puis faire évaluer les clés cibles par le client WPF.
