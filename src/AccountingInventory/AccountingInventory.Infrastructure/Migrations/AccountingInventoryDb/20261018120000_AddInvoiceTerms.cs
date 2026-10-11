using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261018120000_AddInvoiceTerms")]
public sealed class AddInvoiceTerms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tolerate columns created by a prior/manual update when this migration
        // is still pending. Existing non-null values are preserved.
        migrationBuilder.Sql("""
            ALTER TABLE products ADD COLUMN IF NOT EXISTS payment_terms_days integer NOT NULL DEFAULT 0;
            ALTER TABLE products ADD COLUMN IF NOT EXISTS due_date date NULL;
            UPDATE products SET payment_terms_days = 0 WHERE payment_terms_days IS NULL;
            UPDATE products SET due_date = purchase_date WHERE due_date IS NULL;
            ALTER TABLE products ALTER COLUMN payment_terms_days SET DEFAULT 0;
            ALTER TABLE products ALTER COLUMN payment_terms_days SET NOT NULL;
            ALTER TABLE products ALTER COLUMN due_date SET NOT NULL;

            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS payment_terms_days integer NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS due_date date NULL;
            UPDATE sales_products SET payment_terms_days = 0 WHERE payment_terms_days IS NULL;
            UPDATE sales_products SET due_date = sale_date WHERE due_date IS NULL;
            ALTER TABLE sales_products ALTER COLUMN payment_terms_days SET DEFAULT 0;
            ALTER TABLE sales_products ALTER COLUMN payment_terms_days SET NOT NULL;
            ALTER TABLE sales_products ALTER COLUMN due_date SET NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "due_date", table: "products");
        migrationBuilder.DropColumn(name: "payment_terms_days", table: "products");
        migrationBuilder.DropColumn(name: "due_date", table: "sales_products");
        migrationBuilder.DropColumn(name: "payment_terms_days", table: "sales_products");
    }
}
