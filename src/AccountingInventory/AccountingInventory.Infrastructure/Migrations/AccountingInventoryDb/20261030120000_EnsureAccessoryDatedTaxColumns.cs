using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261030120000_EnsureAccessoryDatedTaxColumns")]
public sealed class EnsureAccessoryDatedTaxColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("""
            ALTER TABLE stock_skus ADD COLUMN IF NOT EXISTS default_tax_id uuid;
            ALTER TABLE sku_movements ADD COLUMN IF NOT EXISTS tax_id uuid;
            ALTER TABLE sku_movements ADD COLUMN IF NOT EXISTS tax_treatment text NOT NULL DEFAULT 'Taxable';
            """);

    // These columns belong to AddDatedTaxProfiles; retain them on downgrade.
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
