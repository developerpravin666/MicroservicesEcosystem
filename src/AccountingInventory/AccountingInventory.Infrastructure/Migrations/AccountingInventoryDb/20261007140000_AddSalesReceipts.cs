using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261007140000_AddSalesReceipts")]
public sealed class AddSalesReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sales_receipts",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                sales_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                payment_mode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                reference_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_sales_receipts", x => x.id);
                table.ForeignKey(
                    name: "fk_sales_receipts_sales_products_sales_product_id",
                    column: x => x.sales_product_id,
                    principalTable: "sales_products",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex("ix_sales_receipts_sales_product_id_payment_date",
            "sales_receipts", new[] { "sales_product_id", "payment_date" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "sales_receipts");
}
