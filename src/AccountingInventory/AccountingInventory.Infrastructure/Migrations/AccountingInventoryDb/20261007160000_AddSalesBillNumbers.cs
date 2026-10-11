using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261007160000_AddSalesBillNumbers")]
public sealed class AddSalesBillNumbers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS bill_number character varying(32) NULL;

            WITH numbered_sales AS (
                SELECT id,
                       EXTRACT(YEAR FROM sale_date)::integer AS bill_year,
                       ROW_NUMBER() OVER (
                           PARTITION BY EXTRACT(YEAR FROM sale_date)
                           ORDER BY sale_date, created_at, id) AS bill_sequence
                FROM sales_products
                WHERE bill_number IS NULL
            )
            UPDATE sales_products AS sale
            SET bill_number = 'SM-' || numbered_sales.bill_year::text || '-' ||
                LPAD(numbered_sales.bill_sequence::text, 6, '0')
            FROM numbered_sales
            WHERE sale.id = numbered_sales.id AND sale.bill_number IS NULL;

            CREATE TABLE IF NOT EXISTS sales_bill_counters (
                bill_year integer PRIMARY KEY,
                last_number integer NOT NULL
            );

            INSERT INTO sales_bill_counters (bill_year, last_number)
            SELECT EXTRACT(YEAR FROM sale_date)::integer, COUNT(*)::integer
            FROM sales_products
            GROUP BY EXTRACT(YEAR FROM sale_date)::integer
            ON CONFLICT (bill_year) DO UPDATE
                SET last_number = GREATEST(sales_bill_counters.last_number, EXCLUDED.last_number);

            CREATE OR REPLACE FUNCTION generate_sales_bill_number(target_year integer)
            RETURNS text
            LANGUAGE sql
            AS $$
                INSERT INTO sales_bill_counters (bill_year, last_number)
                VALUES (target_year, 1)
                ON CONFLICT (bill_year) DO UPDATE
                    SET last_number = sales_bill_counters.last_number + 1
                RETURNING 'SM-' || target_year::text || '-' ||
                    CASE WHEN last_number < 1000000
                        THEN LPAD(last_number::text, 6, '0')
                        ELSE last_number::text
                    END;
            $$;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE sales_products ALTER COLUMN bill_number SET NOT NULL;
            CREATE UNIQUE INDEX IF NOT EXISTS ix_sales_products_bill_number
                ON sales_products (bill_number);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex("ix_sales_products_bill_number", "sales_products");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS generate_sales_bill_number(integer); DROP TABLE IF EXISTS sales_bill_counters;");
        migrationBuilder.DropColumn("bill_number", "sales_products");
    }
}
