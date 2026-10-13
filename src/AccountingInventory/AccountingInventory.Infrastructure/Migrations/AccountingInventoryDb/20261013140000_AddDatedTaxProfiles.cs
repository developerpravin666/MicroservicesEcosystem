using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261013140000_AddDatedTaxProfiles")]
public sealed class AddDatedTaxProfiles : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE taxes ADD COLUMN IF NOT EXISTS profile_id uuid;
            ALTER TABLE taxes ADD COLUMN IF NOT EXISTS name varchar(100) NOT NULL DEFAULT 'GST';
            ALTER TABLE taxes ADD COLUMN IF NOT EXISTS effective_from date NOT NULL DEFAULT '0001-01-01';
            ALTER TABLE taxes ADD COLUMN IF NOT EXISTS treatment varchar(20) NOT NULL DEFAULT 'Taxable';
            UPDATE taxes SET profile_id = id WHERE profile_id IS NULL;
            ALTER TABLE taxes ALTER COLUMN profile_id SET NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_taxes_profile_id_effective_from ON taxes(profile_id, effective_from) WHERE NOT is_deleted;
            ALTER TABLE product_types ADD COLUMN IF NOT EXISTS default_tax_id uuid;
            ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_id uuid;
            ALTER TABLE products ADD COLUMN IF NOT EXISTS tax_treatment text NOT NULL DEFAULT 'Taxable';
            ALTER TABLE stock_skus ADD COLUMN IF NOT EXISTS default_tax_id uuid;
            ALTER TABLE sku_movements ADD COLUMN IF NOT EXISTS tax_id uuid;
            ALTER TABLE sku_movements ADD COLUMN IF NOT EXISTS tax_treatment text NOT NULL DEFAULT 'Taxable';
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS tax_treatment text NOT NULL DEFAULT 'Taxable';
            ALTER TABLE sales_invoice_lines ADD COLUMN IF NOT EXISTS tax_treatment text NOT NULL DEFAULT 'Taxable';
            ALTER TABLE sales_invoices ADD COLUMN IF NOT EXISTS tax_point_date date;
            ALTER TABLE sales_invoices ADD COLUMN IF NOT EXISTS tax_date_reason text;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM products WHERE tax_id IS NOT NULL)
                OR EXISTS (SELECT 1 FROM taxes GROUP BY profile_id HAVING count(*) > 1)
                OR EXISTS (SELECT 1 FROM sales_invoices WHERE tax_point_date IS NOT NULL)
                OR EXISTS (SELECT 1 FROM product_types WHERE default_tax_id IS NOT NULL)
                OR EXISTS (SELECT 1 FROM stock_skus WHERE default_tax_id IS NOT NULL)
                OR EXISTS (SELECT 1 FROM sku_movements WHERE tax_id IS NOT NULL)
                OR EXISTS (SELECT 1 FROM sales_products WHERE tax_treatment <> 'Taxable')
              THEN RAISE EXCEPTION 'Dated GST history is in use; downgrade would lose financial information'; END IF;
            END $$;
            ALTER TABLE sales_invoices DROP COLUMN tax_point_date, DROP COLUMN tax_date_reason;
            ALTER TABLE sales_invoice_lines DROP COLUMN tax_treatment;
            ALTER TABLE sales_products DROP COLUMN tax_treatment;
            ALTER TABLE sku_movements DROP COLUMN tax_id, DROP COLUMN tax_treatment;
            ALTER TABLE stock_skus DROP COLUMN default_tax_id;
            ALTER TABLE products DROP COLUMN tax_id, DROP COLUMN tax_treatment;
            ALTER TABLE product_types DROP COLUMN default_tax_id;
            DROP INDEX ix_taxes_profile_id_effective_from;
            ALTER TABLE taxes DROP COLUMN profile_id, DROP COLUMN name, DROP COLUMN effective_from, DROP COLUMN treatment;
            """);
    }
}
