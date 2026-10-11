using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261005130000_AddSalesPayments")]
public sealed class AddSalesPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sales_payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                sales_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                payment_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                finance_vendor_id = table.Column<Guid>(type: "uuid", nullable: true),
                down_payment = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                number_of_emi = table.Column<int>(type: "integer", nullable: true),
                emi_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                has_insurance = table.Column<bool>(type: "boolean", nullable: true),
                insurance_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                first_installment_date = table.Column<DateOnly>(type: "date", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sales_payments", x => x.id);
                table.ForeignKey(
                    name: "fk_sales_payments_finance_vendors_finance_vendor_id",
                    column: x => x.finance_vendor_id,
                    principalTable: "finance_vendors",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_sales_payments_sales_products_sales_product_id",
                    column: x => x.sales_product_id,
                    principalTable: "sales_products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ix_sales_payments_finance_vendor_id", "sales_payments", "finance_vendor_id");
        migrationBuilder.CreateIndex("ix_sales_payments_sales_product_id", "sales_payments", "sales_product_id", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "sales_payments");
}
