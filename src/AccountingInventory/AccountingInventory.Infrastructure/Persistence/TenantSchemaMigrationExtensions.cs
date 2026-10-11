using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AccountingInventory.Infrastructure.Persistence;

public static class TenantSchemaMigrationExtensions
{
    public static async Task ApplyTenantSchemaMigrationsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var tenantDbContext = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var connectionString = tenantDbContext.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The AccountingInventoryDb connection string is not configured.");

        var schemas = await tenantDbContext.Tenants
            .AsNoTracking()
            .Where(tenant => !tenant.IsDeleted && tenant.IsActive)
            .Select(tenant => tenant.SchemaName)
            .ToListAsync(cancellationToken);

        foreach (var schemaName in schemas.Distinct(StringComparer.Ordinal))
        {
            await TenantSchemaMigrator.MigrateAsync(connectionString, schemaName, cancellationToken);
        }
    }
}
