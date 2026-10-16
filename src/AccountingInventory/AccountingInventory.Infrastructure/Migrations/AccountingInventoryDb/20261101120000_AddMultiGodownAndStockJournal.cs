using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261101120000_AddMultiGodownAndStockJournal")]
public sealed class AddMultiGodownAndStockJournal : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS godowns (
                id uuid NOT NULL,
                name character varying(100) NOT NULL,
                code character varying(20) NOT NULL,
                address character varying(500),
                is_primary boolean NOT NULL DEFAULT false,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_godowns PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_godowns_code ON godowns (code);

            ALTER TABLE products ADD COLUMN IF NOT EXISTS godown_id uuid;

            CREATE TABLE IF NOT EXISTS stock_balances (
                id uuid NOT NULL,
                godown_id uuid NOT NULL,
                product_id uuid,
                sku_id uuid,
                item_type character varying(20) NOT NULL,
                serial_number character varying(100),
                quantity numeric(18,4) NOT NULL,
                last_movement_date date NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_stock_balances PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_stock_balances_godown_id ON stock_balances (godown_id);
            CREATE INDEX IF NOT EXISTS ix_stock_balances_godown_id_product_id ON stock_balances (godown_id, product_id);
            CREATE INDEX IF NOT EXISTS ix_stock_balances_godown_id_sku_id ON stock_balances (godown_id, sku_id);
            CREATE INDEX IF NOT EXISTS ix_stock_balances_serial_number ON stock_balances (serial_number);

            CREATE TABLE IF NOT EXISTS stock_transfer_journals (
                id uuid NOT NULL,
                transfer_number character varying(50) NOT NULL,
                transfer_date date NOT NULL,
                from_godown_id uuid NOT NULL,
                to_godown_id uuid NOT NULL,
                vehicle_number character varying(50),
                dispatch_note character varying(100),
                transporter_name character varying(100),
                narration character varying(500),
                status character varying(30) NOT NULL,
                total_quantity numeric(18,4) NOT NULL,
                total_value numeric(18,2) NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_stock_transfer_journals PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_stock_transfer_journals_transfer_number ON stock_transfer_journals (transfer_number);
            CREATE INDEX IF NOT EXISTS ix_stock_transfer_journals_transfer_date ON stock_transfer_journals (transfer_date);
            CREATE INDEX IF NOT EXISTS ix_stock_transfer_journals_from_godown_id ON stock_transfer_journals (from_godown_id);
            CREATE INDEX IF NOT EXISTS ix_stock_transfer_journals_to_godown_id ON stock_transfer_journals (to_godown_id);

            CREATE TABLE IF NOT EXISTS stock_transfer_lines (
                id uuid NOT NULL,
                stock_transfer_journal_id uuid NOT NULL,
                item_type character varying(20) NOT NULL,
                product_id uuid,
                sku_id uuid,
                item_name character varying(200) NOT NULL,
                serial_number character varying(100),
                quantity numeric(18,4) NOT NULL,
                unit_cost numeric(18,2) NOT NULL,
                total_value numeric(18,2) NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_stock_transfer_lines PRIMARY KEY (id),
                CONSTRAINT fk_stock_transfer_lines_stock_transfer_journals_stock_transfer_journal_id
                    FOREIGN KEY (stock_transfer_journal_id) REFERENCES stock_transfer_journals (id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS ix_stock_transfer_lines_stock_transfer_journal_id ON stock_transfer_lines (stock_transfer_journal_id);
            CREATE INDEX IF NOT EXISTS ix_stock_transfer_lines_product_id ON stock_transfer_lines (product_id);
            CREATE INDEX IF NOT EXISTS ix_stock_transfer_lines_sku_id ON stock_transfer_lines (sku_id);

            CREATE TABLE IF NOT EXISTS stock_adjustments (
                id uuid NOT NULL,
                adjustment_number character varying(50) NOT NULL,
                adjustment_date date NOT NULL,
                godown_id uuid NOT NULL,
                adjustment_type character varying(20) NOT NULL,
                reason character varying(300) NOT NULL,
                item_type character varying(20) NOT NULL,
                product_id uuid,
                sku_id uuid,
                item_name character varying(200) NOT NULL,
                serial_number character varying(100),
                book_quantity numeric(18,4) NOT NULL,
                physical_quantity numeric(18,4) NOT NULL,
                discrepancy_quantity numeric(18,4) NOT NULL,
                unit_cost numeric(18,2) NOT NULL,
                total_value numeric(18,2) NOT NULL,
                journal_entry_id uuid,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_stock_adjustments PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_stock_adjustments_adjustment_number ON stock_adjustments (adjustment_number);
            CREATE INDEX IF NOT EXISTS ix_stock_adjustments_adjustment_date ON stock_adjustments (adjustment_date);
            CREATE INDEX IF NOT EXISTS ix_stock_adjustments_godown_id ON stock_adjustments (godown_id);
            CREATE INDEX IF NOT EXISTS ix_stock_adjustments_product_id ON stock_adjustments (product_id);
            CREATE INDEX IF NOT EXISTS ix_stock_adjustments_sku_id ON stock_adjustments (sku_id);

            -- Seed default primary godown for existing tenant schemas if none exists
            INSERT INTO godowns (id, name, code, address, is_primary, is_active, is_deleted, created_at)
            SELECT gen_random_uuid(), 'Main Store / Warehouse', 'MAIN', 'Default Primary Godown', true, true, false, NOW()
            WHERE NOT EXISTS (SELECT 1 FROM godowns WHERE is_primary = true);
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS stock_adjustments;
            DROP TABLE IF EXISTS stock_transfer_lines;
            DROP TABLE IF EXISTS stock_transfer_journals;
            DROP TABLE IF EXISTS stock_balances;
            ALTER TABLE products DROP COLUMN IF EXISTS godown_id;
            DROP TABLE IF EXISTS godowns;
            """);
    }
}

