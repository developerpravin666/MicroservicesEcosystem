using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AccountingInventory.Infrastructure.Persistence;

namespace AccountingInventory.Infrastructure.Migrations;

[DbContext(typeof(TenantDbContext))]
[Migration("20261029120000_UniqueTenantRegistration")]
public sealed class UniqueTenantRegistration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Preserve existing records; stop with an actionable error if cleanup is needed.
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM tenant.tenants GROUP BY lower(btrim(slug)) HAVING count(*) > 1)
                    OR EXISTS (SELECT 1 FROM tenant.tenants WHERE nullif(btrim(owner_email), '') IS NOT NULL
                        GROUP BY lower(btrim(owner_email)) HAVING count(*) > 1) THEN
                    RAISE EXCEPTION 'Duplicate tenant slugs or emails exist. Resolve duplicates before applying UniqueTenantRegistration.';
                END IF;
            END $$;
            UPDATE tenant.tenants SET slug = lower(btrim(slug)), owner_email = nullif(lower(btrim(owner_email)), '');
            """);
        migrationBuilder.CreateIndex(name: "ix_tenants_owner_email", schema: "tenant", table: "tenants", column: "owner_email", unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropIndex(name: "ix_tenants_owner_email", schema: "tenant", table: "tenants");
}
