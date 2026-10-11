using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20260928160000_AddVariantAndColor")]
public sealed class AddVariantAndColor : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in new[] { "variants", "colors" })
        {
            migrationBuilder.CreateTable(
                name: table,
                columns: tableBuilder => new
                {
                    id = tableBuilder.Column<Guid>(type: "uuid", nullable: false),
                    created_at = tableBuilder.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = tableBuilder.Column<Guid>(type: "uuid", nullable: true),
                    is_active = tableBuilder.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = tableBuilder.Column<bool>(type: "boolean", nullable: false),
                    modified_at = tableBuilder.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = tableBuilder.Column<Guid>(type: "uuid", nullable: true),
                    name = tableBuilder.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = tableBuilder.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: tableBuilder => tableBuilder.PrimaryKey($"pk_{table}", x => x.id));

            migrationBuilder.CreateIndex(
                name: $"ix_{table}_name",
                table: table,
                column: "name",
                unique: true);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "colors");
        migrationBuilder.DropTable(name: "variants");
    }
}
