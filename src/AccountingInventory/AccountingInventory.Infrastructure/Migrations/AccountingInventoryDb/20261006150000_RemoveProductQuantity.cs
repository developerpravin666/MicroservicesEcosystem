using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261006150000_RemoveProductQuantity")]
public sealed class RemoveProductQuantity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "quantity", table: "products");

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.AddColumn<int>(name: "quantity", table: "products", type: "integer", nullable: false, defaultValue: 1);
}
