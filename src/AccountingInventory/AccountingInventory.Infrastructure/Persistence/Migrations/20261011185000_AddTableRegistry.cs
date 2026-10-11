using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingInventory.Infrastructure.Persistence.Migrations;

/// <summary>
/// EF Core migration for TableRegistry entity and auto-classification stored procedure.
/// </summary>
public partial class AddTableRegistry : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "tenant");

        migrationBuilder.CreateTable(
            name: "table_registry",
            schema: "tenant",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                schema_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValue: "tenant"),
                table_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                display_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                table_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Data"),
                category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, defaultValue: "General"),
                description = table.Column<string>(type: "text", nullable: true),
                is_system = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_table_registry", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "ix_table_registry_schema_table",
            schema: "tenant",
            table: "table_registry",
            columns: new[] { "schema_name", "table_name" },
            unique: true);

        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION tenant.sync_table_registry(p_schema text)
            RETURNS void AS $$
            BEGIN
                INSERT INTO tenant.table_registry (id, schema_name, table_name, display_name, table_type, category, description, is_system, created_at)
                SELECT 
                    gen_random_uuid(),
                    t.table_schema,
                    t.table_name,
                    INITCAP(REPLACE(t.table_name, '_', ' ')),
                    CASE 
                        WHEN t.table_name IN (
                            'brands', 'variants', 'colors', 'product_types', 'product_models', 
                            'vendors', 'finance_vendors', 'taxes', 'customer_bill_settings', 
                            'chart_accounts', 'accounting_periods', 'accounting_dimensions', 
                            'account_budgets', 'users', 'accounting_user_permissions', 
                            'tenants', 'feature_flags', 'table_registry'
                        ) THEN 'Master'
                        ELSE 'Data'
                    END,
                    CASE
                        WHEN t.table_name IN ('brands', 'variants', 'colors', 'product_types', 'product_models') THEN 'Catalog / Master'
                        WHEN t.table_name IN ('vendors', 'finance_vendors') THEN 'Partners / Master'
                        WHEN t.table_name IN ('taxes', 'customer_bill_settings') THEN 'Configuration / Master'
                        WHEN t.table_name IN ('chart_accounts', 'accounting_periods', 'accounting_dimensions', 'account_budgets') THEN 'Finance / Master'
                        WHEN t.table_name IN ('users', 'accounting_user_permissions', 'tenants', 'feature_flags') THEN 'System / Master'
                        WHEN t.table_name IN ('products', 'stock_skus', 'sku_movements', 'inventory_adjustments') THEN 'Inventory'
                        WHEN t.table_name IN ('sales_products', 'sales_invoices', 'sales_invoice_lines', 'sales_payments', 'sales_receipts', 'sales_invoice_receipts', 'customers', 'customer_advances', 'customer_advance_refunds', 'invoice_corrections', 'correction_refunds', 'invoice_snapshots') THEN 'Sales'
                        WHEN t.table_name IN ('purchase_payments') THEN 'Purchases'
                        WHEN t.table_name IN ('journal_entries', 'journal_lines', 'bank_reconciliations', 'bank_statement_lines', 'accounting_approvals', 'supporting_documents', 'fixed_assets', 'opening_subledger_balances', 'opening_settlements') THEN 'Accounting'
                        WHEN t.table_name IN ('audit_logs') THEN 'Compliance'
                        WHEN t.table_name IN ('master_import_batches', 'master_import_staging_rows') THEN 'Data Import'
                        ELSE 'General'
                    END,
                    CASE
                        WHEN t.table_name = 'brands' THEN 'Phone & accessory brand catalog master'
                        WHEN t.table_name = 'variants' THEN 'Product variants (RAM, storage, specs) master'
                        WHEN t.table_name = 'colors' THEN 'Device color master catalog'
                        WHEN t.table_name = 'product_types' THEN 'Product classification master'
                        WHEN t.table_name = 'product_models' THEN 'Device model master list'
                        WHEN t.table_name = 'vendors' THEN 'Supplier and vendor contacts master'
                        WHEN t.table_name = 'finance_vendors' THEN 'Financiers and EMI partners master'
                        WHEN t.table_name = 'taxes' THEN 'GST rates and tax slabs master'
                        WHEN t.table_name = 'customer_bill_settings' THEN 'Store header and bill print settings master'
                        WHEN t.table_name = 'chart_accounts' THEN 'General ledger chart of accounts master'
                        WHEN t.table_name = 'accounting_periods' THEN 'Fiscal accounting lock dates master'
                        WHEN t.table_name = 'users' THEN 'Store users, staff and login credentials master'
                        WHEN t.table_name = 'tenants' THEN 'Multi-tenant store registry master'
                        WHEN t.table_name = 'feature_flags' THEN 'System and tenant feature toggles master'
                        WHEN t.table_name = 'table_registry' THEN 'System table classification registry'
                        WHEN t.table_name = 'products' THEN 'Purchased serialized inventory units (phones/devices)'
                        WHEN t.table_name = 'stock_skus' THEN 'Accessory and bulk non-serialized inventory SKUs'
                        WHEN t.table_name = 'sku_movements' THEN 'Stock transfer and audit inventory ledger'
                        WHEN t.table_name = 'sales_products' THEN 'Direct point-of-sale product sold transactions'
                        WHEN t.table_name = 'sales_invoices' THEN 'Customer multi-item GST tax invoices'
                        WHEN t.table_name = 'sales_invoice_lines' THEN 'Invoice line items and price breakdowns'
                        WHEN t.table_name = 'sales_payments' THEN 'Payment transactions, part-payments and cash settlements'
                        WHEN t.table_name = 'customers' THEN 'Customer profiles and party ledgers'
                        WHEN t.table_name = 'customer_advances' THEN 'Customer advance booking deposits'
                        WHEN t.table_name = 'journal_entries' THEN 'General ledger double-entry journal transactions'
                        WHEN t.table_name = 'journal_lines' THEN 'Debit and credit posting financial entries'
                        WHEN t.table_name = 'audit_logs' THEN 'System mutation and compliance activity logs'
                        ELSE 'Operational transactional table'
                    END,
                    t.table_name LIKE '__%' OR t.table_name = 'table_registry',
                    CURRENT_TIMESTAMP
                FROM information_schema.tables t
                WHERE t.table_schema = p_schema
                  AND t.table_type = 'BASE TABLE'
                  AND t.table_name NOT IN (
                      SELECT table_name FROM tenant.table_registry WHERE schema_name = p_schema
                  )
                ON CONFLICT (schema_name, table_name) DO NOTHING;
            END;
            $$ LANGUAGE plpgsql;

            SELECT tenant.sync_table_registry('tenant');
        ");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS tenant.sync_table_registry(text);");

        migrationBuilder.DropTable(
            name: "table_registry",
            schema: "tenant");
    }
}

