using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20260923170000_ConvertTenantAuditActorColumnsToUuid")]
public sealed class ConvertTenantAuditActorColumnsToUuid : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Older versions stored actor names as text. Only UUID-shaped values can
        // be retained as actor IDs; other legacy values are cleared to NULL.
        migrationBuilder.Sql("""
            ALTER TABLE "users"
                ALTER COLUMN "created_by" TYPE uuid USING
                    CASE WHEN "created_by" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                         THEN "created_by"::uuid ELSE NULL END,
                ALTER COLUMN "modified_by" TYPE uuid USING
                    CASE WHEN "modified_by" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                         THEN "modified_by"::uuid ELSE NULL END;

            ALTER TABLE "vendors"
                ALTER COLUMN "created_by" TYPE uuid USING
                    CASE WHEN "created_by" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                         THEN "created_by"::uuid ELSE NULL END,
                ALTER COLUMN "modified_by" TYPE uuid USING
                    CASE WHEN "modified_by" ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
                         THEN "modified_by"::uuid ELSE NULL END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "users"
                ALTER COLUMN "created_by" TYPE text USING "created_by"::text,
                ALTER COLUMN "modified_by" TYPE text USING "modified_by"::text;

            ALTER TABLE "vendors"
                ALTER COLUMN "created_by" TYPE text USING "created_by"::text,
                ALTER COLUMN "modified_by" TYPE text USING "modified_by"::text;
            """);
    }
}
