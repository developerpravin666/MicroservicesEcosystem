using BuildingBlocks.Domain;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AccountingInventory.Infrastructure.Persistence;

// Design-time operations never load API hosted services or migrate live tenants.
public sealed class AccountingInventoryDesignFactory : IDesignTimeDbContextFactory<AccountingInventoryDbContext>
{
    public AccountingInventoryDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AccountingInventoryDbContext>()
            .UseNpgsql("Host=localhost;Database=design_only;Username=design_only")
            .UseSnakeCaseNamingConvention().Options, new DesignTenant());
    private sealed class DesignTenant : ITenantProvider
    {
        public string SchemaName => "public";
    }
}
