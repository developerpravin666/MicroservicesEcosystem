using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class UpdatingUniquenessonProductTypeentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_types_name",
                table: "product_types");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_vendor_id_brand_id_name",
                table: "product_types",
                columns: new[] { "vendor_id", "brand_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_types_vendor_id_brand_id_name",
                table: "product_types");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_name",
                table: "product_types",
                column: "name",
                unique: true);
        }
    }
}
