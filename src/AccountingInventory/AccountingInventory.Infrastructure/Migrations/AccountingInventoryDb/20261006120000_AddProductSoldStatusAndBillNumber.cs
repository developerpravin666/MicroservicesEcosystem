using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

public sealed partial class AddProductSoldStatusAndBillNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE products ADD COLUMN IF NOT EXISTS bill_number character varying(100) NULL;
            ALTER TABLE products ADD COLUMN IF NOT EXISTS is_sold boolean NOT NULL DEFAULT FALSE;

            -- Older sales reduced quantity to zero. Restore only products not
            -- already marked sold, so retrying a partially applied migration
            -- cannot increment stock a second time.
            UPDATE products AS product
            SET is_sold = TRUE,
                quantity = product.quantity + 1
            WHERE EXISTS (
                SELECT 1 FROM sales_products AS sale
                WHERE sale.is_deleted = FALSE
                  AND lower(sale.product_id) = product.id::text
            )
              AND product.is_sold = FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE products AS product
            SET quantity = GREATEST(0, product.quantity - 1)
            WHERE EXISTS (
                SELECT 1 FROM sales_products AS sale
                WHERE sale.is_deleted = FALSE
                  AND lower(sale.product_id) = product.id::text
            )
            """);
        migrationBuilder.DropColumn(name: "bill_number", table: "products");
        migrationBuilder.DropColumn(name: "is_sold", table: "products");
    }
}
