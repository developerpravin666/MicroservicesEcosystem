using BuildingBlocks.Messaging;
using BuildingBlocks.Observability;
using BuildingBlocks.Observability.Middleware;
using BuildingBlocks.Security;
using BuildingBlocks.WebDefaults;
using AccountingInventory.Api.Endpoints;
using AccountingInventory.Application;
using AccountingInventory.Infrastructure;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

const string ServiceName = "AccountingInventory.Api";
var builder = WebApplication.CreateBuilder(args);
AccountingInventory.Api.ProductionConfiguration.Validate(builder);

builder.AddSharedLogging(ServiceName);

builder.Services.AddPlatformSecurity(builder.Configuration);
builder.Services.AddWebDefaults(ServiceName);
builder.Services.AddPlatformMessaging(builder.Configuration, AccountingInventory.Application.DependencyInjection.ApplicationAssembly);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendCorsPolicy", policy =>
    {
        var origins = new List<string>();
        var frontendUrl = builder.Configuration["Frontend:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(frontendUrl))
        {
            origins.Add(frontendUrl.TrimEnd('/'));
        }
        var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        origins.AddRange(configuredOrigins);

        if (builder.Environment.IsDevelopment())
        {
            origins.Add("http://localhost:5173");
            origins.Add("http://localhost:3000");
        }

        var distinctOrigins = origins.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().ToArray();
        if (distinctOrigins.Length > 0)
        {
            policy.WithOrigins(distinctOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

app.UseWebDefaults();
app.UseCorrelationId();
app.UseCors("FrontendCorsPolicy");
app.UseSharedRequestLogging();
app.UsePlatformSecurity();

app.MapAuthEndpoints();
app.MapAdminEndpoints();
app.MapEmailEndpoints();
//app.MapRoleEndpoints();
app.MapTenantEndpoints();
app.MapVendorEndpoints();
app.MapFinanceVendorEndpoints();
app.MapBrandEndpoints();
app.MapVariantEndpoints();
app.MapColorEndpoints();
app.MapProductTypeEndpoints();
app.MapProductModelEndpoints();
app.MapMasterImportEndpoints();
app.MapProductEndpoints();
app.MapInventoryEndpoints();
app.MapPurchaseAccountingEndpoints();
app.MapSalesProductEndpoints();
app.MapSalesAccountingEndpoints();
app.MapSalesInvoiceEndpoints();
app.MapCustomerEndpoints();
app.MapCustomerBillSettingsEndpoints();
app.MapAuditLogEndpoints();
app.MapGeneralLedgerEndpoints();
app.MapAccountingExtensionEndpoints();
app.MapAccountingP0Endpoints();
app.MapTaxEndpoints();
app.MapDashboardEndpoints();
app.MapFeatureFlagEndpoints();
app.MapTableRegistryEndpoints();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = ServiceName }));
app.MapGet("/health/ready", async (TenantDbContext db, CancellationToken ct) =>
{
    try
    {
        await db.Tenants.AsNoTracking().Take(1).Select(x => x.Id).ToListAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
});

using (var scope = app.Services.CreateScope())
{
    var tenantDb = scope.ServiceProvider.GetRequiredService<TenantDbContext>();
    await tenantDb.Database.MigrateAsync();
    await tenantDb.Database.ExecuteSqlRawAsync(@"
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_name character varying(150);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_email character varying(150);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS owner_mobile character varying(25);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS initial_password_hash character varying(255);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS state_code character varying(10);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS gstin character varying(20);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS address character varying(500);
        ALTER TABLE tenant.tenants ADD COLUMN IF NOT EXISTS rejection_reason character varying(500);

        CREATE TABLE IF NOT EXISTS tenant.feature_flags (
            id uuid NOT NULL,
            key character varying(100) NOT NULL,
            name character varying(200) NOT NULL,
            description text,
            is_enabled boolean NOT NULL DEFAULT true,
            category character varying(50) DEFAULT 'General',
            tenant_id uuid,
            created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            modified_at timestamp with time zone,
            CONSTRAINT pk_feature_flags PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_feature_flags_key_tenant
            ON tenant.feature_flags (key, COALESCE(tenant_id, '00000000-0000-0000-0000-000000000000'::uuid));

        INSERT INTO tenant.feature_flags (id, key, name, description, is_enabled, category, tenant_id, created_at)
        VALUES ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'voice_typing', 'Voice Typing & Speech Dictation', 'Enables automatic speech-to-text dictation across input fields and form controls via Web Speech API', true, 'Accessibility', NULL, CURRENT_TIMESTAMP)
        ON CONFLICT DO NOTHING;

        CREATE TABLE IF NOT EXISTS tenant.table_registry (
            id uuid NOT NULL PRIMARY KEY,
            schema_name character varying(100) NOT NULL DEFAULT 'tenant',
            table_name character varying(100) NOT NULL,
            display_name character varying(150) NOT NULL,
            table_type character varying(50) NOT NULL DEFAULT 'Data',
            category character varying(100) DEFAULT 'General',
            description text,
            is_system boolean NOT NULL DEFAULT false,
            created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
            modified_at timestamp with time zone
        );
        CREATE UNIQUE INDEX IF NOT EXISTS ix_table_registry_schema_table
            ON tenant.table_registry (schema_name, table_name);

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
                    WHEN t.table_name IN ('brands', 'variants', 'colors', 'product_types', 'product_models', 'vendors', 'finance_vendors', 'taxes', 'customer_bill_settings', 'chart_accounts', 'accounting_periods', 'accounting_dimensions', 'account_budgets', 'users', 'accounting_user_permissions', 'tenants', 'feature_flags', 'table_registry') THEN 'Master'
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

await app.Services.ApplyTenantSchemaMigrationsAsync();

Log.Information("Starting {Service}", ServiceName);
app.Run();
