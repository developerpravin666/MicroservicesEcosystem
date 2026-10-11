using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20260929120000_AddPurchaseProducts")]
public sealed class AddPurchaseProducts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "products",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                brand_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                product_model_id = table.Column<Guid>(type: "uuid", nullable: false),
                variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                color_id = table.Column<Guid>(type: "uuid", nullable: false),
                serial_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                serial_number1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                quantity = table.Column<int>(type: "integer", nullable: false),
                purchase_price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                discount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                cgst = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                sgst = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                tax = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_products", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_products_serial_number", table: "products", column: "serial_number", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "products");
}
