using AccountingInventory.Infrastructure.Persistence;
using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

internal static class MigrationChainChecks
{
    public static async Task RunAsync(string connectionString)
    {
        var schema = "tenant_migrationcheck_" + Guid.NewGuid().ToString("N");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var create = new NpgsqlCommand($"CREATE SCHEMA {schema}", connection)) await create.ExecuteNonQueryAsync();
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString) { SearchPath = schema };
            var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
                .UseNpgsql(builder.ConnectionString, n => n.MigrationsHistoryTable("__TenantSchemaHistory", schema))
                .UseSnakeCaseNamingConvention()
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options;
            await using var db = new AccountingInventoryDbContext(options, new TestTenantProvider(schema));
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261013140000_AddDatedTaxProfiles");
            await migrator.MigrateAsync();
            await db.StockSkus.AsNoTracking().Take(1).ToListAsync();
            await db.SkuMovements.AsNoTracking().Take(1).ToListAsync();
            if ((await db.Database.GetPendingMigrationsAsync()).Any()) throw new Exception("Pending migrations remain.");
            await migrator.MigrateAsync();
            await migrator.MigrateAsync("20261012120000_AddPurchaseInvoiceDate");
            await migrator.MigrateAsync();
            await db.StockSkus.AsNoTracking().Take(1).ToListAsync();
            Console.WriteLine("PASS: fresh schema, dated GST upgrade before accessory tables, complete migration chain, SKU queries, repeated migration and downgrade/re-upgrade.");
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP SCHEMA {schema} CASCADE", connection);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed class TestTenantProvider(string schema) : ITenantProvider
    {
        public string SchemaName => schema;
    }
}
