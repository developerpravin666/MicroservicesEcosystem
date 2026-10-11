using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

public sealed partial class AddCustomersAndSalesCustomerId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS customers (
                id uuid NOT NULL PRIMARY KEY,
                name character varying(200) NOT NULL,
                mobile character varying(20) NOT NULL,
                address character varying(500) NOT NULL,
                email character varying(256) NULL,
                created_at timestamp with time zone NOT NULL,
                created_by uuid NULL,
                modified_at timestamp with time zone NULL,
                modified_by uuid NULL,
                is_active boolean NOT NULL,
                is_deleted boolean NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_customers_mobile ON customers (mobile);
            """);
        migrationBuilder.Sql("""
            INSERT INTO customers (id, name, mobile, address, email, created_at, created_by, modified_at, modified_by, is_active, is_deleted)
            SELECT DISTINCT ON (customer_mobile) gen_random_uuid(), customer_name, customer_mobile, customer_address,
                   NULL, now(), NULL, NULL, NULL, TRUE, FALSE
            FROM sales_products ORDER BY customer_mobile, sale_date, id
            ON CONFLICT (mobile) DO NOTHING
            """);

        migrationBuilder.Sql("ALTER TABLE sales_products ADD COLUMN IF NOT EXISTS customer_id uuid NULL;");
        migrationBuilder.Sql("""
            UPDATE sales_products AS sale SET customer_id = customer.id
            FROM customers AS customer WHERE customer.mobile = sale.customer_mobile AND sale.customer_id IS NULL
            """);
        migrationBuilder.Sql("""
            ALTER TABLE sales_products ALTER COLUMN customer_id SET NOT NULL;
            CREATE INDEX IF NOT EXISTS ix_sales_products_customer_id ON sales_products (customer_id);
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'fk_sales_products_customers_customer_id'
                      AND conrelid = 'sales_products'::regclass
                ) THEN
                    ALTER TABLE sales_products
                        ADD CONSTRAINT fk_sales_products_customers_customer_id
                        FOREIGN KEY (customer_id) REFERENCES customers (id) ON DELETE RESTRICT;
                END IF;
            END $$;
            """);

        migrationBuilder.Sql("""
            ALTER TABLE sales_products
                DROP COLUMN IF EXISTS customer_name,
                DROP COLUMN IF EXISTS customer_mobile,
                DROP COLUMN IF EXISTS customer_address;
            DROP INDEX IF EXISTS ix_sales_products_product_id;
            """);
        ReorderSalesProductAuditColumns(migrationBuilder);
        migrationBuilder.CreateIndex("ix_sales_products_product_id", "sales_products", "product_id", unique: true, filter: "is_deleted = false");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("fk_sales_products_customers_customer_id", "sales_products");
        migrationBuilder.DropIndex("ix_sales_products_customer_id", "sales_products");
        migrationBuilder.AddColumn<string>("customer_name", "sales_products", type: "character varying(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<string>("customer_mobile", "sales_products", type: "character varying(20)", maxLength: 20, nullable: true);
        migrationBuilder.AddColumn<string>("customer_address", "sales_products", type: "character varying(500)", maxLength: 500, nullable: true);
        migrationBuilder.Sql("""
            UPDATE sales_products AS sale SET customer_name = customer.name, customer_mobile = customer.mobile, customer_address = customer.address
            FROM customers AS customer WHERE customer.id = sale.customer_id
            """);
        migrationBuilder.AlterColumn<string>("customer_name", "sales_products", type: "character varying(200)", maxLength: 200, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(200)", oldMaxLength: 200, oldNullable: true);
        migrationBuilder.AlterColumn<string>("customer_mobile", "sales_products", type: "character varying(20)", maxLength: 20, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(20)", oldMaxLength: 20, oldNullable: true);
        migrationBuilder.AlterColumn<string>("customer_address", "sales_products", type: "character varying(500)", maxLength: 500, nullable: false,
            oldClrType: typeof(string), oldType: "character varying(500)", oldMaxLength: 500, oldNullable: true);
        migrationBuilder.DropColumn("customer_id", "sales_products");
        migrationBuilder.DropIndex("ix_sales_products_product_id", "sales_products");
        ReorderSalesProductAuditColumns(migrationBuilder);
        migrationBuilder.CreateIndex("ix_sales_products_product_id", "sales_products", "product_id", unique: true, filter: "is_deleted = false");
        migrationBuilder.DropTable("customers");
    }

    private static void ReorderSalesProductAuditColumns(MigrationBuilder migrationBuilder)
        => migrationBuilder.Sql("""
            ALTER TABLE sales_products ADD COLUMN _reorder_created_at timestamp with time zone;
            ALTER TABLE sales_products ADD COLUMN _reorder_created_by uuid;
            ALTER TABLE sales_products ADD COLUMN _reorder_modified_at timestamp with time zone;
            ALTER TABLE sales_products ADD COLUMN _reorder_modified_by uuid;
            ALTER TABLE sales_products ADD COLUMN _reorder_is_active boolean;
            ALTER TABLE sales_products ADD COLUMN _reorder_is_deleted boolean;
            UPDATE sales_products SET
                _reorder_created_at = created_at, _reorder_created_by = created_by,
                _reorder_modified_at = modified_at, _reorder_modified_by = modified_by,
                _reorder_is_active = is_active, _reorder_is_deleted = is_deleted;
            ALTER TABLE sales_products DROP COLUMN created_at, DROP COLUMN created_by,
                DROP COLUMN modified_at, DROP COLUMN modified_by, DROP COLUMN is_active, DROP COLUMN is_deleted;
            ALTER TABLE sales_products ADD COLUMN created_at timestamp with time zone NULL;
            ALTER TABLE sales_products ADD COLUMN created_by uuid NULL;
            ALTER TABLE sales_products ADD COLUMN modified_at timestamp with time zone NULL;
            ALTER TABLE sales_products ADD COLUMN modified_by uuid NULL;
            ALTER TABLE sales_products ADD COLUMN is_active boolean NULL;
            ALTER TABLE sales_products ADD COLUMN is_deleted boolean NULL;
            UPDATE sales_products SET
                created_at = _reorder_created_at, created_by = _reorder_created_by,
                modified_at = _reorder_modified_at, modified_by = _reorder_modified_by,
                is_active = _reorder_is_active, is_deleted = _reorder_is_deleted;
            ALTER TABLE sales_products ALTER COLUMN created_at SET NOT NULL;
            ALTER TABLE sales_products ALTER COLUMN is_active SET NOT NULL;
            ALTER TABLE sales_products ALTER COLUMN is_deleted SET NOT NULL;
            ALTER TABLE sales_products DROP COLUMN _reorder_created_at, DROP COLUMN _reorder_created_by,
                DROP COLUMN _reorder_modified_at, DROP COLUMN _reorder_modified_by,
                DROP COLUMN _reorder_is_active, DROP COLUMN _reorder_is_deleted;
            """);
}
