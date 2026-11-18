using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using AccountingInventory.Infrastructure.Persistence;

namespace AccountingInventory.Infrastructure.Migrations;

[DbContext(typeof(TenantDbContext))]
[Migration("20261118190000_AddTenantSubscribedModules")]
public sealed class AddTenantSubscribedModules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS subscribed_modules character varying(250) DEFAULT 'accounting_inventory,hr_payroll';
            UPDATE tenant.tenants SET subscribed_modules = 'accounting_inventory,hr_payroll' WHERE subscribed_modules IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "subscribed_modules",
            schema: "tenant",
            table: "tenants");
    }
}
