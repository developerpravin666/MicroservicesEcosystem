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
        var coordinator = scope.ServiceProvider.GetRequiredService<AccountingInventory.Application.Abstractions.IDatabaseMigrationCoordinator>();
        await coordinator.MigrateAllTenantSchemasAsync(cancellationToken);
    }
}
