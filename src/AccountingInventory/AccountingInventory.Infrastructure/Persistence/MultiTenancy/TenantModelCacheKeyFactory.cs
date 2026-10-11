using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AccountingInventory.Infrastructure.Persistence.MultiTenancy;

/// <summary>Keeps design-time and runtime tenant context model keys distinct.</summary>
public sealed class TenantModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => context is AccountingInventoryDbContext tenantContext
            ? (context.GetType(), tenantContext.SchemaName, designTime)
            : (context.GetType(), designTime);

    public object Create(DbContext context) => Create(context, designTime: false);
}
