using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

internal static class P0MigrationChecks
{
    public static async Task RunAsync(AccountingInventoryDbContext db)
    {
        // This context is exclusively the rolled-back p0_check_<GUID> schema.
        if (!db.SchemaName.StartsWith("p0_check_", StringComparison.Ordinal)) throw new Exception("Migration checks require the scratch schema.");
        db.ChangeTracker.Clear();
        // Legacy schemas permit one live sale per unit; later resales exist only after P0.
        await db.Database.ExecuteSqlRawAsync("""
            WITH sales AS (SELECT id, row_number() OVER (PARTITION BY product_id ORDER BY sale_date, id) AS ordinal
                FROM sales_products WHERE is_deleted=false)
            UPDATE sales_products SET is_deleted=true WHERE id IN (SELECT id FROM sales WHERE ordinal>1);
            """);
        var saleCount = await db.SalesProducts.CountAsync(); var purchaseCount = await db.Products.CountAsync();
        await db.Database.ExecuteSqlRawAsync("""
            DROP TABLE correction_refunds, opening_settlements, opening_subledger_balances, invoice_corrections, invoice_snapshots CASCADE;
            DROP INDEX ix_users_is_owner;
            ALTER TABLE users DROP COLUMN is_owner, DROP COLUMN email_verified, DROP COLUMN invitation_hash, DROP COLUMN invitation_expires_at;
            DROP INDEX ix_sales_products_product_id;
            ALTER TABLE sales_products DROP COLUMN is_returned;
            CREATE UNIQUE INDEX ix_sales_products_product_id ON sales_products(product_id) WHERE is_deleted=false;
            ALTER TABLE products DROP COLUMN is_opening_stock;
            """);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var migration in new Migration[] { new AccountingP0Workflows(), new P0OpeningStockGuard() })
            foreach (var command in generator.Generate(migration.UpOperations, db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        if (await db.InvoiceSnapshots.CountAsync(x => x.Kind == "Sale") != saleCount
            || await db.InvoiceSnapshots.CountAsync(x => x.Kind == "Purchase") != purchaseCount
            || await db.InvoiceSnapshots.AnyAsync(x => !x.Reconstructed))
            throw new Exception("Upgrade must capture and flag reconstructed invoice snapshots.");
        var owner = await db.Users.SingleAsync(x => x.IsOwner);
        if (owner.UserName != "owner" || !owner.EmailVerified) throw new Exception("Upgrade must preserve the legacy owner and grandfather existing users.");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE invoice_corrections ADD COLUMN igst_amount numeric(18,2) NOT NULL DEFAULT 0");
        var fixture = InvoiceCorrection.Create("Sale", Guid.NewGuid(), Guid.NewGuid(), "TEST",new(2026,1,10),"Scratch rollback guard","Restock",10,0,0,0,0,10);
        db.InvoiceCorrections.Add(fixture);await db.SaveChangesAsync();
        var transaction=db.Database.CurrentTransaction!;await transaction.CreateSavepointAsync("p0_downgrade_guard");
        var blocked=false;
        try {
            foreach(var command in generator.Generate(new AccountingP0Workflows().DownOperations,db.Model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        } catch(PostgresException e) when(e.SqlState=="P0001") {blocked=true;await transaction.RollbackToSavepointAsync("p0_downgrade_guard");}
        if(!blocked || !await db.InvoiceCorrections.AnyAsync(x=>x.Id==fixture.Id))throw new Exception("Downgrade must preserve used financial workflows.");
        Console.WriteLine("PASS: P0 migration, reconstructed snapshots, persistent owner and financial-history downgrade guard");
    }
}
