using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261009120000_AddGeneralLedger")]
public sealed class AddGeneralLedger : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("chart_accounts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                normal_balance = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                is_system = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_chart_accounts", x => x.id));

        migrationBuilder.CreateTable("journal_entries",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                journal_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                journal_date = table.Column<DateOnly>(type: "date", nullable: false),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                source_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                source_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table => table.PrimaryKey("pk_journal_entries", x => x.id));

        migrationBuilder.CreateTable("journal_lines",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                account_id = table.Column<Guid>(type: "uuid", nullable: false),
                debit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                credit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                memo = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_journal_lines", x => x.id);
                table.ForeignKey("fk_journal_lines_chart_accounts_account_id", x => x.account_id,
                    "chart_accounts", "id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("fk_journal_lines_journal_entries_journal_entry_id", x => x.journal_entry_id,
                    "journal_entries", "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("ix_chart_accounts_code", "chart_accounts", "code", unique: true);
        migrationBuilder.CreateIndex("ix_journal_entries_journal_date", "journal_entries", "journal_date");
        migrationBuilder.CreateIndex("ix_journal_entries_journal_number", "journal_entries", "journal_number", unique: true);
        migrationBuilder.CreateIndex("ix_journal_lines_account_id", "journal_lines", "account_id");
        migrationBuilder.CreateIndex("ix_journal_lines_journal_entry_id", "journal_lines", "journal_entry_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("journal_lines");
        migrationBuilder.DropTable("chart_accounts");
        migrationBuilder.DropTable("journal_entries");
    }
}
