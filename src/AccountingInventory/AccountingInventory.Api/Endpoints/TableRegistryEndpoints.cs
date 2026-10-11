using System.Data;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Api.Endpoints;

public static class TableRegistryEndpoints
{
    public static RouteGroupBuilder MapTableRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tables").WithTags("Table Management");

        // 1. GET /api/admin/tables - List all tables with live row counts & KPI summary
        group.MapGet("/", async (
            [FromQuery] string? schema,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            await EnsureSchemaSyncedAsync(tenantDb, targetSchema, ct);

            var tables = await tenantDb.TableRegistries
                .AsNoTracking()
                .Where(t => t.SchemaName == targetSchema)
                .OrderBy(t => t.TableType == "Master" ? 0 : 1)
                .ThenBy(t => t.Category)
                .ThenBy(t => t.DisplayName)
                .ToListAsync(ct);

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            var tableDtos = new List<TableRegistryDto>();
            long totalDataRecords = 0;

            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);

                foreach (var table in tables)
                {
                    long count = 0;
                    try
                    {
                        await using var cmd = conn.CreateCommand();
                        // Validate identifiers
                        if (IsValidSqlIdentifier(targetSchema) && IsValidSqlIdentifier(table.TableName))
                        {
                            cmd.CommandText = $"SELECT COUNT(*) FROM \"{targetSchema}\".\"{table.TableName}\"";
                            var result = await cmd.ExecuteScalarAsync(ct);
                            count = Convert.ToInt64(result);
                        }
                    }
                    catch
                    {
                        count = 0;
                    }

                    if (string.Equals(table.TableType, "Data", StringComparison.OrdinalIgnoreCase))
                    {
                        totalDataRecords += count;
                    }

                    tableDtos.Add(new TableRegistryDto(
                        table.Id,
                        table.SchemaName,
                        table.TableName,
                        table.DisplayName,
                        table.TableType,
                        table.Category,
                        table.Description,
                        table.IsSystem,
                        count,
                        table.CreatedAt,
                        table.ModifiedAt
                    ));
                }
            }

            var allSchemas = await GetAvailableSchemasAsync(tenantDb, ct);

            var resultSummary = new TableRegistryDashboardResult(
                targetSchema,
                allSchemas,
                tableDtos.Count,
                tableDtos.Count(t => string.Equals(t.TableType, "Master", StringComparison.OrdinalIgnoreCase)),
                tableDtos.Count(t => string.Equals(t.TableType, "Data", StringComparison.OrdinalIgnoreCase)),
                totalDataRecords,
                tableDtos
            );

            return Results.Ok(resultSummary);
        })
        .WithName("GetTableRegistry").AllowAnonymous()
        .Produces<TableRegistryDashboardResult>();

        // 2. PUT /api/admin/tables/{tableName}/type - Update table type (Master <-> Data)
        group.MapPut("/{tableName}/type", async (
            string tableName,
            [FromBody] UpdateTableTypeRequest request,
            [FromQuery] string? schema,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request?.TableType))
            {
                return Results.BadRequest(new { message = "TableType must be either 'Master' or 'Data'." });
            }

            var normalizedType = request.TableType.Trim().Equals("Master", StringComparison.OrdinalIgnoreCase)
                ? "Master"
                : "Data";

            var targetSchema = ResolveTargetSchema(schema ?? request.Schema, tenantContext, tenantDb);

            var record = await tenantDb.TableRegistries
                .FirstOrDefaultAsync(t => t.SchemaName == targetSchema && t.TableName == tableName, ct);

            if (record == null)
            {
                // Auto-register if table exists in information_schema
                record = new TableRegistry
                {
                    Id = Guid.NewGuid(),
                    SchemaName = targetSchema,
                    TableName = tableName,
                    DisplayName = request.DisplayName ?? FormatDisplayName(tableName),
                    TableType = normalizedType,
                    Category = request.Category ?? "General",
                    Description = request.Description ?? "",
                    IsSystem = tableName.StartsWith("__") || tableName == "table_registry",
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow
                };
                tenantDb.TableRegistries.Add(record);
            }
            else
            {
                record.TableType = normalizedType;
                if (!string.IsNullOrWhiteSpace(request.DisplayName)) record.DisplayName = request.DisplayName;
                if (!string.IsNullOrWhiteSpace(request.Category)) record.Category = request.Category;
                if (request.Description != null) record.Description = request.Description;
                record.ModifiedAt = DateTime.UtcNow;
            }

            await tenantDb.SaveChangesAsync(ct);

            return Results.Ok(new
            {
                success = true,
                message = $"Table '{tableName}' type updated to '{normalizedType}'.",
                table = record
            });
        })
        .WithName("UpdateTableType").AllowAnonymous();

        // 3. POST /api/admin/tables/sync - Discover & sync new tables
        group.MapPost("/sync", async (
            [FromQuery] string? schema,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            await EnsureSchemaSyncedAsync(tenantDb, targetSchema, ct);

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully scanned and synchronized table registry for schema '{targetSchema}'."
            });
        })
        .WithName("SyncTableRegistry").AllowAnonymous();

        // 4. POST /api/admin/tables/{tableName}/clear - Clear specific Data table (BLOCKED for Master tables)
        group.MapPost("/{tableName}/clear", async (
            string tableName,
            [FromQuery] string? schema,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);

            var table = await tenantDb.TableRegistries
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchemaName == targetSchema && t.TableName == tableName, ct);

            if (table == null)
            {
                return Results.NotFound(new { message = $"Table '{tableName}' not found in registry for schema '{targetSchema}'." });
            }

            // CRITICAL INTEGRITY CHECK: Master tables must never be deleted!
            if (string.Equals(table.TableType, "Master", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "PROTECTED_MASTER_TABLE",
                    message = $"Cannot clear data from Master table '{tableName}'. Master tables are strictly protected to safeguard reference catalogs (brands, models, taxes, etc.). If you genuinely intend to delete records from this table, update its table type to 'Data' first."
                });
            }

            if (table.IsSystem || tableName.StartsWith("__"))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "SYSTEM_TABLE",
                    message = $"Cannot clear internal system table '{tableName}'."
                });
            }

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            if (!IsValidSqlIdentifier(targetSchema) || !IsValidSqlIdentifier(tableName))
            {
                return Results.BadRequest(new { message = "Invalid schema or table identifier." });
            }

            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"TRUNCATE TABLE \"{targetSchema}\".\"{tableName}\" CASCADE;";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully cleared all data from table '{tableName}' in schema '{targetSchema}'. Master reference tables remained protected.",
                tableName,
                schema = targetSchema,
                rowCount = 0
            });
        })
        .WithName("ClearDataTable").AllowAnonymous();

        // 5. POST /api/admin/tables/clear-all-data - Bulk clear all Data tables (Master tables remain protected)
        group.MapPost("/clear-all-data", async (
            [FromQuery] string? schema,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            await EnsureSchemaSyncedAsync(tenantDb, targetSchema, ct);

            var allTables = await tenantDb.TableRegistries
                .AsNoTracking()
                .Where(t => t.SchemaName == targetSchema)
                .ToListAsync(ct);

            var dataTables = allTables
                .Where(t => string.Equals(t.TableType, "Data", StringComparison.OrdinalIgnoreCase) && !t.IsSystem && !t.TableName.StartsWith("__"))
                .Select(t => t.TableName)
                .ToList();

            var masterTables = allTables
                .Where(t => string.Equals(t.TableType, "Master", StringComparison.OrdinalIgnoreCase))
                .Select(t => t.TableName)
                .ToList();

            if (dataTables.Count == 0)
            {
                return Results.Ok(new
                {
                    success = true,
                    message = "No eligible Data tables found to clear.",
                    clearedTables = Array.Empty<string>(),
                    preservedMasterTables = masterTables
                });
            }

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            if (!IsValidSqlIdentifier(targetSchema) || !dataTables.All(IsValidSqlIdentifier))
            {
                return Results.BadRequest(new { message = "Invalid schema or table identifier." });
            }

            var truncateStatements = string.Join(", ", dataTables.Select(t => $"\"{targetSchema}\".\"{t}\""));

            await using (var conn = new NpgsqlConnection(connectionString))
            {
                await conn.OpenAsync(ct);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = $"TRUNCATE TABLE {truncateStatements} CASCADE;";
                await cmd.ExecuteNonQueryAsync(ct);
            }

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully cleared data from {dataTables.Count} Data tables in schema '{targetSchema}'. All {masterTables.Count} Master tables remain safely preserved.",
                clearedCount = dataTables.Count,
                clearedTables = dataTables,
                preservedCount = masterTables.Count,
                preservedMasterTables = masterTables
            });
        })
        .WithName("ClearAllDataTables").AllowAnonymous();

        return group;
    }

    private static string ResolveTargetSchema(string? requestedSchema, ITenantContext? context, TenantDbContext tenantDb)
    {
        if (!string.IsNullOrWhiteSpace(requestedSchema))
        {
            return requestedSchema.Trim();
        }

        if (context?.SchemaName is { Length: > 0 } userSchema && userSchema != "tenant")
        {
            return userSchema;
        }

        // Default to first active tenant schema or "tenant"
        var firstTenant = tenantDb.Tenants.AsNoTracking().Where(t => t.IsActive && !t.IsDeleted).Select(t => t.SchemaName).FirstOrDefault();
        return firstTenant ?? "tenant";
    }

    private static async Task<List<string>> GetAvailableSchemasAsync(TenantDbContext tenantDb, CancellationToken ct)
    {
        var tenantSchemas = await tenantDb.Tenants
            .AsNoTracking()
            .Where(t => t.IsActive && !t.IsDeleted)
            .Select(t => t.SchemaName)
            .Distinct()
            .ToListAsync(ct);

        var list = new List<string> { "tenant" };
        list.AddRange(tenantSchemas);
        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task EnsureSchemaSyncedAsync(TenantDbContext tenantDb, string schemaName, CancellationToken ct)
    {
        if (!IsValidSqlIdentifier(schemaName)) return;

        var sql = @"
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM pg_proc p 
                    JOIN pg_namespace n ON p.pronamespace = n.oid 
                    WHERE n.nspname = 'tenant' AND p.proname = 'sync_table_registry'
                ) THEN
                    PERFORM tenant.sync_table_registry('" + schemaName + @"');
                ELSE
                    -- Fallback: insert directly from information_schema
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
                        'General',
                        'Table in ' || t.table_schema,
                        t.table_name LIKE '__%' OR t.table_name = 'table_registry',
                        CURRENT_TIMESTAMP
                    FROM information_schema.tables t
                    WHERE t.table_schema = '" + schemaName + @"'
                      AND t.table_type = 'BASE TABLE'
                      AND t.table_name NOT IN (
                          SELECT table_name FROM tenant.table_registry WHERE schema_name = '" + schemaName + @"'
                      )
                    ON CONFLICT (schema_name, table_name) DO NOTHING;
                END IF;
            END $$;
        ";

        try
        {
            await tenantDb.Database.ExecuteSqlRawAsync(sql, ct);
        }
        catch
        {
            // Ignore if schema does not exist yet
        }
    }

    private static bool IsValidSqlIdentifier(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 128
            && value.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static string FormatDisplayName(string rawName)
    {
        var parts = rawName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }
}

public sealed record UpdateTableTypeRequest(
    string TableType,
    string? DisplayName = null,
    string? Category = null,
    string? Description = null,
    string? Schema = null);

public sealed record TableRegistryDto(
    Guid Id,
    string SchemaName,
    string TableName,
    string DisplayName,
    string TableType,
    string Category,
    string Description,
    bool IsSystem,
    long RowCount,
    DateTime CreatedAt,
    DateTime? ModifiedAt);

public sealed record TableRegistryDashboardResult(
    string ActiveSchema,
    IReadOnlyList<string> AvailableSchemas,
    int TotalTables,
    int MasterTables,
    int DataTables,
    long TotalDataRecords,
    IReadOnlyList<TableRegistryDto> Tables);

