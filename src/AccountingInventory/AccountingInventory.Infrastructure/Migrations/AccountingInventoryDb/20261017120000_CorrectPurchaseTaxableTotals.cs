using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261017120000_CorrectPurchaseTaxableTotals")]
public sealed class CorrectPurchaseTaxableTotals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("UPDATE products SET total_amount = ROUND((purchase_price - discount) * (1 + (cgst + sgst) / 100), 2)");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("UPDATE products SET total_amount = ROUND(purchase_price * (1 + (cgst + sgst) / 100), 2)");
}
