using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20260930140000_AddSalesProduct")]
public sealed class AddSalesProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "sales_products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                product_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                customer_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                customer_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                sale_date = table.Column<DateOnly>(type: "date", nullable: false),
                product_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                selling_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_sales_products", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_sales_products_serial_number", table: "sales_products", column: "serial_number", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "sales_products");
}
