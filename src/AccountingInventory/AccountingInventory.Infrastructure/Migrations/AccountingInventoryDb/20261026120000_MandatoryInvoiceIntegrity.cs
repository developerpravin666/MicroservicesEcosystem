using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class MandatoryInvoiceIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_invoice_lines",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<string>(
                name: "hsn_sac",
                table: "sales_invoice_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "unit_of_measure",
                table: "sales_invoice_lines",
                type: "text",
                nullable: false,
                defaultValue: "NOS");

            migrationBuilder.AddColumn<decimal>(
                name: "igst_amount",
                table: "invoice_corrections",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "business_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "customer_advances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_mode = table.Column<string>(type: "text", nullable: false),
                    reference_number = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    remaining_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_advances", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_advances_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "customer_advance_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    advance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    payment_mode = table.Column<string>(type: "text", nullable: false),
                    reference_number = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_advance_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_customer_advance_refunds_customer_advances_advance_id",
                        column: x => x.advance_id,
                        principalTable: "customer_advances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sales_invoice_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    advance_id = table.Column<Guid>(type: "uuid", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_mode = table.Column<string>(type: "text", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference_number = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sales_invoice_receipts", x => x.id);
                    table.ForeignKey(
                        name: "fk_sales_invoice_receipts_customer_advances_advance_id",
                        column: x => x.advance_id,
                        principalTable: "customer_advances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_sales_invoice_receipts_sales_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "sales_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_business_requests_request_key",
                table: "business_requests",
                column: "request_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_customer_advance_refunds_advance_id",
                table: "customer_advance_refunds",
                column: "advance_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_advances_customer_id",
                table: "customer_advances",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_receipts_advance_id",
                table: "sales_invoice_receipts",
                column: "advance_id");

            migrationBuilder.CreateIndex(
                name: "ix_sales_invoice_receipts_invoice_id",
                table: "sales_invoice_receipts",
                column: "invoice_id");
            migrationBuilder.Sql("""
                INSERT INTO customer_advances (id,customer_id,payment_date,payment_mode,amount,remaining_amount,created_at,is_active,is_deleted)
                SELECT e.id,c.id,e.journal_date,
                  CASE WHEN EXISTS (SELECT 1 FROM journal_lines cash JOIN chart_accounts a ON a.id=cash.account_id
                    WHERE cash.journal_entry_id=e.id AND a.code='1000' AND cash.debit>0) THEN 'Cash' ELSE 'Bank' END,
                  sum(l.credit-l.debit),sum(l.credit-l.debit),e.created_at,true,false
                FROM journal_entries e JOIN customers c ON e.source_id=c.id::text
                JOIN journal_lines l ON l.journal_entry_id=e.id JOIN chart_accounts a ON a.id=l.account_id AND a.code='1100'
                WHERE e.source_type='CustomerAdvance'
                GROUP BY e.id,c.id,e.journal_date,e.created_at HAVING sum(l.credit-l.debit)>0;
                UPDATE journal_entries e SET source_id=a.id::text FROM customer_advances a WHERE e.id=a.id AND e.source_type='CustomerAdvance';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM sales_invoice_receipts) OR EXISTS (SELECT 1 FROM customer_advances)
                    OR EXISTS (SELECT 1 FROM business_requests) OR EXISTS (SELECT 1 FROM invoice_corrections WHERE igst_amount <> 0)
                    OR EXISTS (SELECT 1 FROM sales_invoice_lines WHERE hsn_sac IS NOT NULL OR quantity <> round(quantity,2)) THEN
                    RAISE EXCEPTION 'Cannot remove used invoice/payment history; restore a rehearsed backup instead.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "business_requests");

            migrationBuilder.DropTable(
                name: "customer_advance_refunds");

            migrationBuilder.DropTable(
                name: "sales_invoice_receipts");

            migrationBuilder.DropTable(
                name: "customer_advances");

            migrationBuilder.DropColumn(
                name: "hsn_sac",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "unit_of_measure",
                table: "sales_invoice_lines");

            migrationBuilder.DropColumn(
                name: "igst_amount",
                table: "invoice_corrections");

            migrationBuilder.AlterColumn<decimal>(
                name: "quantity",
                table: "sales_invoice_lines",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);
        }
    }
}
