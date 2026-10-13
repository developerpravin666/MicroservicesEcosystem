using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261013120000_AddProductHsnSac")]
public sealed class AddProductHsnSac : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE product_types ADD COLUMN IF NOT EXISTS hsn_sac varchar(8) NULL;");
        migrationBuilder.Sql("ALTER TABLE products ADD COLUMN IF NOT EXISTS hsn_sac varchar(8) NULL;");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("hsn_sac", "products");
        migrationBuilder.DropColumn("hsn_sac", "product_types");
    }
}
