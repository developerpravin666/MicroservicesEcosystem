using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations;

/// <summary>
/// Tenant tables are intentionally unqualified. TenantSchemaMigrator sets the
/// PostgreSQL search_path before running this migration, allowing one migration to
/// create the same tables in every tenant schema.
/// </summary>
public partial class InitialTenantSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                password_hash = table.Column<string>(type: "text", nullable: false),
                refresh_token_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                refresh_token_expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_users", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_users_email", table: "users", column: "email", unique: true);
        migrationBuilder.CreateIndex(name: "ix_users_user_name", table: "users", column: "user_name", unique: true);

        migrationBuilder.CreateTable(
            name: "vendors",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                code = table.Column<string>(type: "text", nullable: false),
                mobile = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                description = table.Column<string>(type: "text", nullable: true),
                address = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "text", nullable: true),
                modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                modified_by = table.Column<string>(type: "text", nullable: true),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                is_deleted = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_vendors", x => x.id));

        migrationBuilder.CreateIndex(name: "ix_vendors_code", table: "vendors", column: "code", unique: true);
        migrationBuilder.CreateIndex(name: "ix_vendors_email", table: "vendors", column: "email", unique: true);
        migrationBuilder.CreateIndex(name: "ix_vendors_mobile", table: "vendors", column: "mobile", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "users");
        migrationBuilder.DropTable(name: "vendors");
    }
}
