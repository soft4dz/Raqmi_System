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
| GET /api/v1/security/users/{id}/units | users.read |
| PUT /api/v1/security/users/{id}/units | users.write |
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

## Périmètre utilisateur ↔ unité

Lot **A6a** de la réorganisation (`docs/reorganisation/10-plan-comprime.md`) : modèle, claims, filtre
d'API, administration et rôle `reception`. Le contrôle **dans chaque service métier** est le lot A6b, qui
n'est pas livré ; les deux limites du filtre décrites plus bas en découlent.

### Modèle

Table `security.user_unit_assignments` (`UserUnitAssignment`) : un utilisateur, un code d'unité (clé
étrangère vers `organization.hotel_units.code`, suppression de l'unité refusée), une validité optionnelle
(`valid_from`, `valid_to` exclu), qui a affecté et quand. Unicité (utilisateur, unité) tenue par un index
unique. L'entité et sa configuration EF sont livrées ; le `DbSet` et la migration correspondante sont à
générer à l'intégration (les tests construisent le schéma par `EnsureCreated`).

Sémantique, volontairement asymétrique pour rester compatible avec l'existant :

- **aucune affectation = périmètre global**. Les rôles système et tous les comptes créés avant ce lot
  n'ont aucune ligne et voient tout, exactement comme avant. Le périmètre se restreint par un acte
  d'administration explicite et audité, jamais par défaut ;
- **au moins une affectation = périmètre restreint** aux affectations en validité à l'instant où le jeton
  est émis. Un compte dont toutes les affectations sont expirées n'a accès à **aucune** unité : une
  restriction qui expire ne redevient jamais globale par accident.

### Jeton

`IUnitScope { IsGlobal, AllowedUnitCodes, Allows(code) }` est photographié dans le JWT à la connexion et
**relu en base à chaque `POST /api/v1/auth/refresh`** : une affectation retirée disparaît du jeton
suivant, comme un rôle retiré en fait disparaître les permissions. Le jeton ne porte que des codes.

| Périmètre | Claims portés |
|---|---|
| global | `scope=global` |
| restreint | un claim `unit=<CODE>` par unité autorisée (majuscules) ; jamais de claim `scope` |
| restreint, aucune unité en validité | `scope=none` |
| jeton émis avant ce lot | aucun claim de périmètre : lu **global** jusqu'à son expiration (`AccessTokenMinutes`) |

La réponse de connexion et `GET /api/v1/me` exposent `unitScope { isGlobal, units[] }` ; le client WPF
l'affiche dans le bandeau de session (« Toutes les unités », la liste des codes, ou « Aucune unité ») —
affichage seulement, le serveur fait autorité sur chaque route.

### Filtre d'API (`UnitScopeEndpointFilter`, groupe `/api/v1`)

Après authentification et autorisation, pour un appelant au périmètre **restreint** (un appelant global
n'est jamais filtré), le filtre lit :

1. la valeur de route `hotelUnitCode` ;
2. le paramètre de requête `hotelUnitCode` (clé et valeur comparées sans casse) ;
3. dans chaque argument lié à la route, une propriété publique `HotelUnitCode` de type chaîne — la forme
   de tous les corps de création (recette, réservation, type de chambre, événement…).

Chaque code trouvé doit être dans le périmètre, sinon **403** avec `ErrorResponse` :
`{ "message": "L'unite hoteliere 'X' n'est pas dans votre perimetre (unites autorisees : A, B). …" }`.

**Préfixes exemptés** : `/api/v1/auth/*`, `/api/v1/me`, `/api/v1/health/*`, `/api/v1/security/*`,
`/api/v1/organization/*`, `/api/v1/settings`. Se connecter, lire son contexte, administrer les comptes
(dont leurs affectations) et les unités elles-mêmes ne dépendent pas du périmètre de l'administrateur.

**Préfixes effectivement couverts** (routes portant `hotelUnitCode` en route, en requête ou dans le
corps) :

| Module | Préfixes sous `/api/v1` |
|---|---|
| PMS | `/lodging/availability`, `/lodging/front-desk`, `/lodging/room-types`, `/lodging/rooms` (liste, création, `{id}/out-of-order`, `{id}/out-of-service`), `/lodging/reservations` (liste, création), `/lodging/occupancy`, `/lodging/room-blocks`, `/lodging/policy`, `/lodging/restrictions`, `/lodging/overbooking`, `/lodging/business-date`, `/lodging/forecast`, `/lodging/tape-chart`, `/lodging/arrivals`, `/lodging/departures`, `/lodging/in-house`, `/lodging/no-shows`, `/lodging/night-audit`, `/lodging/extras`, `/lodging/packages`, `/lodging/cancellation-policies`, `/lodging/yield-rules` |
| Tarifs | `/tariffs/plans` (liste, création), `/tariffs/resolve` |
| Housekeeping | `/housekeeping/board`, `/housekeeping/day-sheet`, `/housekeeping/tasks` (liste, génération), `/housekeeping/minibar/items`, `/housekeeping/minibar/consumptions` |
| MICE | `/mice/spaces` (liste, `{hotelUnitCode}/{code}`), `/mice/events` (liste, création), `/mice/allotments` (liste, création) |
| CRM | `/crm/satisfaction`, `/crm/satisfaction/nps`, `/crm/interactions` |
| Exploitation et finance | `/revenue/daily` (liste, `summary`, création), `/closing/daily`, `/treasury/receipts` (liste, `summary`, création, modification), `/billing/invoices` (liste, création), `/budget/plans` (liste, création), `/budget/variance` |
| Stocks, RH, pilotage, système | `/inventory/warehouses` (création, modification), `/hr/employees` (liste, création, modification), `/kpis/thresholds`, `/sync/stations/heartbeat` |

**Deux limites, assumées, que seul le contrôle dans les services métier (A6b) lèvera :**

1. **Une liste appelée sans paramètre d'unité n'est pas filtrée.** `GET /api/v1/revenue/daily` sans
   `hotelUnitCode` rend aujourd'hui toutes les unités, à un compte restreint comme à un compte global. Le
   filtre refuse ce qui est *demandé* hors périmètre ; il ne restreint pas ce qui n'est pas précisé.
2. **Une route qui n'identifie l'unité qu'après chargement n'est pas couverte.** Tout ce qui est adressé
   par identifiant — `/lodging/reservations/{id}/…` (arrivée, départ, annulation, folio),
   `/billing/invoices/{id}/…`, `/treasury/receipts/{id}`, `/revenue/daily/{id}` (dont soumission et
   validation), `/inventory/movements|transfers|counts`, `/lodging/deposits`, `/mice/events/{id}/…`,
   `/housekeeping/tasks/{id}/…` — et les modules dont la requête ne porte pas d'unité (`/accounting`,
   `/approvals`, `/purchasing`, `/kitchen`, `/maintenance`, `/reporting`, `/crm/guests|segments|loyalty|campaigns`,
   `/hr` hors employés, `/audit`, `/kpis` en lecture).

Le filtre est la première ligne, pas la dernière. `UnitScopeEndpointFilterTests` le prouve sur la route, la
requête et le corps (accepté, refusé, casse), sur un périmètre global, sur `scope=none`, sur les
exemptions et sur une requête anonyme ; il fige aussi la limite 1 pour qu'A6b la change sciemment.

### Administration du périmètre

| Route | Permission | Effet |
|---|---|---|
| `GET /api/v1/security/users/{id}/units` | `users.read` | `{ userId, userName, isGlobal, units[{ hotelUnitCode, assignedAt, assignedBy, validFrom, validTo }] }` ; 404 si le compte n'existe pas |
| `PUT /api/v1/security/users/{id}/units` | `users.write` | Remplacement **complet** du périmètre (`{ "units": ["HOTELA"] }`, codes normalisés en majuscules, doublons fondus). `units: []` rend le compte **global** ; champ absent → 400 ; code inconnu → 400 et **rien** n'est enregistré, pas même les codes valides ; une unité conservée garde sa date d'affectation ; audit `security.user.units_changed` avec l'avant et l'après |

Le changement s'applique au **prochain jeton** du compte (connexion ou rafraîchissement). Dans le client
WPF, `UsersView` propose les unités (actives et désactivées ; `units.read` requis pour les lister, sinon
l'écran le dit et le reste du module fonctionne) sous les rôles du compte sélectionné, et confirme avant
d'enregistrer en montrant l'avant et l'après.

### Rôle système `reception`

Le comptoir du PMS, jusqu'ici tenu par `cashier` — qui porte aussi la caisse (`treasury.write`,
`revenue.write`) et le night audit. `reception` reçoit strictement `settings.read`, `lodging.read`,
`lodging.checkin`, `lodging.reserve`, `lodging.checkout`, `lodging.room_move`, `lodging.noshow`,
`lodging.cancel`, `customers.read`, `customers.write`, `crm.read`, `housekeeping.read`, `invoices.read`,
`treasury.read`, plus les clés cibles que ces clés historiques couvrent (règle d'équivalence du seeder).
Ni caisse, ni `lodging.night_audit`, ni émission de facture, jamais `approvals.decide` :
`RoleCatalog.ApprovalDeciderRoles` est inchangé, et `cashier` n'a pas été modifié
(`SecuritySeederTests`, `RbacPolicyMatrixTests`).

## Next security tasks

- Lot A6b : faire respecter le périmètre utilisateur ↔ unité dans chaque service métier (listes sans
  paramètre d'unité, routes adressées par identifiant), en s'appuyant sur `IUnitScopeProvider`.
- Retaguer les domaines hors P0 (socle, CRM, MICE, Housekeeping, F&B, Pilotage, Système) vers les clés
  cibles, puis faire évaluer les clés cibles par le client WPF.
