using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261005120000_EnsureOneActiveSalePerProduct")]
public sealed class EnsureOneActiveSalePerProduct : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE products AS product
            SET quantity = GREATEST(0, product.quantity - (
                SELECT COUNT(*)::integer
                FROM sales_products AS sale
                WHERE sale.is_deleted = false
                  AND lower(sale.product_id) = product.id::text
            ))
            """);

        migrationBuilder.CreateIndex(
            name: "ix_sales_products_product_id",
            table: "sales_products",
            column: "product_id",
            unique: true,
            filter: "is_deleted = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "ix_sales_products_product_id", table: "sales_products");
        migrationBuilder.Sql("""
            UPDATE products AS product
            SET quantity = product.quantity + (
                SELECT COUNT(*)::integer
                FROM sales_products AS sale
                WHERE sale.is_deleted = false
                  AND lower(sale.product_id) = product.id::text
            )
            """);
    }
}
