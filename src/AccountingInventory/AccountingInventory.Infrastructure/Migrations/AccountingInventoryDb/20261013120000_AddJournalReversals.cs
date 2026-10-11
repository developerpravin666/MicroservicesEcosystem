using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261013120000_AddJournalReversals")]
public sealed class AddJournalReversals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE journal_entries
                ADD COLUMN IF NOT EXISTS reversal_of_journal_entry_id uuid NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_entries_reversal_of_journal_entry_id
                ON journal_entries (reversal_of_journal_entry_id)
                WHERE reversal_of_journal_entry_id IS NOT NULL;
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'fk_journal_entries_journal_entries_reversal_of_journal_entry_id'
                      AND conrelid = 'journal_entries'::regclass
                ) THEN
                    ALTER TABLE journal_entries
                        ADD CONSTRAINT fk_journal_entries_journal_entries_reversal_of_journal_entry_id
                        FOREIGN KEY (reversal_of_journal_entry_id)
                        REFERENCES journal_entries (id) ON DELETE RESTRICT;
                END IF;
            END $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_journal_entries_journal_entries_reversal_of_journal_entry_id", "journal_entries");
        migrationBuilder.DropIndex("ix_journal_entries_reversal_of_journal_entry_id", "journal_entries");
        migrationBuilder.DropColumn("reversal_of_journal_entry_id", "journal_entries");
    }
}
