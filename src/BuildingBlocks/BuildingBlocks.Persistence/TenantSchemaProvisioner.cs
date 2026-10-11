using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Persistence;

/// <summary>
/// Provisions a brand-new PostgreSQL schema for a tenant and runs that service's EF
/// Core migrations into it. Called by each tenant-scoped service's
/// TenantCreatedIntegrationEvent consumer (see AccountingInventory's and
/// InventoryService's own consumers) the moment a new tenant is created in
/// TenantService — so by the time anyone tries to register a user or add stock for
/// that tenant, the schema and its tables already exist.
///
/// TContext must be a schema-per-tenant DbContext: one whose OnModelCreating never
/// calls HasDefaultSchema, so the exact same compiled model can be migrated into any
/// schema depending on what ITenantContextAccessor currently points at.
/// </summary>
public sealed class TenantSchemaProvisioner<TContext>(
    TContext dbContext,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<TenantSchemaProvisioner<TContext>> logger)
    where TContext : DbContext
{
    public async Task ProvisionAsync(Guid tenantId, string schemaName, CancellationToken cancellationToken)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);

        // Step 1: create the schema itself. This runs before any tenant context is
        // set, so the interceptor points the connection at the default "public"
        // search_path — irrelevant here since CREATE SCHEMA doesn't depend on it.
        await dbContext.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA IF NOT EXISTS \"{schemaName}\";", cancellationToken);

        // Step 2: force the connection closed so the NEXT operation (the migration)
        // opens a fresh connection and the TenantSchemaConnectionInterceptor re-fires
        // with the schema we're about to set — otherwise the already-open connection
        // from step 1 would still have the old (default) search_path.
        await dbContext.Database.CloseConnectionAsync();
        tenantContextAccessor.SetTenant(tenantId, schemaName);

        // Step 3: run this service's migrations against the new schema. Because the
        // model has no hardcoded schema, the exact same migration DDL that created
        // "public"'s tables (during local dev) now creates an identical set of
        // tables inside the new tenant schema, including its own independent
        // __EFMigrationsHistory row.
        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation(
            "Provisioned schema {SchemaName} for tenant {TenantId} using {DbContext}.",
            schemaName, tenantId, typeof(TContext).Name);
    }
}
