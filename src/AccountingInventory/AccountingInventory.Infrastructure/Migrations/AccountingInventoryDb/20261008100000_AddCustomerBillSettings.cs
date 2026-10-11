using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261008100000_AddCustomerBillSettings")]
public sealed class AddCustomerBillSettings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "customer_bill_settings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                company_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                company_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                company_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                tax_registration_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                bill_title = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                footer_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                paper_size = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                show_customer_email = table.Column<bool>(type: "boolean", nullable: false),
                show_serial_number = table.Column<bool>(type: "boolean", nullable: false),
                show_discount = table.Column<bool>(type: "boolean", nullable: false),
                show_payment_history = table.Column<bool>(type: "boolean", nullable: false),
                show_balance_due = table.Column<bool>(type: "boolean", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
            },
            constraints: table => table.PrimaryKey("pk_customer_bill_settings", x => x.id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "customer_bill_settings");
}
