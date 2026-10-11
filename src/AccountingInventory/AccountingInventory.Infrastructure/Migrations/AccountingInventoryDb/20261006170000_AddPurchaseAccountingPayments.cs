using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261006170000_AddPurchaseAccountingPayments")]
public sealed class AddPurchaseAccountingPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "purchase_payments",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                vendor_id = table.Column<Guid>(type: "uuid", nullable: false),
                bill_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_purchase_payments", x => x.id);
                table.ForeignKey("fk_purchase_payments_vendors_vendor_id", x => x.vendor_id,
                    principalTable: "vendors", principalColumn: "id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("ix_purchase_payments_vendor_id_bill_number",
            "purchase_payments", new[] { "vendor_id", "bill_number" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "purchase_payments");
}
