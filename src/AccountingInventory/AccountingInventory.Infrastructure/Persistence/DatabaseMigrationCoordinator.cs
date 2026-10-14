using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Data;

namespace AccountingInventory.Infrastructure.Persistence;

public sealed class DatabaseMigrationCoordinator(
    TenantDbContext tenantDbContext,
    IConfiguration configuration,
    ILogger<DatabaseMigrationCoordinator> logger)
    : IDatabaseMigrationCoordinator
{
    internal const string ControlPlaneSupplementalSql = @"
        CREATE TABLE IF NOT EXISTS tenant.__email_templates (
            key varchar(100) PRIMARY KEY, subject text NOT NULL, html_body text NOT NULL
        );
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
    ";

    public async Task<MigrationExecutionResult> MigrateAllAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting streamlined platform database migration...");

        // 1. Migrate Control Plane inside an explicit transaction
        var controlPlaneResult = await MigrateControlPlaneAsync(cancellationToken);
        if (!controlPlaneResult.Success)
        {
            logger.LogError("Control plane migration failed: {Message}. Halting platform migration.", controlPlaneResult.Message);
            return controlPlaneResult;
        }

        // 2. Migrate All Active Tenant Schemas inside isolated per-schema transactions
        var tenantResult = await MigrateAllTenantSchemasAsync(cancellationToken);

        var allMigrated = new List<string> { "tenant" };
        allMigrated.AddRange(tenantResult.MigratedSchemas);

        return new MigrationExecutionResult(
            Success: tenantResult.Success,
            Message: $"Platform migration completed. Control plane ('tenant') and {tenantResult.SuccessfulSchemas}/{tenantResult.TotalSchemas} tenant schemas successfully migrated.",
            TotalSchemas: tenantResult.TotalSchemas + 1,
            SuccessfulSchemas: tenantResult.SuccessfulSchemas + 1,
            MigratedSchemas: allMigrated,
            FailedSchemas: tenantResult.FailedSchemas,
            Timestamp: DateTimeOffset.UtcNow);
    }

    public async Task<MigrationExecutionResult> MigrateControlPlaneAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Migrating control plane schema 'tenant' with transactional safety...");

        var strategy = tenantDbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            try
            {
                // Ensure schema exists
                await tenantDbContext.Database.ExecuteSqlRawAsync(
                    "CREATE SCHEMA IF NOT EXISTS \"tenant\";", cancellationToken);

                // Apply EF Core migrations for control plane (EF Core manages its own internal migration transactions)
                await tenantDbContext.Database.MigrateAsync(cancellationToken);

                // Execute supplemental idempotent scripts inside an explicit database transaction
                await using var tx = await tenantDbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
                try
                {
                    await tenantDbContext.Database.ExecuteSqlRawAsync(ControlPlaneSupplementalSql, cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                }
                catch
                {
                    await tx.RollbackAsync(cancellationToken);
                    throw;
                }

                logger.LogInformation("Control plane schema 'tenant' successfully migrated and committed.");

                return new MigrationExecutionResult(
                    Success: true,
                    Message: "Control plane schema 'tenant' migrated successfully.",
                    TotalSchemas: 1,
                    SuccessfulSchemas: 1,
                    MigratedSchemas: new[] { "tenant" },
                    FailedSchemas: new Dictionary<string, string>(),
                    Timestamp: DateTimeOffset.UtcNow);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to migrate control plane schema 'tenant'.");
                return new MigrationExecutionResult(
                    Success: false,
                    Message: $"Control plane migration failed: {ex.Message}",
                    TotalSchemas: 1,
                    SuccessfulSchemas: 0,
                    MigratedSchemas: Array.Empty<string>(),
                    FailedSchemas: new Dictionary<string, string> { ["tenant"] = ex.Message },
                    Timestamp: DateTimeOffset.UtcNow);
            }
        });
    }

    public async Task<MigrationExecutionResult> MigrateTenantSchemaAsync(string schemaName, CancellationToken cancellationToken = default)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);
        var connectionString = configuration.GetConnectionString("AccountingInventoryDb")
            ?? throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");

        try
        {
            await TenantSchemaMigrator.MigrateAsync(connectionString, schemaName, cancellationToken);
            return new MigrationExecutionResult(
                Success: true,
                Message: $"Tenant schema '{schemaName}' migrated successfully.",
                TotalSchemas: 1,
                SuccessfulSchemas: 1,
                MigratedSchemas: new[] { schemaName },
                FailedSchemas: new Dictionary<string, string>(),
                Timestamp: DateTimeOffset.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to migrate tenant schema '{SchemaName}'.", schemaName);
            return new MigrationExecutionResult(
                Success: false,
                Message: $"Migration failed for schema '{schemaName}': {ex.Message}",
                TotalSchemas: 1,
                SuccessfulSchemas: 0,
                MigratedSchemas: Array.Empty<string>(),
                FailedSchemas: new Dictionary<string, string> { [schemaName] = ex.Message },
                Timestamp: DateTimeOffset.UtcNow);
        }
    }

    public async Task<MigrationExecutionResult> MigrateAllTenantSchemasAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Scanning active tenant schemas for migration...");

        var schemas = await tenantDbContext.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted && t.IsActive)
            .Select(t => t.SchemaName)
            .Distinct()
            .ToListAsync(cancellationToken);

        var migrated = new List<string>();
        var failed = new Dictionary<string, string>();
        var connectionString = configuration.GetConnectionString("AccountingInventoryDb")
            ?? throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");

        logger.LogInformation("Discovered {Count} active tenant schemas to migrate.", schemas.Count);

        foreach (var schema in schemas)
        {
            try
            {
                logger.LogInformation("Migrating tenant schema '{Schema}' with transactional DDL...", schema);
                await TenantSchemaMigrator.MigrateAsync(connectionString, schema, cancellationToken);
                migrated.Add(schema);
                logger.LogInformation("Tenant schema '{Schema}' migration succeeded.", schema);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Tenant schema '{Schema}' migration failed: {Message}", schema, ex.Message);
                failed[schema] = ex.Message;
            }
        }

        var isSuccess = failed.Count == 0;
        var summaryMsg = isSuccess
            ? $"Successfully migrated all {migrated.Count} tenant schemas."
            : $"Completed with warnings: {migrated.Count} schemas migrated successfully, {failed.Count} schemas failed.";

        return new MigrationExecutionResult(
            Success: isSuccess,
            Message: summaryMsg,
            TotalSchemas: schemas.Count,
            SuccessfulSchemas: migrated.Count,
            MigratedSchemas: migrated,
            FailedSchemas: failed,
            Timestamp: DateTimeOffset.UtcNow);
    }

}
