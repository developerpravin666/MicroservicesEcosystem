using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class RemovedVendorIdfromBrandandProductType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_types_vendor_id_brand_id_name",
                table: "product_types");

            migrationBuilder.DropColumn(
                name: "vendor_id",
                table: "product_types");

            migrationBuilder.DropColumn(
                name: "vendor_id",
                table: "brands");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_brand_id_name",
                table: "product_types",
                columns: new[] { "brand_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_types_brand_id_name",
                table: "product_types");

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_id",
                table: "product_types",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "vendor_id",
                table: "brands",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_product_types_vendor_id_brand_id_name",
                table: "product_types",
                columns: new[] { "vendor_id", "brand_id", "name" },
                unique: true);
        }
    }
}
