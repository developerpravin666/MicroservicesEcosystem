using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Migrations.AccountingInventoryDb
{
    /// <inheritdoc />
    public partial class AccountingP0Workflows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sales_products_product_id",
                table: "sales_products");

            migrationBuilder.AddColumn<bool>(
                name: "email_verified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "invitation_expires_at",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "invitation_hash",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_owner",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_returned",
                table: "sales_products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "invoice_corrections",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bill_number = table.Column<string>(type: "text", nullable: false),
                    note_number = table.Column<string>(type: "text", nullable: false),
                    note_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    disposition = table.Column<string>(type: "text", nullable: false),
                    taxable_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cgst_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sgst_rate = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    cgst_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    sgst_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_corrections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    party_name = table.Column<string>(type: "text", nullable: false),
                    party_mobile = table.Column<string>(type: "text", nullable: false),
                    party_address = table.Column<string>(type: "text", nullable: false),
                    party_email = table.Column<string>(type: "text", nullable: true),
                    product_name = table.Column<string>(type: "text", nullable: false),
                    serial_number = table.Column<string>(type: "text", nullable: false),
                    details_json = table.Column<string>(type: "jsonb", nullable: false),
                    reconstructed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "opening_subledger_balances",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    journal_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cutover_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_subledger_balances", x => x.id);
                    table.ForeignKey(
                        name: "fk_opening_subledger_balances_journal_entries_journal_entry_id",
                        column: x => x.journal_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "correction_refunds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_mode = table.Column<string>(type: "text", nullable: false),
                    reference = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_correction_refunds", x => x.id);
                    table.ForeignKey(
                        name: "fk_correction_refunds_invoice_corrections_correction_id",
                        column: x => x.correction_id,
                        principalTable: "invoice_corrections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "opening_settlements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    opening_balance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    payment_mode = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_settlements", x => x.id);
                    table.ForeignKey(
                        name: "fk_opening_settlements_opening_subledger_balances_opening_bala",
                        column: x => x.opening_balance_id,
                        principalTable: "opening_subledger_balances",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_is_owner",
                table: "users",
                column: "is_owner",
                unique: true,
                filter: "is_owner = true");

            migrationBuilder.CreateIndex(
                name: "ix_sales_products_product_id",
                table: "sales_products",
                column: "product_id",
                unique: true,
                filter: "is_deleted = false AND is_returned = false");

            migrationBuilder.CreateIndex(
                name: "ix_correction_refunds_correction_id",
                table: "correction_refunds",
                column: "correction_id");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_corrections_kind_source_id",
                table: "invoice_corrections",
                columns: new[] { "kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_corrections_note_number",
                table: "invoice_corrections",
                column: "note_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoice_snapshots_kind_source_id",
                table: "invoice_snapshots",
                columns: new[] { "kind", "source_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opening_settlements_opening_balance_id",
                table: "opening_settlements",
                column: "opening_balance_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_subledger_balances_journal_entry_id",
                table: "opening_subledger_balances",
                column: "journal_entry_id");

            migrationBuilder.CreateIndex(
                name: "ix_opening_subledger_balances_kind_party_id_reference",
                table: "opening_subledger_balances",
                columns: new[] { "kind", "party_id", "reference" },
                unique: true);
            migrationBuilder.Sql("""
                -- Preserve the previous owner decision once; never derive ownership from user ordering again.
                UPDATE users SET is_owner = true WHERE id = (
                    SELECT id FROM users WHERE is_deleted = false ORDER BY created_at, id LIMIT 1);
                -- Existing accounts are grandfathered. New accounts must verify an email invitation.
                UPDATE users SET email_verified = true WHERE is_deleted = false;
                INSERT INTO invoice_snapshots (id, kind, source_id, party_name, party_mobile, party_address, party_email,
                    product_name, serial_number, details_json, reconstructed, created_at, is_active, is_deleted)
                SELECT gen_random_uuid(), 'Purchase', p.id, v.name, v.mobile, COALESCE(v.address,''), v.email,
                    concat_ws(' - ', b.name, m.name, vr.name, c.name) || ' — ' || p.serial_number,
                    p.serial_number, to_jsonb(p), true, now(), true, false
                FROM products p JOIN vendors v ON v.id=p.vendor_id
                LEFT JOIN brands b ON b.id=p.brand_id LEFT JOIN product_models m ON m.id=p.product_model_id
                LEFT JOIN variants vr ON vr.id=p.variant_id LEFT JOIN colors c ON c.id=p.color_id
                WHERE p.is_deleted=false ON CONFLICT (kind,source_id) DO NOTHING;
                INSERT INTO invoice_snapshots (id, kind, source_id, party_name, party_mobile, party_address, party_email,
                    product_name, serial_number, details_json, reconstructed, created_at, is_active, is_deleted)
                SELECT gen_random_uuid(), 'Sale', s.id, cu.name, cu.mobile, cu.address, cu.email,
                    concat_ws(' - ', b.name, m.name, vr.name, c.name) || ' — ' || p.serial_number,
                    p.serial_number, to_jsonb(s) || jsonb_build_object('Seller',
                        (SELECT to_jsonb(t) FROM customer_bill_settings t WHERE t.is_deleted=false LIMIT 1)),
                    true, now(), true, false
                FROM sales_products s JOIN customers cu ON cu.id=s.customer_id
                JOIN products p ON p.id::text=s.product_id
                LEFT JOIN brands b ON b.id=p.brand_id LEFT JOIN product_models m ON m.id=p.product_model_id
                LEFT JOIN variants vr ON vr.id=p.variant_id LEFT JOIN colors c ON c.id=p.color_id
                WHERE s.is_deleted=false ON CONFLICT (kind,source_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM invoice_corrections) OR EXISTS (SELECT 1 FROM opening_subledger_balances)
                        OR EXISTS (SELECT 1 FROM sales_products WHERE is_returned=true) THEN
                        RAISE EXCEPTION 'P0 financial workflows have been used. Restore the rehearsed pre-upgrade backup rather than dropping financial history.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "correction_refunds");

            migrationBuilder.DropTable(
                name: "invoice_snapshots");

            migrationBuilder.DropTable(
                name: "opening_settlements");

            migrationBuilder.DropTable(
                name: "invoice_corrections");

            migrationBuilder.DropTable(
                name: "opening_subledger_balances");

            migrationBuilder.DropIndex(
                name: "ix_users_is_owner",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_sales_products_product_id",
                table: "sales_products");

            migrationBuilder.DropColumn(
                name: "email_verified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "invitation_expires_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "invitation_hash",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_owner",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_returned",
                table: "sales_products");

            migrationBuilder.CreateIndex(
                name: "ix_sales_products_product_id",
                table: "sales_products",
                column: "product_id",
                unique: true,
                filter: "\"is_deleted\" = false");
        }
    }
}
