using System.Data;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccountingInventory.Api.Endpoints;

public static class TableRegistryEndpoints
{
    public static RouteGroupBuilder MapTableRegistryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/tables").WithTags("Table Management")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser()
                .RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString()));

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
        .WithName("GetTableRegistry")
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
        .WithName("UpdateTableType");

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
        .WithName("SyncTableRegistry");

        // 4. Clear a specific table; master tables require explicit confirmation.
        group.MapPost("/{tableName}/clear", async (
            string tableName,
            [FromQuery] string? schema,
            [FromBody] ClearTableRequest request,
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

            var isMaster = string.Equals(table.TableType, "Master", StringComparison.OrdinalIgnoreCase);
            if (isMaster && !string.Equals(request.ConfirmTableName, tableName, StringComparison.Ordinal))
            {
                return Results.BadRequest(new
                {
                    success = false,
                    error = "MASTER_TABLE_CONFIRMATION_REQUIRED",
                    message = $"Type the exact table name '{tableName}' to confirm deletion of its master records."
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

            var operationId = await TableClearRecovery.ClearAsync(connectionString, targetSchema, [tableName], ct);

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully cleared all data from table '{tableName}' in schema '{targetSchema}'.",
                operationId,
                tableName,
                schema = targetSchema,
                rowCount = 0
            });
        })
        .WithName("ClearDataTable");

        // 5. POST /api/admin/tables/clear-all-data - Bulk clear both Master and Data tables
        group.MapPost("/clear-all-data", async (
            [FromQuery] string? schema,
            [FromBody] ClearAllTablesRequest request,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            var conf = request.Confirmation?.Trim().ToUpperInvariant();
            if (conf != "CLEAR ALL TABLES" && conf != "DELETE ALL" && conf != "DELETE")
                return Results.BadRequest(new { message = "Type 'DELETE' or 'CLEAR ALL TABLES' to confirm deletion of Master and Data records." });
            await EnsureSchemaSyncedAsync(tenantDb, targetSchema, ct);

            var allTables = await tenantDb.TableRegistries
                .AsNoTracking()
                .Where(t => t.SchemaName == targetSchema)
                .ToListAsync(ct);

            var eligibleTables = allTables
                .Where(t => t.TableName != "table_registry" && !t.TableName.StartsWith("__"))
                .Select(t => t.TableName)
                .ToList();

            var systemTables = allTables
                .Where(t => t.TableName == "table_registry" || t.TableName.StartsWith("__"))
                .Select(t => t.TableName)
                .ToList();

            if (eligibleTables.Count == 0)
            {
                return Results.Ok(new
                {
                    success = true,
                    message = "No eligible tables found to clear.",
                    clearedTables = Array.Empty<string>(),
                    preservedSystemTables = systemTables
                });
            }

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            if (!IsValidSqlIdentifier(targetSchema) || !eligibleTables.All(IsValidSqlIdentifier))
            {
                return Results.BadRequest(new { message = "Invalid schema or table identifier." });
            }

            var operationId = await TableClearRecovery.ClearAsync(connectionString, targetSchema, eligibleTables, ct);

            return Results.Ok(new
            {
                success = true,
                message = $"Successfully cleared data from {eligibleTables.Count} tables (Master and Data) in schema '{targetSchema}'.",
                operationId,
                clearedCount = eligibleTables.Count,
                clearedTables = eligibleTables,
                preservedCount = systemTables.Count,
                preservedSystemTables = systemTables
            });
        })
        .WithName("ClearAllDataTables");

        group.MapPost("/clear-selected", async ([FromBody] ClearSelectedTablesRequest request,
            [FromQuery] string schema, TenantDbContext db, CancellationToken ct) =>
        {
            if (request.Tables is null || request.Tables.Length == 0 || !IsValidSqlIdentifier(schema))
                return Results.BadRequest(new { message = "Please select at least one table to delete records from." });

            var conf = request.Confirmation?.Trim().ToUpperInvariant();
            if (conf != "CLEAR SELECTED TABLES" && conf != "DELETE" && conf != "CONFIRM" && conf != "DELETE SELECTED")
                return Results.BadRequest(new { message = "Select tables and type 'DELETE' or 'CLEAR SELECTED TABLES' to confirm." });

            var names = request.Tables.Distinct().ToArray();
            var allowed = await db.TableRegistries.Where(t => t.SchemaName == schema && names.Contains(t.TableName)
                && t.TableName != "table_registry" && !t.TableName.StartsWith("__")).Select(t => t.TableName).ToListAsync(ct);
            if (allowed.Count != names.Length || !names.All(IsValidSqlIdentifier))
                return Results.BadRequest(new { message = "The selection includes unknown or protected tables." });
            var id = await TableClearRecovery.ClearAsync(db.Database.GetConnectionString()!, schema, names, ct);
            return Results.Ok(new {
                success = true,
                operationId = id,
                deletedTables = names,
                message = $"Successfully deleted data from {names.Length} selected table(s). A recovery copy was saved."
            });
        });
        group.MapPost("/restore/{operationId:guid}", async (Guid operationId, [FromQuery] string schema,
            [FromBody] RestoreTablesRequest request, TenantDbContext db, CancellationToken ct) =>
        {
            if (request.Confirmation != "RESTORE DATA") return Results.BadRequest(new { message = "Type RESTORE DATA to confirm." });
            try { await TableClearRecovery.RestoreAsync(db.Database.GetConnectionString()!, operationId, schema, ct); }
            catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
            catch (PostgresException) { return Results.Conflict(new { message = "Restore could not be completed with the current database structure or constraints. No records were changed; the recovery copy was retained." }); }
            return Results.Ok(new { message = "Cleared records were restored." });
        });

        // 5b. POST /api/admin/tables/drop-schema - Super Admin manual drop of entire schema and data
        group.MapPost("/drop-schema", async (
            [FromQuery] string schema,
            [FromBody] DropSchemaRequest request,
            TenantDbContext tenantDb,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(schema) || !IsValidSqlIdentifier(schema))
                return Results.BadRequest(new { message = "Invalid schema identifier." });

            var protectedSchemas = new[] { "public", "tenant", "pg_catalog", "information_schema", "admin_table_clear" };
            if (protectedSchemas.Contains(schema.Trim().ToLowerInvariant()))
            {
                return Results.BadRequest(new { message = $"Schema '{schema}' is a protected system schema and cannot be dropped." });
            }

            var expectedConf = $"DROP SCHEMA {schema.Trim().ToUpperInvariant()}";
            var conf = request?.Confirmation?.Trim();
            if (!string.Equals(conf, expectedConf, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(conf, $"DROP {schema.Trim().ToUpperInvariant()}", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = $"Type '{expectedConf}' to confirm dropping the entire schema and all its data." });
            }

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            // Drop schema CASCADE
            await using (var dropCmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;", conn))
            {
                await dropCmd.ExecuteNonQueryAsync(ct);
            }

            // Remove entries from tenant.table_registry
            try
            {
                var tablesToRemove = await tenantDb.TableRegistries
                    .Where(t => t.SchemaName == schema)
                    .ToListAsync(ct);
                if (tablesToRemove.Count > 0)
                {
                    tenantDb.TableRegistries.RemoveRange(tablesToRemove);
                    await tenantDb.SaveChangesAsync(ct);
                }
            }
            catch { /* Ignore if table_registry doesn't exist */ }

            return Results.Ok(new
            {
                success = true,
                schema,
                message = $"Successfully dropped schema '{schema}' and all of its tables, constraints, and data."
            });
        })
        .WithName("DropSchemaAndData");

        // 6. GET /api/admin/tables/{tableName}/records - Paginated view of table records
        group.MapGet("/{tableName}/records", async (
            string tableName,
            [FromQuery] string? schema,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            [FromQuery] string? search,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            if (!IsValidSqlIdentifier(targetSchema) || !IsValidSqlIdentifier(tableName))
                return Results.BadRequest(new { message = "Invalid schema or table identifier." });

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            int currentPage = Math.Max(1, page ?? 1);
            int take = Math.Clamp(pageSize ?? 25, 1, 100);
            int offset = (currentPage - 1) * take;

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            // Fetch Columns
            var columns = new List<TableColumnMetaDto>();
            await using (var colCmd = new NpgsqlCommand(@"
                SELECT column_name, data_type, is_nullable
                FROM information_schema.columns
                WHERE table_schema = @schema AND table_name = @table
                ORDER BY ordinal_position", conn))
            {
                colCmd.Parameters.AddWithValue("schema", targetSchema);
                colCmd.Parameters.AddWithValue("table", tableName);
                await using var reader = await colCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    columns.Add(new TableColumnMetaDto(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2) == "YES"
                    ));
                }
            }

            if (columns.Count == 0)
                return Results.NotFound(new { message = $"Table '{targetSchema}.{tableName}' not found or has no columns." });

            // Identify Primary Key
            string? pkColumn = null;
            await using (var pkCmd = new NpgsqlCommand(@"
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu 
                  ON tc.constraint_name = kcu.constraint_name 
                 AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY' 
                  AND tc.table_schema = @schema 
                  AND tc.table_name = @table
                LIMIT 1", conn))
            {
                pkCmd.Parameters.AddWithValue("schema", targetSchema);
                pkCmd.Parameters.AddWithValue("table", tableName);
                var pkObj = await pkCmd.ExecuteScalarAsync(ct);
                if (pkObj != null && pkObj != DBNull.Value)
                    pkColumn = pkObj.ToString();
            }

            pkColumn ??= columns.FirstOrDefault(c => c.Name.Equals("id", StringComparison.OrdinalIgnoreCase))?.Name ?? columns[0].Name;

            // Count Total
            long totalCount = 0;
            string countSql = $"SELECT COUNT(*) FROM \"{targetSchema}\".\"{tableName}\"";
            var textCols = columns.Where(c => c.DataType.Contains("char") || c.DataType.Contains("text")).Take(5).ToList();
            if (!string.IsNullOrWhiteSpace(search) && textCols.Count > 0)
            {
                var searchClauses = string.Join(" OR ", textCols.Select(c => $"\"{c.Name}\"::text ILIKE @search"));
                countSql += $" WHERE ({searchClauses})";
            }

            await using (var countCmd = new NpgsqlCommand(countSql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search) && textCols.Count > 0)
                    countCmd.Parameters.AddWithValue("search", $"%{search.Trim()}%");
                var cnt = await countCmd.ExecuteScalarAsync(ct);
                totalCount = Convert.ToInt64(cnt);
            }

            // Fetch Rows
            var records = new List<Dictionary<string, object?>>();
            string selectSql = $"SELECT * FROM \"{targetSchema}\".\"{tableName}\"";
            if (!string.IsNullOrWhiteSpace(search) && textCols.Count > 0)
            {
                var searchClauses = string.Join(" OR ", textCols.Select(c => $"\"{c.Name}\"::text ILIKE @search"));
                selectSql += $" WHERE ({searchClauses})";
            }
            selectSql += $" ORDER BY \"{pkColumn}\" DESC LIMIT @limit OFFSET @offset";

            await using (var selectCmd = new NpgsqlCommand(selectSql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search) && textCols.Count > 0)
                    selectCmd.Parameters.AddWithValue("search", $"%{search.Trim()}%");
                selectCmd.Parameters.AddWithValue("limit", take);
                selectCmd.Parameters.AddWithValue("offset", offset);

                await using var reader = await selectCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        var colName = reader.GetName(i);
                        var val = reader.IsDBNull(i) ? null : reader.GetValue(i);
                        if (val is DateTime dt)
                            val = dt.ToString("o");
                        else if (val is DateTimeOffset dto)
                            val = dto.ToString("o");
                        else if (val is Guid g)
                            val = g.ToString();
                        else if (val is byte[] bytes)
                            val = Convert.ToBase64String(bytes);

                        row[colName] = val;
                    }
                    records.Add(row);
                }
            }

            return Results.Ok(new TableRecordsResult(
                targetSchema,
                tableName,
                pkColumn,
                columns,
                totalCount,
                currentPage,
                take,
                (int)Math.Ceiling((double)totalCount / take),
                records
            ));
        })
        .WithName("GetTableRecords");

        // 7. POST /api/admin/tables/{tableName}/delete-records - Delete specific individual records
        group.MapPost("/{tableName}/delete-records", async (
            string tableName,
            [FromQuery] string? schema,
            [FromBody] DeleteRecordsRequest request,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            if (!IsValidSqlIdentifier(targetSchema) || !IsValidSqlIdentifier(tableName))
                return Results.BadRequest(new { message = "Invalid schema or table identifier." });

            if (string.IsNullOrWhiteSpace(request?.KeyColumn) || !IsValidSqlIdentifier(request.KeyColumn))
                return Results.BadRequest(new { message = "Invalid key column identifier." });

            if (request.RecordKeys == null || request.RecordKeys.Length == 0)
                return Results.BadRequest(new { message = "No record keys provided for deletion." });

            var table = await tenantDb.TableRegistries
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.SchemaName == targetSchema && t.TableName == tableName, ct);

            if (table != null && (table.IsSystem || tableName.StartsWith("__")))
                return Results.BadRequest(new { message = $"Cannot delete records from internal system table '{tableName}'." });

            var connectionString = tenantDb.Database.GetConnectionString()
                ?? throw new InvalidOperationException("Database connection string not configured.");

            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            try
            {
                string deleteSql = $"DELETE FROM \"{targetSchema}\".\"{tableName}\" WHERE \"{request.KeyColumn}\"::text = ANY(@keys)";
                await using var cmd = new NpgsqlCommand(deleteSql, conn);
                cmd.Parameters.AddWithValue("keys", request.RecordKeys);

                var affected = await cmd.ExecuteNonQueryAsync(ct);
                return Results.Ok(new {
                    success = true,
                    deletedCount = affected,
                    message = $"Successfully deleted {affected} record(s) from '{targetSchema}.{tableName}'."
                });
            }
            catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return Results.Conflict(new {
                    success = false,
                    error = "FOREIGN_KEY_VIOLATION",
                    message = "One or more records cannot be deleted because other dependent records reference them (Foreign Key constraint)."
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new {
                    success = false,
                    error = "DELETE_FAILED",
                    message = ex.Message
                });
            }
        })
        .WithName("DeleteTableRecords");

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
                UPDATE tenant.table_registry
                SET is_system = (table_name LIKE '__%' OR table_name = 'table_registry')
                WHERE schema_name = '" + schemaName + @"';

                IF EXISTS (
                    SELECT 1 FROM pg_proc p 
                    JOIN pg_namespace n ON p.pronamespace = n.oid 
                    WHERE n.nspname = 'tenant' AND p.proname = 'sync_table_registry'
                ) THEN
                    PERFORM tenant.sync_table_registry('" + schemaName + @"');
                    UPDATE tenant.table_registry
                    SET is_system = (table_name LIKE '__%' OR table_name = 'table_registry')
                    WHERE schema_name = '" + schemaName + @"';
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

public sealed record ClearTableRequest(string? ConfirmTableName = null);
public sealed record ClearAllTablesRequest(string? Confirmation = null);
public sealed record ClearSelectedTablesRequest(string[] Tables, string? Confirmation);
public sealed record RestoreTablesRequest(string? Confirmation);
public sealed record DropSchemaRequest(string? Confirmation);

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

public sealed record DeleteRecordsRequest(string KeyColumn, string[] RecordKeys);

public sealed record TableColumnMetaDto(string Name, string DataType, bool IsNullable);

public sealed record TableRecordsResult(
    string Schema,
    string TableName,
    string PrimaryKeyColumn,
    IReadOnlyList<TableColumnMetaDto> Columns,
    long TotalRecords,
    int Page,
    int PageSize,
    int TotalPages,
    IReadOnlyList<Dictionary<string, object?>> Records);


