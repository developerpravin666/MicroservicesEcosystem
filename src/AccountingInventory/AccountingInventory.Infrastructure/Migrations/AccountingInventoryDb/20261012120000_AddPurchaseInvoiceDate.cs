using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261012120000_AddPurchaseInvoiceDate")]
public sealed class AddPurchaseInvoiceDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // A prior/manual schema update may have added purchase_date before this
        // migration was recorded. Add it only when missing, backfill only NULLs,
        // and then enforce the model's required/default behavior without
        // overwriting existing purchase dates.
        migrationBuilder.Sql("""
            ALTER TABLE products ADD COLUMN IF NOT EXISTS purchase_date date NULL;
            UPDATE products SET purchase_date = created_at::date WHERE purchase_date IS NULL;
            ALTER TABLE products ALTER COLUMN purchase_date SET DEFAULT CURRENT_DATE;
            ALTER TABLE products ALTER COLUMN purchase_date SET NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "purchase_date", table: "products");
}
