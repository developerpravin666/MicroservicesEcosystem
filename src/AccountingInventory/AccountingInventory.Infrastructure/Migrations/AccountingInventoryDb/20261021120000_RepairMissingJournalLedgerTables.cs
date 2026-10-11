using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261021120000_RepairMissingJournalLedgerTables")]
public sealed class RepairMissingJournalLedgerTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql(JournalLedgerTableRepair.Sql);

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately retain repaired ledger tables and their data on rollback.
    }
}
