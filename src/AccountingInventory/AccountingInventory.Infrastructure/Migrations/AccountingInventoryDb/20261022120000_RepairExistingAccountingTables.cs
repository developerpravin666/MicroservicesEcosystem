using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261022120000_RepairExistingAccountingTables")]
public sealed class RepairExistingAccountingTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(JournalLedgerTableRepair.Sql);
        migrationBuilder.Sql(ExistingAccountingTableRepair.Sql);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS products_invoice_due_date ON products;
            DROP TRIGGER IF EXISTS sales_products_invoice_due_date ON sales_products;
            DROP FUNCTION IF EXISTS set_invoice_due_date();
            """);
        // Retain repaired columns and historical accounting data on rollback.
    }
}
