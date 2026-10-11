# Microservices Ecosystem — .NET 10 / C# 14

Multi-tenant, Clean Architecture, DDD, event-driven microservices reference solution.
Tenant management lives inside **AccountingInventory** — alongside Users and Roles, not
as a separate microservice — because tenants, users, and roles are one bounded
context (Identity & Access Management), and putting them together avoids a cross-
service network hop and an eventual-consistency window for the one thing that must
exist before anything else can: the tenant itself.

## Layout

```
Directory.Build.props / Directory.Packages.props    Central Package Management, shared across every project
deploy/                                              Docker + Compose for both deployment modes
src/
├── BuildingBlocks/            Shared kernel — see "Shared building blocks" below
├── Gateway/ApiGateway/        YARP reverse proxy, rate limiting, correlation-id propagation
├── Identity/                  AccountingInventory: Tenants, Users, DB-managed Roles/Permissions, JWT issuance
│   ├── AccountingInventory.Domain / .Application / .Infrastructure / .Api
└── Services/
    └── InventoryService/      THE GOLD MASTER TEMPLATE — clone this to add a new business module
```

## Multi-tenancy

**Two databases, two different isolation strategies, both owned by AccountingInventory:**

- **`TenantDb`** — one shared, non-tenant-scoped database holding the `Tenants`
  table itself (Name, Slug, SchemaName, Status). This is genuinely global data — the
  list of tenants can't live "inside" a tenant — so it gets its own dedicated
  database rather than a schema inside `AccountingInventoryDb`, which also sidesteps any
  possibility of it colliding with a tenant's own schema.
- **`AccountingInventoryDb`** — schema-per-tenant. Every tenant gets its own PostgreSQL schema,
  named from **both its name and its id** (e.g. `tenant_acme_corp_3f2a1b4c` —
  `TenantSchemaNameValidator.BuildSchemaName`), containing that tenant's `Users` and
  `Roles` tables. Two tenants can have identically-named users with zero collision
  because they're in physically different schemas.

At runtime, which schema a request's queries land in is decided by
`BuildingBlocks.Persistence.TenantSchemaConnectionInterceptor`, which runs
`SET search_path TO "tenant_acme_corp_3f2a1b4c", "public";` on every connection a
schema-per-tenant `DbContext` opens, based on `ITenantContext` (populated from the
caller's JWT `tenant_id`/`tenant_schema` claims). No entity in `IdentityDbContext`
hardcodes a schema — the same compiled EF Core model works for every tenant because
PostgreSQL resolves the unqualified table names via `search_path`.

### Tenant registration flow

`POST /api/tenants/register { name, slug }` (anonymous — self-service signup) does
all of this **synchronously, in one request**, because Identity owns both databases
involved:

1. Reserve the slug in `TenantDb` (`Tenant.Create` derives the schema name from the
   new tenant's name + generated id).
2. Provision the new schema in `AccountingInventoryDb` and run migrations into it
   (`TenantSchemaProvisioner`).
3. Seed the tenant's two default, system-defined roles directly into that schema:
   `Admin` (every built-in permission) and `User` (none by default).
4. Mark the tenant `Active` in `TenantDb`.
5. Publish `TenantCreatedIntegrationEvent` via RabbitMQ so **other** tenant-scoped
   services (InventoryService, and any future Gold-Master clone) provision their own
   schema for this tenant asynchronously — that part necessarily stays event-driven,
   since Identity has no visibility into another service's database.

A user can register (`POST /api/auth/register` with `X-Tenant-Id` and user details) the moment step 4
completes — no waiting on other services.

## Roles and permissions: managed entirely from the database

- **`Role`** is a full aggregate (its own table, per tenant schema): a name plus a
  list of permission codes (e.g. `Inventory.StockItems.Create`), fully CRUD-able via
  `POST/GET /api/roles`, `PUT /api/roles/{id}/permissions`, `DELETE /api/roles/{id}`.
- **`User`** references roles by id, also DB-managed via
  `POST/DELETE /api/roles/{roleId}/users/{userId}`.
- At **login** (and again at every **token refresh**), AccountingInventory reads the
  current user's roles straight from the database, unions every permission code
  those roles grant, and embeds them as `role`/`permission` claims on the JWT.
- Every protected endpoint in every service declares the permission it needs in one
  line — `.RequirePermission("Inventory.StockItems.Create")`
  (`BuildingBlocks.Security`) — a fast local claim check, no network call to
  AccountingInventory per request. But the claims themselves are 100% computed from the
  database, so editing a role's permissions changes what its users can do the next
  time they log in or refresh (≤ 15 minutes by default), with no code change or
  redeploy anywhere.

## Shared building blocks

| Project | Owns |
|---|---|
| `BuildingBlocks.Domain` | `AuditableEntity`, `AggregateRoot`, `DomainException`, `ITenantEntity`, `ITenantContext`/`ITenantContextAccessor`, tenant schema-name derivation/validation |
| `BuildingBlocks.Application` | Shared `ConflictException`/`NotFoundException`/`UnauthorizedException`/`ForbiddenException`, `ValidationBehavior<,>` |
| `BuildingBlocks.Security` | JWT auth registration, tenant-context resolution from claims, `RequirePermission()` |
| `BuildingBlocks.Persistence` | Audit + tenant-schema `DbContext` interceptors, `TenantSchemaProvisioner` |
| `BuildingBlocks.Messaging` | MassTransit/RabbitMQ host registration, `IEventPublisher`, correlation propagation |
| `BuildingBlocks.Contracts` | Cross-service integration events (`TenantCreatedIntegrationEvent`) |
| `BuildingBlocks.Observability` | Serilog bootstrap (console + rolling file), correlation-id middleware, request logging |
| `BuildingBlocks.WebDefaults` | Shared `IExceptionHandler` → RFC 7807 ProblemDetails, Swagger, OpenTelemetry |

`AddPlatformSecurity`, `AddPlatformMessaging`, and `AddWebDefaults` are **host-level**
registrations — called exactly once by whichever `Program.cs` is the process's entry
point, since JWT auth, the RabbitMQ bus, and Swagger/OTel can each only be configured
once per process.

## Deployment: separate servers or one server, same images

See `deploy/README.md` for full commands. In short:

- **`deploy/docker-compose.yml`** — Postgres, Redis, RabbitMQ, Gateway, Identity, and
  Inventory together on one Docker host.
- **`deploy/docker-compose.<service>.yml`** — one file per service (`identity`,
  `inventory`, `gateway`), each deployable to its own server independently, pointed
  at shared infrastructure via environment variables.

Both modes build and run the exact same Dockerfiles/images.

## Cloning the Gold Master (InventoryService)

1. Copy `src/Services/InventoryService` → `src/Services/OrderService`; rename
   `.csproj` files, `RootNamespace`, and every `namespace InventoryService.*`.
2. Replace `StockItem` with your own aggregate; replace `AddStock`/`GetStock` with
   your own commands/queries.
3. Keep the `TenantProvisioningConsumer` pattern (copy Inventory's — one line
   changes: your DbContext type) so the new service's schema gets created per tenant
   automatically when AccountingInventory publishes `TenantCreatedIntegrationEvent`.
4. Protect your endpoints with `.RequirePermission("OrderService.Orders.Create")` and
   grant that code to whichever roles should have it via `/api/roles`.
5. Add a Dockerfile + gateway route + compose entries following Inventory's pattern.

## Running locally

```bash
cp deploy/.env.example deploy/.env   # set a real JWT signing key
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

```bash
# 1. Register a tenant (creates its schema + seeds Admin/User roles, synchronously)
curl -X POST http://localhost:8080/api/identity/api/tenants/register \
  -H 'Content-Type: application/json' -d '{"name":"Acme Corp","slug":"acme"}'

# 2. Register the first user for that tenant (gets the seeded "User" role)
curl -X POST http://localhost:8080/api/identity/api/auth/register \
  -H 'Content-Type: application/json' -H 'X-Tenant-Id: <tenantId>' \
  -d '{"userName":"jane","email":"jane@acme.test","password":"Passw0rd!"}'

# 3. Log in
curl -X POST http://localhost:8080/api/identity/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"tenantSlug":"acme","userName":"jane","password":"Passw0rd!"}'

# 4. Call a protected Inventory endpoint with the returned access token
curl -X POST http://localhost:8080/api/inventory/api/stock-items \
  -H 'Authorization: Bearer <accessToken>' -H 'Content-Type: application/json' \
  -d '{"sku":"WIDGET-1","displayName":"Widget","warehouseLocation":"MAIN"}'

# 5. Add a vendor to that tenant's schema. Use the tenantId returned at step 1;
#    the API resolves the database schema itself, so never send a schema name.
curl -X POST http://localhost:8080/api/identity/api/vendors/add \
  -H 'X-Tenant-Id: <tenantId>' -H 'Content-Type: application/json' \
  -d '{"name":"Acme Supplies","code":"ACME-SUP","mobile":"5550100","email":"sales@acme.test","description":"Preferred supplier","address":"1 Market Street"}'
```

Logs land in the console (structured JSON) and under
`<project>/logs/<ServiceName>-<date>.log` for each running service.

## Known trade-offs (read before production use)

- **Permission changes take effect on next login/refresh, not instantly** —
  deliberate, to avoid a database round-trip on every authorized request. Lower
  `Jwt:AccessTokenMinutes` if your use case needs faster propagation.
- **`User.RoleIds` / `Role.PermissionCodes` are stored as converted delimited
  columns**, not a normalized join table — simple and adequate for the tenant sizes
  this reference architecture targets; a high-scale deployment should replace these
  with real `UserRole`/`RolePermission` join tables.
- **Tenant registration is anonymous** (self-service signup) with no email
  verification or abuse protection beyond the Gateway's rate limiter — add both
  before exposing it publicly.
- **Schema-per-tenant via `search_path`** is a well-established PostgreSQL/EF Core
  pattern but an advanced one — it has not been exercised against a real PostgreSQL
  instance as part of generating this solution (no database/build environment was
  available). Run the full Register-tenant → Register-user → Login → protected-
  endpoint flow against a real Postgres instance before trusting it in production,
  particularly the connection-reset-then-reopen sequence in
  `RegisterCommandHandler`/`LoginCommandHandler` (`ResetConnectionAsync` +
  `ITenantContextAccessor.SetTenant`) and the synchronous provisioning step inside
  `RegisterTenantCommandHandler`.
