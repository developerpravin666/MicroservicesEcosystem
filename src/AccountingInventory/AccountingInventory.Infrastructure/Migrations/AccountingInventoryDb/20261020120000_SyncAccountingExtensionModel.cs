using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class SyncAccountingExtensionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bank_statement_lines_bank_reconciliations_bank_reconciliation_id",
                table: "bank_statement_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_fixed_assets_chart_accounts_accumulated_depreciation_account_id",
                table: "fixed_assets");

            migrationBuilder.DropIndex(
                name: "ix_accounting_user_permissions_user_id",
                table: "accounting_user_permissions");

            migrationBuilder.DropIndex(
                name: "ix_account_budgets_account_id",
                table: "account_budgets");

            migrationBuilder.AddForeignKey(
                name: "fk_bank_statement_lines_bank_reconciliations_bank_reconciliati",
                table: "bank_statement_lines",
                column: "bank_reconciliation_id",
                principalTable: "bank_reconciliations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fixed_assets_chart_accounts_accumulated_depreciation_accoun",
                table: "fixed_assets",
                column: "accumulated_depreciation_account_id",
                principalTable: "chart_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_bank_statement_lines_bank_reconciliations_bank_reconciliati",
                table: "bank_statement_lines");

            migrationBuilder.DropForeignKey(
                name: "fk_fixed_assets_chart_accounts_accumulated_depreciation_accoun",
                table: "fixed_assets");

            migrationBuilder.CreateIndex(
                name: "ix_accounting_user_permissions_user_id",
                table: "accounting_user_permissions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_account_budgets_account_id",
                table: "account_budgets",
                column: "account_id");

            migrationBuilder.AddForeignKey(
                name: "fk_bank_statement_lines_bank_reconciliations_bank_reconciliation_id",
                table: "bank_statement_lines",
                column: "bank_reconciliation_id",
                principalTable: "bank_reconciliations",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fixed_assets_chart_accounts_accumulated_depreciation_account_id",
                table: "fixed_assets",
                column: "accumulated_depreciation_account_id",
                principalTable: "chart_accounts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
