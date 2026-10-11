using AccountingInventory.Infrastructure.Persistence.MultiTenancy;
using BuildingBlocks.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;

namespace AccountingInventory.Infrastructure.Persistence;

internal static class TenantSchemaMigrator
{
    public static async Task MigrateAsync(
        string connectionString,
        string schemaName,
        CancellationToken cancellationToken)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);
        var tenantProvider = new StaticTenantProvider(schemaName);
        var tenantConnectionString = CreateTenantConnectionString(connectionString, schemaName);

        var options = new DbContextOptionsBuilder<AccountingInventoryDbContext>()
            .UseNpgsql(
                tenantConnectionString,
                npgsql => npgsql.MigrationsHistoryTable("__TenantSchemaHistory", tenantProvider.SchemaName))
            .UseSnakeCaseNamingConvention()
            .ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>()
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var applicationDbContext = new AccountingInventoryDbContext(options, tenantProvider);

        // PostgreSQL identifiers cannot be parameters. Validation guarantees this
        // value is a valid tenant-schema identifier before it is interpolated.
        await applicationDbContext.Database.ExecuteSqlRawAsync(
            $"CREATE SCHEMA IF NOT EXISTS \"{tenantProvider.SchemaName}\";", cancellationToken);
        await applicationDbContext.Database.MigrateAsync(cancellationToken);

        // Idempotent safety fallback: ensures staging tables and indexes exist even if
        // historical schemas were bootstrapped without full migration execution.
        await applicationDbContext.Database.ExecuteSqlRawAsync($@"
            CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".master_import_batches (
                id uuid NOT NULL,
                batch_number character varying(80) NOT NULL,
                file_name character varying(250) NOT NULL,
                file_type character varying(50),
                status integer NOT NULL,
                total_rows integer NOT NULL,
                valid_rows integer NOT NULL,
                error_rows integer NOT NULL,
                warning_rows integer NOT NULL,
                created_count integer NOT NULL,
                updated_count integer NOT NULL,
                summary_json text,
                review_notes text,
                reviewed_at timestamp with time zone,
                reviewed_by text,
                created_at timestamp with time zone NOT NULL,
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                is_active boolean NOT NULL,
                is_deleted boolean NOT NULL,
                CONSTRAINT pk_master_import_batches PRIMARY KEY (id)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_master_import_batches_batch_number
                ON ""{tenantProvider.SchemaName}"".master_import_batches (batch_number);

            CREATE TABLE IF NOT EXISTS ""{tenantProvider.SchemaName}"".master_import_staging_rows (
                id uuid NOT NULL,
                batch_id uuid NOT NULL,
                row_index integer NOT NULL,
                entity_type character varying(50) NOT NULL,
                action integer NOT NULL,
                status integer NOT NULL,
                entity_key character varying(150),
                entity_name character varying(250),
                validation_errors text,
                raw_data_json text NOT NULL,
                is_approved boolean NOT NULL,
                is_imported boolean NOT NULL,
                imported_at timestamp with time zone,
                import_message text,
                created_at timestamp with time zone NOT NULL,
                created_by uuid,
                modified_at timestamp with time zone,
                modified_by uuid,
                is_active boolean NOT NULL,
                is_deleted boolean NOT NULL,
                CONSTRAINT pk_master_import_staging_rows PRIMARY KEY (id)
            );
            DO $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint 
                    WHERE conname = 'fk_master_import_staging_rows_master_import_batches_batch_id'
                ) THEN
                    ALTER TABLE ""{tenantProvider.SchemaName}"".master_import_staging_rows
                        ADD CONSTRAINT fk_master_import_staging_rows_master_import_batches_batch_id
                        FOREIGN KEY (batch_id) REFERENCES ""{tenantProvider.SchemaName}"".master_import_batches (id) ON DELETE CASCADE;
                END IF;
            END $$;
            CREATE INDEX IF NOT EXISTS ix_master_import_staging_rows_batch_id_row_index
                ON ""{tenantProvider.SchemaName}"".master_import_staging_rows (batch_id, row_index);
        ", cancellationToken);
    }

    internal static string CreateTenantConnectionString(string connectionString, string schemaName)
    {
        TenantSchemaNameValidator.EnsureValid(schemaName);
        var builder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            // Include public for extensions/functions while ensuring unqualified
            // EF DDL and DML always resolve to this tenant first.
            SearchPath = $"{schemaName},public"
        };

        return builder.ConnectionString;
    }
}
