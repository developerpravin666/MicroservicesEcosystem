# Deployment

Two ways to deploy the exact same Docker images — the choice is made entirely at
deploy time via which compose file(s) you run, with zero code or image differences.

## Option A — Single server (everything together)

```bash
cp deploy/.env.example deploy/.env   # edit JWT_SIGNING_KEY at minimum
docker compose -f deploy/docker-compose.yml --env-file deploy/.env up -d --build
```

Brings up Postgres, Redis, RabbitMQ, and both services (Gateway, Identity —
which owns tenant management — and Inventory) on one Docker host. Best for demos,
small deployments, or a single beefy VM.

## Option B — Separate hosts (one service per server)

Run shared infrastructure (Postgres/Redis/RabbitMQ) wherever you like — a managed
cloud database, a dedicated infra VM, whatever — then deploy each service
independently, pointing at that infrastructure via environment variables:

```bash
# On the identity-service host (this is also where tenants are registered/managed):
POSTGRES_HOST=db.internal RABBITMQ_HOST=mq.internal \
  docker compose -f deploy/docker-compose.identity.yml --env-file deploy/.env up -d --build

# On the inventory-service host:
POSTGRES_HOST=db.internal REDIS_HOST=cache.internal RABBITMQ_HOST=mq.internal \
  docker compose -f deploy/docker-compose.inventory.yml --env-file deploy/.env up -d --build

# On the gateway host, once the others are reachable:
IDENTITY_SERVICE_URL=http://identity.internal:8082/ \
INVENTORY_SERVICE_URL=http://inventory.internal:8083/ \
  docker compose -f deploy/docker-compose.gateway.yml --env-file deploy/.env up -d --build
```

Each service scales, restarts, and redeploys independently — this is the shape you'd
hand to Kubernetes (one Deployment + Service per file) or a fleet of separate VMs.

## Database layout

One PostgreSQL server hosts three physical databases:

- **`tenant_db`** — owned by IdentityService. The shared tenant registry (Name,
  Slug, SchemaName, Status). One table, never tenant-scoped, migrated once at
  IdentityService startup.
- **`identity_db`** — owned by IdentityService. Schema-per-tenant: every tenant gets
  its own schema (e.g. `tenant_acme_corp_3f2a1b4c`, named from the tenant's name +
  id) containing that tenant's `Users` and `Roles` tables, created on demand the
  moment a tenant registers via `POST /api/tenants/register`.
- **`inventory_db`** — owned by InventoryService. Also schema-per-tenant
  (`StockItems`), provisioned asynchronously when InventoryService consumes the
  `TenantCreatedIntegrationEvent` IdentityService publishes after registering a
  tenant.

All three are created automatically by `init-databases.sh` the first time the
`postgres` container starts. See the main README's "Multi-tenancy" section for the
full mechanics of how schema selection works at request time.
