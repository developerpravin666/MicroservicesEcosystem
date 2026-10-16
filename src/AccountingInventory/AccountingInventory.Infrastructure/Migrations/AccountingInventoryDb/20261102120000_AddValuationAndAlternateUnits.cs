using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261102120000_AddValuationAndAlternateUnits")]
public sealed class AddValuationAndAlternateUnits : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS unit_of_measures (
                id uuid NOT NULL,
                code character varying(20) NOT NULL,
                name character varying(100) NOT NULL,
                symbol character varying(20) NOT NULL,
                is_base_unit boolean NOT NULL DEFAULT true,
                base_unit_id uuid,
                conversion_rate numeric(18,4),
                description character varying(300),
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_unit_of_measures PRIMARY KEY (id),
                CONSTRAINT fk_unit_of_measures_base_unit FOREIGN KEY (base_unit_id) REFERENCES unit_of_measures (id) ON DELETE RESTRICT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_unit_of_measures_code ON unit_of_measures (code);

            -- Seed standard Indian UOM masters if not present
            INSERT INTO unit_of_measures (id, code, name, symbol, is_base_unit, base_unit_id, conversion_rate, description, is_active, is_deleted, created_at)
            VALUES 
                ('10000000-0000-0000-0000-000000000001', 'PCS', 'Pieces', 'pcs', true, NULL, NULL, 'Base unit: Pieces', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000002', 'NAG', 'Numbers / Nag', 'nag', true, NULL, NULL, 'Base unit: Nag', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000003', 'NOS', 'Numbers', 'nos', true, NULL, NULL, 'Base unit: Numbers', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000004', 'SET', 'Set', 'set', true, NULL, NULL, 'Base unit: Set', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000005', 'KGS', 'Kilograms', 'kg', true, NULL, NULL, 'Base unit: Kilograms', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000006', 'MTR', 'Meters', 'm', true, NULL, NULL, 'Base unit: Meters', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000007', 'BOX', 'Box (10 Pcs)', 'box', false, '10000000-0000-0000-0000-000000000001', 10.0000, 'Compound unit: 1 Box = 10 Pcs', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000008', 'PKT', 'Packet (10 Pcs)', 'pkt', false, '10000000-0000-0000-0000-000000000001', 10.0000, 'Compound unit: 1 Packet = 10 Pcs', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000009', 'DOZ', 'Dozen (12 Pcs)', 'doz', false, '10000000-0000-0000-0000-000000000001', 12.0000, 'Compound unit: 1 Dozen = 12 Pcs', true, false, NOW()),
                ('10000000-0000-0000-0000-000000000010', 'CTN', 'Carton (50 Pcs)', 'ctn', false, '10000000-0000-0000-0000-000000000001', 50.0000, 'Compound unit: 1 Carton = 50 Pcs', true, false, NOW())
            ON CONFLICT (code) DO NOTHING;

            ALTER TABLE stock_skus ADD COLUMN IF NOT EXISTS alternate_unit character varying(20);
            ALTER TABLE stock_skus ADD COLUMN IF NOT EXISTS conversion_factor numeric(18,4);

            ALTER TABLE sales_invoice_lines ADD COLUMN IF NOT EXISTS alternate_unit character varying(20);
            ALTER TABLE sales_invoice_lines ADD COLUMN IF NOT EXISTS alternate_quantity numeric(18,4);
            ALTER TABLE sales_invoice_lines ADD COLUMN IF NOT EXISTS conversion_factor numeric(18,4);
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE sales_invoice_lines DROP COLUMN IF EXISTS alternate_unit;
            ALTER TABLE sales_invoice_lines DROP COLUMN IF EXISTS alternate_quantity;
            ALTER TABLE sales_invoice_lines DROP COLUMN IF EXISTS conversion_factor;

            ALTER TABLE stock_skus DROP COLUMN IF EXISTS alternate_unit;
            ALTER TABLE stock_skus DROP COLUMN IF EXISTS conversion_factor;

            DROP TABLE IF EXISTS unit_of_measures;
        """);
    }
}

