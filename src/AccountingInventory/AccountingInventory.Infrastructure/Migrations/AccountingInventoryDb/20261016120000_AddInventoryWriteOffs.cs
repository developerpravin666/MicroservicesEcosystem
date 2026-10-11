using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261016120000_AddInventoryWriteOffs")]
public sealed class AddInventoryWriteOffs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "inventory_adjustments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                product_id = table.Column<Guid>(type: "uuid", nullable: false),
                adjustment_date = table.Column<DateOnly>(type: "date", nullable: false),
                reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            }, constraints: table =>
            {
                table.PrimaryKey("pk_inventory_adjustments", row => row.id);
                table.ForeignKey("fk_inventory_adjustments_products_product_id", row => row.product_id,
                    "products", "id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("ix_inventory_adjustments_product_id", "inventory_adjustments", "product_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable("inventory_adjustments");
}
