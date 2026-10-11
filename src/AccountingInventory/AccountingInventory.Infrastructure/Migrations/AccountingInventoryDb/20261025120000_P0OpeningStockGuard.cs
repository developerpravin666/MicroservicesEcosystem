using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class P0OpeningStockGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_opening_stock",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM products WHERE is_opening_stock=true) THEN
                        RAISE EXCEPTION 'Opening stock has been imported. Rehearse recovery from the pre-upgrade backup instead of removing its origin marker.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "is_opening_stock",
                table: "products");
        }
    }
}
