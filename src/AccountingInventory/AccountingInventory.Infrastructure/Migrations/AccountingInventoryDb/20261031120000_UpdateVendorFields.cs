using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261031120000_UpdateVendorFields")]
public sealed class UpdateVendorFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS ix_vendors_email;
            DROP INDEX IF EXISTS ix_vendors_mobile;
            ALTER TABLE vendors ALTER COLUMN mobile DROP NOT NULL;
            ALTER TABLE vendors ALTER COLUMN email DROP NOT NULL;
            ALTER TABLE invoice_snapshots ALTER COLUMN party_mobile DROP NOT NULL;
            ALTER TABLE invoice_snapshots ALTER COLUMN party_address DROP NOT NULL;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE vendors ALTER COLUMN mobile SET NOT NULL;
            ALTER TABLE vendors ALTER COLUMN email SET NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_vendors_email ON vendors (email);
            CREATE UNIQUE INDEX IF NOT EXISTS ix_vendors_mobile ON vendors (mobile);
            ALTER TABLE invoice_snapshots ALTER COLUMN party_mobile SET NOT NULL;
            ALTER TABLE invoice_snapshots ALTER COLUMN party_address SET NOT NULL;
            """);
    }
}

