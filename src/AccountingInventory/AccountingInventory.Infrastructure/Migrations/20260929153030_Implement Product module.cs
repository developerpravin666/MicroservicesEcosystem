using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class ImplementProductmodule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (
                        SELECT 1 FROM information_schema.tables 
                        WHERE table_schema = current_schema() AND table_name = 'products'
                    ) THEN
                        CREATE TABLE products (
                            id uuid NOT NULL,
                            vendor_id uuid NOT NULL,
                            brand_id uuid NOT NULL,
                            product_type_id uuid NOT NULL,
                            product_model_id uuid NOT NULL,
                            variant_id uuid NOT NULL,
                            color_id uuid NOT NULL,
                            serial_number character varying(100) NOT NULL,
                            serial_number1 character varying(100),
                            quantity integer NOT NULL,
                            purchase_price numeric(18,2) NOT NULL,
                            discount numeric(18,2) NOT NULL,
                            cgst numeric(5,2) NOT NULL,
                            sgst numeric(5,2) NOT NULL,
                            tax numeric(5,2) NOT NULL,
                            created_at timestamp with time zone NOT NULL,
                            created_by uuid,
                            modified_at timestamp with time zone,
                            modified_by uuid,
                            is_active boolean NOT NULL,
                            is_deleted boolean NOT NULL,
                            CONSTRAINT pk_products PRIMARY KEY (id)
                        );
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql("CREATE UNIQUE INDEX IF NOT EXISTS ix_products_serial_number ON products (serial_number);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "products");
        }
    }
}
