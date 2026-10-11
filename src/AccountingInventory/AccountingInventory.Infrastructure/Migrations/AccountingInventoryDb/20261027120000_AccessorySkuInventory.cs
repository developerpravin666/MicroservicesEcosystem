using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class AccessorySkuInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "sku_id",
                table: "sales_invoice_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stock_skus",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    hsn_sac = table.Column<string>(type: "text", nullable: false),
                    unit_of_measure = table.Column<string>(type: "text", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    inventory_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    last_movement_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_skus", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sku_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku_id = table.Column<Guid>(type: "uuid", nullable: false),
                    movement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    inventory_value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    vendor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bill_number = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_terms_days = table.Column<int>(type: "integer", nullable: false),
                    cgst_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sgst_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igst_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cgst_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sgst_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    igst_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sku_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_sku_movements_stock_skus_sku_id",
                        column: x => x.sku_id,
                        principalTable: "stock_skus",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sku_movements_vendors_vendor_id",
                        column: x => x.vendor_id,
                        principalTable: "vendors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_lines_sku_id",
                table: "sales_invoice_lines",
                column: "sku_id");

            migrationBuilder.CreateIndex(
                name: "ix_sku_movements_sku_id_movement_date",
                table: "sku_movements",
                columns: new[] { "sku_id", "movement_date" });

            migrationBuilder.CreateIndex(
                name: "ix_sku_movements_vendor_id",
                table: "sku_movements",
                column: "vendor_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_skus_code",
                table: "stock_skus",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_sales_invoice_lines_stock_skus_sku_id",
                table: "sales_invoice_lines",
                column: "sku_id",
                principalTable: "stock_skus",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM sku_movements) OR EXISTS (SELECT 1 FROM sales_invoice_lines WHERE sku_id IS NOT NULL) THEN
                    RAISE EXCEPTION 'Cannot remove used accessory inventory history; restore a rehearsed backup instead.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "fk_sales_invoice_lines_stock_skus_sku_id",
                table: "sales_invoice_lines");

            migrationBuilder.DropTable(
                name: "sku_movements");

            migrationBuilder.DropTable(
                name: "stock_skus");

            migrationBuilder.DropIndex(
                name: "ix_sales_invoice_lines_sku_id",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "sku_id",
                table: "sales_invoice_lines");
        }
    }
}
