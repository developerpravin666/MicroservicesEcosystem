using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20260930130000_AddFinanceVendor")]
public sealed class AddFinanceVendor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "finance_vendors",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                contact_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                contact_mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table => table.PrimaryKey("pk_finance_vendors", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_finance_vendors_code", table: "finance_vendors", column: "code");
        migrationBuilder.CreateIndex(name: "ix_finance_vendors_email", table: "finance_vendors", column: "email", unique: false);
        migrationBuilder.CreateIndex(name: "ix_finance_vendors_mobile", table: "finance_vendors", column: "mobile", unique: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "finance_vendors");
}
