using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261015120000_AddBankReconciliation")]
public sealed class AddBankReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "bank_reconciliations",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                statement_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                start_date = table.Column<DateOnly>(type: "date", nullable: false),
                end_date = table.Column<DateOnly>(type: "date", nullable: false),
                opening_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                closing_balance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                is_finalized = table.Column<bool>(type: "boolean", nullable: false),
                finalized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_bank_reconciliations", row => row.id);
                table.ForeignKey("fk_bank_reconciliations_chart_accounts_account_id", row => row.account_id,
                    "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateTable(
            name: "bank_statement_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                bank_reconciliation_id = table.Column<Guid>(type: "uuid", nullable: false),
                transaction_date = table.Column<DateOnly>(type: "date", nullable: false),
                description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                journal_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_bank_statement_lines", row => row.id);
                table.ForeignKey("fk_bank_statement_lines_bank_reconciliations_bank_reconciliation_id",
                    row => row.bank_reconciliation_id, "bank_reconciliations", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_bank_statement_lines_journal_lines_journal_line_id",
                    row => row.journal_line_id, "journal_lines", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("ix_bank_reconciliations_account_id_statement_reference", "bank_reconciliations",
            new[] { "account_id", "statement_reference" }, unique: true);
        migrationBuilder.CreateIndex("ix_bank_statement_lines_bank_reconciliation_id", "bank_statement_lines", "bank_reconciliation_id");
        migrationBuilder.CreateIndex("ix_bank_statement_lines_journal_line_id", "bank_statement_lines", "journal_line_id", unique: true,
            filter: "\"journal_line_id\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("bank_statement_lines");
        migrationBuilder.DropTable("bank_reconciliations");
    }
}
