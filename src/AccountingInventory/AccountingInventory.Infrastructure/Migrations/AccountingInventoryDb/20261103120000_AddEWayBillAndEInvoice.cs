using System;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb;

[DbContext(typeof(AccountingInventoryDbContext))]
[Migration("20261103120000_AddEWayBillAndEInvoice")]
public sealed class AddEWayBillAndEInvoice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS eway_bills (
                id uuid NOT NULL,
                sales_invoice_id uuid NOT NULL,
                ewb_number character varying(20),
                ewb_date timestamp with time zone,
                valid_upto timestamp with time zone,
                doc_type character varying(10) NOT NULL DEFAULT 'INV',
                supply_type character varying(10) NOT NULL DEFAULT 'O',
                sub_supply_type character varying(10) NOT NULL DEFAULT '1',
                trans_mode integer NOT NULL DEFAULT 1,
                distance_km integer NOT NULL,
                transporter_id character varying(30),
                transporter_name character varying(150),
                trans_doc_no character varying(50),
                trans_doc_date date,
                vehicle_no character varying(30),
                vehicle_type character varying(10) NOT NULL DEFAULT 'R',
                status character varying(20) NOT NULL DEFAULT 'Generated',
                cancel_reason character varying(250),
                cancel_date timestamp with time zone,
                payload_json text NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_eway_bills PRIMARY KEY (id),
                CONSTRAINT fk_eway_bills_sales_invoices FOREIGN KEY (sales_invoice_id) REFERENCES sales_invoices (id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_eway_bills_sales_invoice_id ON eway_bills (sales_invoice_id);
            CREATE INDEX IF NOT EXISTS ix_eway_bills_ewb_number ON eway_bills (ewb_number);

            CREATE TABLE IF NOT EXISTS einvoices (
                id uuid NOT NULL,
                sales_invoice_id uuid NOT NULL,
                irn character varying(64) NOT NULL,
                ack_no character varying(30) NOT NULL,
                ack_date timestamp with time zone NOT NULL,
                signed_qr_code text NOT NULL,
                signed_invoice text,
                status character varying(20) NOT NULL DEFAULT 'Generated',
                cancel_reason character varying(250),
                cancel_date timestamp with time zone,
                payload_json text NOT NULL,
                is_active boolean NOT NULL DEFAULT true,
                is_deleted boolean NOT NULL DEFAULT false,
                created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                CONSTRAINT pk_einvoices PRIMARY KEY (id),
                CONSTRAINT fk_einvoices_sales_invoices FOREIGN KEY (sales_invoice_id) REFERENCES sales_invoices (id) ON DELETE RESTRICT
            );

            CREATE INDEX IF NOT EXISTS ix_einvoices_sales_invoice_id ON einvoices (sales_invoice_id);
            CREATE UNIQUE INDEX IF NOT EXISTS ix_einvoices_irn ON einvoices (irn);
            CREATE INDEX IF NOT EXISTS ix_einvoices_ack_no ON einvoices (ack_no);
        """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS einvoices;
            DROP TABLE IF EXISTS eway_bills;
        """);
    }
}

