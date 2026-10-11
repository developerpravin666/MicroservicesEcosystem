using AccountingInventory.Application.Abstractions;
using Microsoft.Extensions.Configuration;
using AccountingInventory.Infrastructure.Persistence;

namespace AccountingInventory.Infrastructure.Security;

/// <summary>Provisions a newly registered tenant using its dedicated EF model/schema.</summary>
public sealed class TenantSchemaProvisionerAdapter(IConfiguration configuration)
    : AccountingInventory.Application.Abstractions.ITenantSchemaProvisioner
{
    public Task ProvisionAsync(Guid tenantId, string schemaName, CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("AccountingInventoryDb")
            ?? throw new InvalidOperationException("The AccountingInventoryDb connection string is not configured.");

        return TenantSchemaMigrator.MigrateAsync(connectionString, schemaName, cancellationToken);
    }
}
