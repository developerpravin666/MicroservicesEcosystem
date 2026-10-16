using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261104120000_AddRcmSelfInvoice")]
public sealed class AddRcmSelfInvoice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS rcm_self_invoices (
                id uuid NOT NULL,
                voucher_number character varying(50) NOT NULL,
                payment_voucher_number character varying(50) NOT NULL,
                voucher_date date NOT NULL,
                vendor_id uuid NOT NULL,
                vendor_name character varying(150) NOT NULL,
                vendor_gstin character varying(20),
                section character varying(20) NOT NULL DEFAULT '9(4)',
                service_category character varying(100) NOT NULL,
                supplier_invoice_number character varying(100),
                supplier_invoice_date date,
                supply_type integer NOT NULL DEFAULT 0,
                place_of_supply_state_code character varying(10) NOT NULL,
                place_of_supply_state_name character varying(100) NOT NULL,
                hsn_sac_code character varying(20) NOT NULL DEFAULT '9965',
                item_description character varying(300) NOT NULL,
                taxable_amount numeric(18,2) NOT NULL,
                gst_rate numeric(6,2) NOT NULL,
                cgst_rate numeric(6,2) NOT NULL,
                sgst_rate numeric(6,2) NOT NULL,
                igst_rate numeric(6,2) NOT NULL,
                cgst_amount numeric(18,2) NOT NULL,
                sgst_amount numeric(18,2) NOT NULL,
                igst_amount numeric(18,2) NOT NULL,
                total_tax_amount numeric(18,2) NOT NULL,
                total_amount numeric(18,2) NOT NULL,
                is_itc_eligible boolean NOT NULL DEFAULT true,
                itc_claimed_date date,
                payment_mode character varying(30) NOT NULL DEFAULT 'Bank',
                payment_reference character varying(100),
                bank_account_id uuid,
                journal_entry_id uuid,
                status character varying(20) NOT NULL DEFAULT 'Active',
                cancel_reason character varying(300),
                cancel_date timestamp with time zone,
                notes character varying(500),
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_rcm_self_invoices PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_rcm_self_invoices_voucher_number ON rcm_self_invoices (voucher_number);
            CREATE INDEX IF NOT EXISTS ix_rcm_self_invoices_voucher_date ON rcm_self_invoices (voucher_date);
            CREATE INDEX IF NOT EXISTS ix_rcm_self_invoices_vendor_id ON rcm_self_invoices (vendor_id);
            CREATE INDEX IF NOT EXISTS ix_rcm_self_invoices_status ON rcm_self_invoices (status);
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS rcm_self_invoices;");
    }
}

