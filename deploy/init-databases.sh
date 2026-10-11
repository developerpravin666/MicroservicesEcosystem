#!/bin/bash
# Runs automatically by the postgres image on first container start. Two databases
# owned by IdentityService — "identity_db" (schema-per-tenant: each tenant's own
# Users/Roles schema, created on demand at tenant registration) and "tenant_db" (the
# shared tenant registry — kept as its own physical database rather than a schema
# inside identity_db, so it's never at risk of colliding with any tenant's schema) —
# plus "inventory_db", owned by InventoryService (also schema-per-tenant).
set -e

for db in identity_db tenant_db inventory_db; do
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" <<-SQL
    SELECT 'CREATE DATABASE $db' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
SQL
done
