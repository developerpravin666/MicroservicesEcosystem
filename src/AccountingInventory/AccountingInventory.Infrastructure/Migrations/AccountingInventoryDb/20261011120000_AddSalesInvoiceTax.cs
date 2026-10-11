using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261011120000_AddSalesInvoiceTax")]
public sealed class AddSalesInvoiceTax : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Some databases may already have a subset of these columns from a
        // previous/manual schema update while this migration is still pending.
        // IF NOT EXISTS preserves those values and lets the migration finish.
        migrationBuilder.Sql("""
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS cgst_rate numeric(5,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS sgst_rate numeric(5,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS taxable_amount numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS cgst_amount numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS sgst_amount numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS total_amount numeric(18,2) NOT NULL DEFAULT 0;
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS tax_id uuid NULL;

            CREATE INDEX IF NOT EXISTS ix_sales_products_tax_id ON sales_products (tax_id);

            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM pg_constraint
                    WHERE conname = 'fk_sales_products_taxes_tax_id'
                      AND conrelid = 'sales_products'::regclass
                ) THEN
                    ALTER TABLE sales_products
                        ADD CONSTRAINT fk_sales_products_taxes_tax_id
                        FOREIGN KEY (tax_id) REFERENCES taxes (id) ON DELETE RESTRICT;
                END IF;
            END $$;
            """);
        migrationBuilder.Sql("UPDATE sales_products SET taxable_amount = GREATEST(selling_price - discount, 0), total_amount = GREATEST(selling_price - discount, 0)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_sales_products_taxes_tax_id", "sales_products");
        migrationBuilder.DropIndex("ix_sales_products_tax_id", "sales_products");
        migrationBuilder.DropColumn("cgst_rate", "sales_products");
        migrationBuilder.DropColumn("sgst_rate", "sales_products");
        migrationBuilder.DropColumn("taxable_amount", "sales_products");
        migrationBuilder.DropColumn("cgst_amount", "sales_products");
        migrationBuilder.DropColumn("sgst_amount", "sales_products");
        migrationBuilder.DropColumn("total_amount", "sales_products");
        migrationBuilder.DropColumn("tax_id", "sales_products");
    }
}
