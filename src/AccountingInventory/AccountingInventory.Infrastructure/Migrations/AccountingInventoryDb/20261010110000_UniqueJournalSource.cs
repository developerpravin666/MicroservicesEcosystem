using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261010110000_UniqueJournalSource")]
public sealed class UniqueJournalSource : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.CreateIndex(
            name: "ix_journal_entries_source_type_source_id",
            table: "journal_entries",
            columns: new[] { "source_type", "source_id" },
            unique: true,
            filter: "\"source_type\" IS NOT NULL AND \"source_id\" IS NOT NULL");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropIndex("ix_journal_entries_source_type_source_id", "journal_entries");
}
