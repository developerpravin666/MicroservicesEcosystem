using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261005110000_AddProductTotalAmount")]
public sealed class AddProductTotalAmount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE products ADD COLUMN IF NOT EXISTS total_amount numeric(18,2) NULL;
            UPDATE products
            SET total_amount = ROUND(purchase_price * (1 + (cgst + sgst) / 100), 2)
            WHERE total_amount IS NULL;
            ALTER TABLE products ALTER COLUMN total_amount SET NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "total_amount", table: "products");
}
