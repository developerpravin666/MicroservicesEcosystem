using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace BuildingBlocks.Persistence;

/// <summary>
/// This is what makes "separate schema per tenant" actually happen at the database
/// level. Every time EF Core opens a physical connection for a schema-per-tenant
/// DbContext, this interceptor runs `SET search_path TO "{tenant_schema}", public;`
/// on that connection BEFORE any query executes. Because the EF Core model for these
/// DbContexts never hardcodes a schema on its entities (see e.g. InventoryDbContext /
/// IdentityDbContext — their OnModelCreating does not call HasDefaultSchema), the same
/// single compiled model resolves table names like "StockItems" or "Users" against
/// whatever schema PostgreSQL's search_path currently points to for that connection —
/// so ten different tenants can share one connection pool and one compiled EF model
/// while their data is physically isolated in ten different PostgreSQL schemas.
///
/// One entity type is deliberately exempt from this: any DbContext that also needs a
/// control-plane table shared across all tenants (e.g. AccountingInventory's
/// TenantDirectory, used to resolve a tenant slug to its schema name BEFORE the
/// caller is authenticated) maps that specific entity to an explicit schema
/// (`.ToTable("TenantDirectory", schema: "public")`) — an explicit schema always wins
/// over search_path, so that table is reachable no matter what the current tenant
/// context is.
///
/// IMPORTANT: if a handler resolves/changes the tenant context PARTWAY through a
/// request (this only ever happens in Login/Register and tenant-provisioning event
/// consumers — see ITenantContextAccessor), it must call
/// `await db.Database.CloseConnectionAsync()` immediately after setting the new
/// tenant context and before the next query, so EF Core is forced to open a fresh
/// connection (cheap — it comes straight back out of the pool) and this interceptor
/// re-fires with the corrected schema. Without that explicit close, EF Core would
/// keep reusing the already-open connection — and an already-open connection's
/// search_path was set once, at the time it was first opened, and does not
/// automatically follow later changes to ITenantContext.
/// </summary>
public sealed class TenantSchemaConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string DefaultSchema = "public";

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await ApplySearchPathAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplySearchPathAsync(connection, CancellationToken.None).GetAwaiter().GetResult();
        base.ConnectionOpened(connection, eventData);
    }

    private async Task ApplySearchPathAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var schema = tenantContext.SchemaName;

        if (schema is not null)
        {
            TenantSchemaNameValidator.EnsureValid(schema);
        }

        var effectiveSchema = schema ?? DefaultSchema;

        await using var command = connection.CreateCommand();
        // Identifiers cannot be parameterized in SQL; EnsureValid above guarantees
        // `effectiveSchema` only ever contains [a-z0-9_] and starts with a letter, so
        // this interpolation can never introduce SQL injection.
        command.CommandText = effectiveSchema == DefaultSchema
            ? $"SET search_path TO \"{DefaultSchema}\";"
            : $"SET search_path TO \"{effectiveSchema}\", \"{DefaultSchema}\";";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
