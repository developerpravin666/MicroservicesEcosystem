using System.Data;
using System.Text.Json;
using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Models;
using AccountingInventory.Infrastructure.Persistence;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace AccountingInventory.Infrastructure.Persistence.Services;

public sealed class DatabaseBackupService(
    IConfiguration configuration,
    TenantDbContext tenantDb,
    ILogger<DatabaseBackupService> logger) : IDatabaseBackupService
{
    private const string MetadataSheetName = "_BACKUP_METADATA";

    private string GetConnectionString()
    {
        return configuration.GetConnectionString("AccountingInventoryDb")
            ?? tenantDb.Database.GetConnectionString()
            ?? throw new InvalidOperationException("ConnectionStrings:AccountingInventoryDb must be configured.");
    }

    public async Task<IReadOnlyList<BackupTableInfoDto>> GetEligibleTablesAsync(string schema, CancellationToken ct)
    {
        ValidateIdentifier(schema);
        var connStr = GetConnectionString();
        var result = new List<BackupTableInfoDto>();

        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        // Fetch tables from information_schema (case-insensitive schema matching)
        var tableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = new NpgsqlCommand(@"
            SELECT table_name 
            FROM information_schema.tables 
            WHERE LOWER(table_schema) = LOWER(@schema) AND table_type = 'BASE TABLE'
              AND table_name NOT LIKE '__%' 
              AND table_name NOT IN ('table_registry', 'spatial_ref_sys')
              AND LOWER(table_schema) NOT LIKE 'table_clear_%'
              AND LOWER(table_schema) NOT LIKE 'admin_table_clear%'
            ORDER BY table_name", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema.Trim());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                tableNames.Add(reader.GetString(0));
            }
        }

        // Fetch registered metadata if available
        var registryList = await tenantDb.TableRegistries
            .AsNoTracking()
            .Where(t => t.SchemaName.ToLower() == schema.ToLower())
            .ToListAsync(ct);

        var registry = registryList.ToDictionary(t => t.TableName, StringComparer.OrdinalIgnoreCase);

        // Also add tables from registry that are not system/internal
        foreach (var reg in registryList)
        {
            if (!reg.TableName.StartsWith("__") &&
                !string.Equals(reg.TableName, "table_registry", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(reg.TableName, "spatial_ref_sys", StringComparison.OrdinalIgnoreCase))
            {
                tableNames.Add(reg.TableName);
            }
        }

        // If no tables found and requested schema was "tenant", fallback to first active tenant store schema
        if (tableNames.Count == 0 && string.Equals(schema, "tenant", StringComparison.OrdinalIgnoreCase))
        {
            var fallbackSchema = await tenantDb.Tenants
                .AsNoTracking()
                .Where(t => t.IsActive && !t.IsDeleted && t.SchemaName != "tenant")
                .Select(t => t.SchemaName)
                .FirstOrDefaultAsync(ct);

            if (!string.IsNullOrWhiteSpace(fallbackSchema) && IsValidSqlIdentifier(fallbackSchema))
            {
                logger.LogInformation("No tables found in 'tenant' schema; falling back to active store schema '{FallbackSchema}'", fallbackSchema);
                return await GetEligibleTablesAsync(fallbackSchema, ct);
            }
        }

        foreach (var tableName in tableNames)
        {
            long rowCount = 0;
            try
            {
                await using var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM \"{schema}\".\"{tableName}\"", conn);
                var scalar = await countCmd.ExecuteScalarAsync(ct);
                rowCount = Convert.ToInt64(scalar);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not get row count for table {Schema}.{Table}", schema, tableName);
            }

            registry.TryGetValue(tableName, out var reg);

            result.Add(new BackupTableInfoDto(
                TableName: tableName,
                DisplayName: reg?.DisplayName ?? FormatDisplayName(tableName),
                TableType: reg?.TableType ?? (tableName.StartsWith("chart_") || tableName.EndsWith("_types") || tableName.EndsWith("_categories") ? "Master" : "Data"),
                Category: reg?.Category ?? "General",
                RowCount: rowCount,
                IsSelected: true,
                IsSystem: reg?.IsSystem ?? false
            ));
        }

        return result
            .OrderBy(t => t.TableType == "Master" ? 0 : 1)
            .ThenBy(t => t.Category)
            .ThenBy(t => t.DisplayName)
            .ToList();
    }

    public async Task<DatabaseBackupExportResult> ExportTablesToExcelAsync(
        string schema,
        IReadOnlyList<string>? selectedTables,
        string requestedBy,
        CancellationToken ct)
    {
        ValidateIdentifier(schema);
        var allTables = await GetEligibleTablesAsync(schema, ct);

        var targetTables = (selectedTables == null || selectedTables.Count == 0)
            ? allTables.Select(t => t.TableName).ToList()
            : allTables.Where(t => selectedTables.Contains(t.TableName, StringComparer.OrdinalIgnoreCase)).Select(t => t.TableName).ToList();

        if (targetTables.Count == 0)
        {
            throw new InvalidOperationException("No eligible tables selected for export.");
        }

        using var workbook = new XLWorkbook();
        long totalRows = 0;
        var metadataList = new List<(string SheetName, string TableName, string DisplayName, long RowCount, string PkColumn)>();

        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        foreach (var tableName in targetTables)
        {
            ValidateIdentifier(tableName);
            var tableMeta = allTables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            var displayName = tableMeta?.DisplayName ?? FormatDisplayName(tableName);

            // Fetch columns
            var columns = await GetTableColumnsAsync(conn, schema, tableName, ct);
            if (columns.Count == 0) continue;

            var pkColumn = await GetPrimaryKeyColumnAsync(conn, schema, tableName, ct) ?? columns[0].Name;

            // Generate sanitized sheet name (max 31 chars)
            var sheetName = SanitizeSheetName(tableName);
            var sheetIndex = 1;
            while (workbook.Worksheets.Contains(sheetName))
            {
                var suffix = $"_{sheetIndex++}";
                sheetName = sheetName.Length + suffix.Length > 31
                    ? sheetName[..(31 - suffix.Length)] + suffix
                    : sheetName + suffix;
            }

            var ws = workbook.Worksheets.Add(sheetName);

            // Style headers
            for (var c = 0; c < columns.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = columns[c].Name;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(27, 54, 93); // Professional Navy
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            }
            ws.Row(1).Height = 24;
            ws.SheetView.FreezeRows(1);

            // Stream rows
            long tableRowCount = 0;
            string selectSql = $"SELECT * FROM \"{schema}\".\"{tableName}\" ORDER BY \"{pkColumn}\" ASC";
            await using (var cmd = new NpgsqlCommand(selectSql, conn))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                var rowIdx = 2;
                while (await reader.ReadAsync(ct))
                {
                    tableRowCount++;
                    totalRows++;

                    for (var c = 0; c < columns.Count; c++)
                    {
                        var cell = ws.Cell(rowIdx, c + 1);
                        if (reader.IsDBNull(c))
                        {
                            cell.Value = string.Empty;
                            continue;
                        }

                        var val = reader.GetValue(c);
                        SetCellValueFormatted(cell, val, columns[c].DataType);
                    }

                    if (rowIdx % 2 == 1)
                    {
                        ws.Row(rowIdx).Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252); // Alternating light gray
                    }

                    rowIdx++;
                }
            }

            // Adjust column widths safely
            ws.Columns().AdjustToContents(1, Math.Min(100, (int)tableRowCount + 1));
            foreach (var col in ws.Columns())
            {
                if (col.Width > 50) col.Width = 50;
                if (col.Width < 12) col.Width = 12;
            }

            metadataList.Add((sheetName, tableName, displayName, tableRowCount, pkColumn));
        }

        // Add _BACKUP_METADATA Sheet as the first sheet
        var metaWs = workbook.Worksheets.Add(MetadataSheetName, 1);
        metaWs.Cell(1, 1).Value = "BACKUP METADATA & RECOVERY MANIFEST";
        metaWs.Range(1, 1, 1, 5).Merge().Style.Font.Bold = true;
        metaWs.Range(1, 1, 1, 5).Style.Font.FontSize = 14;
        metaWs.Range(1, 1, 1, 5).Style.Font.FontColor = XLColor.White;
        metaWs.Range(1, 1, 1, 5).Style.Fill.BackgroundColor = XLColor.FromArgb(15, 23, 42); // Slate 900
        metaWs.Row(1).Height = 30;

        metaWs.Cell(3, 1).Value = "Schema Name:";
        metaWs.Cell(3, 2).Value = schema;
        metaWs.Cell(4, 1).Value = "Exported At (UTC):";
        metaWs.Cell(4, 2).Value = DateTime.UtcNow.ToString("o");
        metaWs.Cell(5, 1).Value = "Exported By:";
        metaWs.Cell(5, 2).Value = requestedBy;
        metaWs.Cell(6, 1).Value = "Format Version:";
        metaWs.Cell(6, 2).Value = "1.0";
        metaWs.Cell(7, 1).Value = "Total Tables:";
        metaWs.Cell(7, 2).Value = metadataList.Count;
        metaWs.Cell(8, 1).Value = "Total Records:";
        metaWs.Cell(8, 2).Value = totalRows;

        metaWs.Range(3, 1, 8, 1).Style.Font.Bold = true;

        // Table manifest
        metaWs.Cell(10, 1).Value = "Sheet Name";
        metaWs.Cell(10, 2).Value = "Database Table Name";
        metaWs.Cell(10, 3).Value = "Display Name";
        metaWs.Cell(10, 4).Value = "Record Count";
        metaWs.Cell(10, 5).Value = "Primary Key Column";

        metaWs.Range(10, 1, 10, 5).Style.Font.Bold = true;
        metaWs.Range(10, 1, 10, 5).Style.Font.FontColor = XLColor.White;
        metaWs.Range(10, 1, 10, 5).Style.Fill.BackgroundColor = XLColor.FromArgb(30, 41, 59);
        metaWs.Row(10).Height = 22;

        var mRow = 11;
        foreach (var m in metadataList)
        {
            metaWs.Cell(mRow, 1).Value = m.SheetName;
            metaWs.Cell(mRow, 2).Value = m.TableName;
            metaWs.Cell(mRow, 3).Value = m.DisplayName;
            metaWs.Cell(mRow, 4).Value = m.RowCount;
            metaWs.Cell(mRow, 5).Value = m.PkColumn;
            mRow++;
        }

        metaWs.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        var bytes = memoryStream.ToArray();

        var fileName = $"{schema}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
        return new DatabaseBackupExportResult(
            Content: bytes,
            FileName: fileName,
            ContentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TableCount: metadataList.Count,
            TotalRows: totalRows);
    }

    public Task<InspectBackupFileResultDto> InspectBackupFileAsync(Stream excelStream, string fileName, CancellationToken ct)
    {
        excelStream.Seek(0, SeekOrigin.Begin);
        using var workbook = new XLWorkbook(excelStream);

        string? sourceSchema = null;
        DateTime? exportedAt = null;
        string? formatVersion = null;
        var tableMapping = new Dictionary<string, (string TableName, string DisplayName)>(StringComparer.OrdinalIgnoreCase);

        // Check for metadata sheet
        if (workbook.TryGetWorksheet(MetadataSheetName, out var metaWs))
        {
            sourceSchema = metaWs.Cell(3, 2).GetString();
            if (DateTime.TryParse(metaWs.Cell(4, 2).GetString(), out var dt))
            {
                exportedAt = dt;
            }
            formatVersion = metaWs.Cell(6, 2).GetString();

            // Read table catalog
            var lastMetaRow = metaWs.LastRowUsed()?.RowNumber() ?? 10;
            for (var r = 11; r <= lastMetaRow; r++)
            {
                var sName = metaWs.Cell(r, 1).GetString().Trim();
                var tName = metaWs.Cell(r, 2).GetString().Trim();
                var dName = metaWs.Cell(r, 3).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(sName) && !string.IsNullOrWhiteSpace(tName))
                {
                    tableMapping[sName] = (tName, string.IsNullOrWhiteSpace(dName) ? FormatDisplayName(tName) : dName);
                }
            }
        }

        var tables = new List<BackupTableInfoDto>();
        long totalRows = 0;

        foreach (var ws in workbook.Worksheets)
        {
            if (ws.Name.Equals(MetadataSheetName, StringComparison.OrdinalIgnoreCase)) continue;

            string tableName = ws.Name;
            string displayName = FormatDisplayName(ws.Name);

            if (tableMapping.TryGetValue(ws.Name, out var mapped))
            {
                tableName = mapped.TableName;
                displayName = mapped.DisplayName;
            }

            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            var rowCount = Math.Max(0, lastRow - 1);
            totalRows += rowCount;

            tables.Add(new BackupTableInfoDto(
                TableName: tableName,
                DisplayName: displayName,
                TableType: (tableName.StartsWith("chart_") || tableName.EndsWith("_types") || tableName.EndsWith("_categories")) ? "Master" : "Data",
                Category: "General",
                RowCount: rowCount,
                IsSelected: true,
                IsSystem: false
            ));
        }

        return Task.FromResult(new InspectBackupFileResultDto(
            FileName: fileName,
            SourceSchema: sourceSchema,
            ExportedAtUtc: exportedAt,
            FormatVersion: formatVersion,
            TotalTablesInFile: tables.Count,
            TotalRowsInFile: totalRows,
            Tables: tables
        ));
    }

    public async Task<DatabaseRestoreResultDto> RestoreFromExcelAsync(
        string schema,
        Stream excelStream,
        string fileName,
        IReadOnlyList<string>? selectedTables,
        Guid? userId,
        string userEmail,
        CancellationToken ct)
    {
        ValidateIdentifier(schema);
        excelStream.Seek(0, SeekOrigin.Begin);
        using var workbook = new XLWorkbook(excelStream);

        // Map sheet names to full table names
        var sheetToTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (workbook.TryGetWorksheet(MetadataSheetName, out var metaWs))
        {
            var lastMetaRow = metaWs.LastRowUsed()?.RowNumber() ?? 10;
            for (var r = 11; r <= lastMetaRow; r++)
            {
                var sName = metaWs.Cell(r, 1).GetString().Trim();
                var tName = metaWs.Cell(r, 2).GetString().Trim();
                if (!string.IsNullOrWhiteSpace(sName) && !string.IsNullOrWhiteSpace(tName))
                {
                    sheetToTable[sName] = tName;
                }
            }
        }

        // Determine all sheets to process
        var sheetsToProcess = new List<(IXLWorksheet Sheet, string TableName)>();
        foreach (var ws in workbook.Worksheets)
        {
            if (ws.Name.Equals(MetadataSheetName, StringComparison.OrdinalIgnoreCase)) continue;

            var tableName = sheetToTable.TryGetValue(ws.Name, out var mappedName) ? mappedName : ws.Name;
            if (selectedTables == null || selectedTables.Count == 0 || selectedTables.Contains(tableName, StringComparer.OrdinalIgnoreCase))
            {
                sheetsToProcess.Add((ws, tableName));
            }
        }

        if (sheetsToProcess.Count == 0)
        {
            return new DatabaseRestoreResultDto(
                Success: false,
                Message: "No matching tables selected for restore.",
                TargetSchema: schema,
                BackupFileName: fileName,
                RestoredAtUtc: DateTime.UtcNow,
                TotalTablesInFile: workbook.Worksheets.Count - (workbook.Worksheets.Contains(MetadataSheetName) ? 1 : 0),
                SelectedTablesCount: 0,
                TotalRowsProcessed: 0,
                TotalInserted: 0,
                TotalUpdated: 0,
                TotalUnchanged: 0,
                TotalErrors: 0,
                TableAudits: Array.Empty<TableRestoreAuditDto>()
            );
        }

        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        // Sort tables in topological order respecting foreign keys
        var topologicalOrder = await GetTopologicalTableOrderAsync(conn, schema, sheetsToProcess.Select(s => s.TableName).ToList(), ct);
        var orderedSheets = sheetsToProcess
            .OrderBy(s => topologicalOrder.IndexOf(s.TableName.ToLowerInvariant()))
            .ToList();

        var tableAudits = new List<TableRestoreAuditDto>();
        int totalRowsProcessed = 0;
        int totalInserted = 0;
        int totalUpdated = 0;
        int totalUnchanged = 0;
        int totalErrors = 0;

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Defer all constraints within the transaction where possible
            try
            {
                await using var deferCmd = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED;", conn, tx);
                await deferCmd.ExecuteNonQueryAsync(ct);
            }
            catch
            {
                // Deferring might not apply to immediate constraints, which is fine since topological sorting handles them
            }

            foreach (var (ws, tableName) in orderedSheets)
            {
                ValidateIdentifier(tableName);
                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                var totalSheetRows = Math.Max(0, lastRow - 1);

                if (totalSheetRows == 0)
                {
                    tableAudits.Add(new TableRestoreAuditDto(
                        TableName: tableName,
                        DisplayName: FormatDisplayName(tableName),
                        RowsInFile: 0,
                        InsertedCount: 0,
                        UpdatedCount: 0,
                        UnchangedCount: 0,
                        ErrorCount: 0,
                        Status: "Success",
                        SampleDifferences: Array.Empty<string>()
                    ));
                    continue;
                }

                // Verify table exists in destination schema
                var columns = await GetTableColumnsAsync(conn, schema, tableName, ct);
                if (columns.Count == 0)
                {
                    tableAudits.Add(new TableRestoreAuditDto(
                        TableName: tableName,
                        DisplayName: FormatDisplayName(tableName),
                        RowsInFile: totalSheetRows,
                        InsertedCount: 0,
                        UpdatedCount: 0,
                        UnchangedCount: 0,
                        ErrorCount: totalSheetRows,
                        Status: "Failed",
                        SampleDifferences: Array.Empty<string>(),
                        ErrorMessage: $"Table '{schema}.{tableName}' does not exist in target database."
                    ));
                    totalErrors += totalSheetRows;
                    continue;
                }

                var colMap = columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
                var pkCol = await GetPrimaryKeyColumnAsync(conn, schema, tableName, ct) ?? columns[0].Name;

                // Read header row
                var lastCol = ws.LastColumnUsed()?.ColumnNumber() ?? 1;
                var headerCols = new List<(int ColIndex, string ColName)>();
                for (var c = 1; c <= lastCol; c++)
                {
                    var colName = ws.Cell(1, c).GetString().Trim();
                    if (!string.IsNullOrWhiteSpace(colName) && colMap.ContainsKey(colName))
                    {
                        headerCols.Add((c, colName));
                    }
                }

                int insertedCount = 0;
                int updatedCount = 0;
                int unchangedCount = 0;
                int errorCount = 0;
                var sampleDiffs = new List<string>();

                for (var r = 2; r <= lastRow; r++)
                {
                    totalRowsProcessed++;
                    var spName = $"sp_{r}";
                    await using (var spCmd = new NpgsqlCommand($"SAVEPOINT {spName};", conn, tx))
                    {
                        await spCmd.ExecuteNonQueryAsync(ct);
                    }

                    try
                    {
                        var rowDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (colIdx, colName) in headerCols)
                        {
                            var cell = ws.Cell(r, colIdx);
                            var isPk = colName.Equals(pkCol, StringComparison.OrdinalIgnoreCase);
                            var val = ConvertCellValue(cell, colMap[colName].DataType, isPk);
                            rowDict[colName] = val;
                        }

                        if (!rowDict.TryGetValue(pkCol, out var pkVal) || pkVal == null || pkVal == DBNull.Value)
                        {
                            // Missing PK: treat as new insert
                            await ExecuteInsertAsync(conn, tx, schema, tableName, rowDict, colMap, ct);
                            insertedCount++;
                            totalInserted++;

                            await using var relCmd = new NpgsqlCommand($"RELEASE SAVEPOINT {spName};", conn, tx);
                            await relCmd.ExecuteNonQueryAsync(ct);
                            continue;
                        }

                        // Check if row already exists in DB
                        var existingRow = await GetExistingRowAsync(conn, tx, schema, tableName, pkCol, pkVal, ct);

                        if (existingRow == null)
                        {
                            // Does not exist: INSERT
                            await ExecuteInsertAsync(conn, tx, schema, tableName, rowDict, colMap, ct);
                            insertedCount++;
                            totalInserted++;
                        }
                        else
                        {
                            // Exists: Compare differences
                            var differences = FindDifferences(existingRow, rowDict);
                            if (differences.Count > 0)
                            {
                                await ExecuteUpdateAsync(conn, tx, schema, tableName, pkCol, pkVal, rowDict, colMap, ct);
                                updatedCount++;
                                totalUpdated++;

                                if (sampleDiffs.Count < 5)
                                {
                                    sampleDiffs.Add($"Row '{pkVal}': {string.Join(", ", differences.Take(3))}");
                                }
                            }
                            else
                            {
                                unchangedCount++;
                                totalUnchanged++;
                            }
                        }

                        await using var releaseCmd = new NpgsqlCommand($"RELEASE SAVEPOINT {spName};", conn, tx);
                        await releaseCmd.ExecuteNonQueryAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        await using (var rbCmd = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT {spName};", conn, tx))
                        {
                            await rbCmd.ExecuteNonQueryAsync(ct);
                        }

                        errorCount++;
                        totalErrors++;
                        if (sampleDiffs.Count < 5)
                        {
                            sampleDiffs.Add(FormatRowError(r, ex));
                        }
                    }
                }

                // Resync table primary key sequences so subsequent store operations never collide
                await ResyncTableSequencesAsync(conn, tx, schema, tableName, pkCol, ct);

                tableAudits.Add(new TableRestoreAuditDto(
                    TableName: tableName,
                    DisplayName: FormatDisplayName(tableName),
                    RowsInFile: totalSheetRows,
                    InsertedCount: insertedCount,
                    UpdatedCount: updatedCount,
                    UnchangedCount: unchangedCount,
                    ErrorCount: errorCount,
                    Status: errorCount > 0 ? (insertedCount > 0 || updatedCount > 0 ? "Partial" : "Failed") : "Success",
                    SampleDifferences: sampleDiffs
                ));
            }

            // Create system audit log record in audit_logs table if it exists
            Guid? auditLogId = null;
            try
            {
                auditLogId = Guid.NewGuid();
                var summaryJson = JsonSerializer.Serialize(new
                {
                    FileName = fileName,
                    TargetSchema = schema,
                    TotalInserted = totalInserted,
                    TotalUpdated = totalUpdated,
                    TotalUnchanged = totalUnchanged,
                    TotalErrors = totalErrors,
                    RestoredBy = userEmail
                });

                string auditInsert = @"
                    INSERT INTO """ + schema + @""".audit_logs (id, table_name, record_id, action, old_value, new_value, created_by, created_date, tenant_schema)
                    VALUES (@id, 'DATABASE_RESTORE', @recordId, 'EXCEL_RESTORE', NULL, @newVal, @userId, @createdDate, @schema)
                    ON CONFLICT DO NOTHING";

                await using var auditCmd = new NpgsqlCommand(auditInsert, conn, tx);
                auditCmd.Parameters.AddWithValue("id", auditLogId.Value);
                auditCmd.Parameters.AddWithValue("recordId", fileName);
                auditCmd.Parameters.AddWithValue("newVal", summaryJson);
                auditCmd.Parameters.AddWithValue("userId", (object?)userId ?? DBNull.Value);
                auditCmd.Parameters.AddWithValue("createdDate", DateTimeOffset.UtcNow);
                auditCmd.Parameters.AddWithValue("schema", schema);
                await auditCmd.ExecuteNonQueryAsync(ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not write entry to audit_logs table during restore.");
            }

            await tx.CommitAsync(ct);

            return new DatabaseRestoreResultDto(
                Success: totalErrors == 0 || (totalInserted > 0 || totalUpdated > 0),
                Message: $"Restore completed successfully. Inserted: {totalInserted}, Updated: {totalUpdated}, Unchanged: {totalUnchanged}, Errors: {totalErrors}.",
                TargetSchema: schema,
                BackupFileName: fileName,
                RestoredAtUtc: DateTime.UtcNow,
                TotalTablesInFile: orderedSheets.Count,
                SelectedTablesCount: orderedSheets.Count,
                TotalRowsProcessed: totalRowsProcessed,
                TotalInserted: totalInserted,
                TotalUpdated: totalUpdated,
                TotalUnchanged: totalUnchanged,
                TotalErrors: totalErrors,
                TableAudits: tableAudits,
                AuditLogId: auditLogId
            );
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex, "Transaction rolled back during database restore from Excel file {FileName}", fileName);
            throw new InvalidOperationException($"Database restore failed and changes were rolled back: {ex.Message}", ex);
        }
    }
    #region In-Database Temp-Table Snapshot Backup & Restore

    private static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    public async Task<DatabaseSnapshotDto> CreateDatabaseSnapshotAsync(
        string schema,
        IReadOnlyList<string>? selectedTables,
        string? backupName,
        string? notes,
        string createdBy,
        CancellationToken ct)
    {
        ValidateIdentifier(schema);
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await EnsureBackupRegistryTablesAsync(conn, ct);

        var allTables = await GetEligibleTablesAsync(schema, ct);
        var targetTables = (selectedTables == null || selectedTables.Count == 0)
            ? allTables.Select(t => t.TableName).ToList()
            : allTables.Where(t => selectedTables.Contains(t.TableName, StringComparer.OrdinalIgnoreCase)).Select(t => t.TableName).ToList();

        if (targetTables.Count == 0)
        {
            throw new InvalidOperationException("No eligible tables selected for backup.");
        }

        // Format: {{tenant db name}}_bkp_YYYYMMDD for the tenant being backed up
        var tenant = await tenantDb.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.SchemaName == schema || t.Slug == schema, ct);

        var rawTenantDbName = tenant?.SchemaName ?? schema;
        var safeTenantDbName = new string(rawTenantDbName.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray()).Trim('_');
        if (string.IsNullOrWhiteSpace(safeTenantDbName)) safeTenantDbName = "tenant";

        // PostgreSQL max identifier length is 63 chars. Ensure prefix leaves room for _bkp_YYYYMMDD_xx
        if (safeTenantDbName.Length > 45)
        {
            safeTenantDbName = safeTenantDbName[..45].TrimEnd('_');
        }

        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var baseSchemaName = $"{safeTenantDbName}_bkp_{dateStr}";
        var backupSchemaName = baseSchemaName;
        int counter = 2;
        while (await SchemaExistsAsync(conn, backupSchemaName, ct))
        {
            backupSchemaName = $"{baseSchemaName}_{counter}";
            counter++;
        }

        ValidateIdentifier(backupSchemaName);

        // Create the dedicated backup schema in the same database
        await using (var createSchemaCmd = new NpgsqlCommand($"CREATE SCHEMA IF NOT EXISTS {Quote(backupSchemaName)};", conn))
        {
            await createSchemaCmd.ExecuteNonQueryAsync(ct);
        }

        var snapshotId = Guid.NewGuid();
        var tenantDisplayName = tenant?.Name ?? rawTenantDbName;
        var finalBackupName = string.IsNullOrWhiteSpace(backupName)
            ? $"{backupSchemaName} ({tenantDisplayName})"
            : backupName.Trim();

        var tableInfos = new List<SnapshotTableInfoDto>();
        long totalRows = 0;

        for (var i = 0; i < targetTables.Count; i++)
        {
            var tableName = targetTables[i];
            ValidateIdentifier(tableName);
            var meta = allTables.FirstOrDefault(t => t.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
            var displayName = meta?.DisplayName ?? FormatDisplayName(tableName);

            // Copy table into the backup schema with actual table name
            var createSql = $"CREATE TABLE {Quote(backupSchemaName)}.{Quote(tableName)} AS SELECT * FROM {Quote(schema)}.{Quote(tableName)};";
            await using (var createCmd = new NpgsqlCommand(createSql, conn))
            {
                await createCmd.ExecuteNonQueryAsync(ct);
            }

            long rowCount = 0;
            await using (var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {Quote(backupSchemaName)}.{Quote(tableName)}", conn))
            {
                var scalar = await countCmd.ExecuteScalarAsync(ct);
                rowCount = Convert.ToInt64(scalar);
            }

            totalRows += rowCount;
            tableInfos.Add(new SnapshotTableInfoDto(
                TableName: tableName,
                DisplayName: displayName,
                RowCount: rowCount,
                TempTableName: tableName
            ));
        }

        var tablesJson = JsonSerializer.Serialize(tableInfos);
        var createdAt = DateTime.UtcNow;

        // Insert into tenant.database_backups
        var insertSql = @"
            INSERT INTO tenant.database_backups
            (id, backup_name, backup_schema_name, database_name, source_schema, total_tables, total_rows, tables_json, status, notes, created_at_utc, created_by)
            VALUES (@id, @name, @schema_name, @db_name, @src_schema, @totalTables, @totalRows, @tablesJson, 'Active', @notes, @createdAt, @createdBy)";

        await using (var insertCmd = new NpgsqlCommand(insertSql, conn))
        {
            insertCmd.Parameters.AddWithValue("id", snapshotId);
            insertCmd.Parameters.AddWithValue("name", finalBackupName);
            insertCmd.Parameters.AddWithValue("schema_name", backupSchemaName);
            insertCmd.Parameters.AddWithValue("db_name", rawTenantDbName);
            insertCmd.Parameters.AddWithValue("src_schema", schema);
            insertCmd.Parameters.AddWithValue("totalTables", tableInfos.Count);
            insertCmd.Parameters.AddWithValue("totalRows", totalRows);
            insertCmd.Parameters.AddWithValue("tablesJson", tablesJson);
            insertCmd.Parameters.AddWithValue("notes", (object?)notes ?? DBNull.Value);
            insertCmd.Parameters.AddWithValue("createdAt", createdAt);
            insertCmd.Parameters.AddWithValue("createdBy", createdBy);
            await insertCmd.ExecuteNonQueryAsync(ct);
        }

        // Insert into tenant.database_backup_restore_logs
        await using (var logCmd = new NpgsqlCommand(@"
            INSERT INTO tenant.database_backup_restore_logs
            (id, backup_id, backup_schema_name, target_schema, action, status, total_rows_processed, message, executed_at_utc, executed_by)
            VALUES (@id, @backupId, @schema_name, @target_schema, 'BACKUP_CREATED', 'Success', @rows, @msg, @now, @user)", conn))
        {
            logCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            logCmd.Parameters.AddWithValue("backupId", snapshotId);
            logCmd.Parameters.AddWithValue("schema_name", backupSchemaName);
            logCmd.Parameters.AddWithValue("target_schema", schema);
            logCmd.Parameters.AddWithValue("rows", totalRows);
            logCmd.Parameters.AddWithValue("msg", $"Created backup schema '{backupSchemaName}' for tenant '{rawTenantDbName}' containing {tableInfos.Count} tables and {totalRows} records.");
            logCmd.Parameters.AddWithValue("now", createdAt);
            logCmd.Parameters.AddWithValue("user", createdBy);
            await logCmd.ExecuteNonQueryAsync(ct);
        }

        return new DatabaseSnapshotDto(
            Id: snapshotId,
            BackupName: finalBackupName,
            BackupSchemaName: backupSchemaName,
            DatabaseName: rawTenantDbName,
            SourceSchema: schema,
            TotalTables: tableInfos.Count,
            TotalRows: totalRows,
            Status: "Active",
            CreatedAtUtc: createdAt,
            CreatedBy: createdBy,
            Notes: notes,
            LastRestoredAtUtc: null,
            LastRestoredBy: null,
            DroppedAtUtc: null,
            DroppedBy: null,
            Tables: tableInfos
        );
    }

    public async Task<IReadOnlyList<DatabaseSnapshotDto>> GetDatabaseSnapshotsAsync(string? schema, CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await EnsureBackupRegistryTablesAsync(conn, ct);

        var list = new List<DatabaseSnapshotDto>();

        var cleanedSchema = schema?.Trim();
        var hasSchemaFilter = !string.IsNullOrWhiteSpace(cleanedSchema) && !cleanedSchema.Equals("ALL", StringComparison.OrdinalIgnoreCase);

        var sql = hasSchemaFilter
            ? @"SELECT id, backup_name, backup_schema_name, database_name, source_schema,
                       total_tables, total_rows, status, tables_json, created_at_utc, created_by,
                       notes, last_restored_at_utc, last_restored_by, dropped_at_utc, dropped_by
                FROM tenant.database_backups
                WHERE LOWER(source_schema) = LOWER(@schema)
                ORDER BY created_at_utc DESC"
            : @"SELECT id, backup_name, backup_schema_name, database_name, source_schema,
                       total_tables, total_rows, status, tables_json, created_at_utc, created_by,
                       notes, last_restored_at_utc, last_restored_by, dropped_at_utc, dropped_by
                FROM tenant.database_backups
                ORDER BY created_at_utc DESC";

        await using (var cmd = new NpgsqlCommand(sql, conn))
        {
            if (hasSchemaFilter)
            {
                cmd.Parameters.Add(new NpgsqlParameter("schema", NpgsqlDbType.Text) { Value = cleanedSchema! });
            }
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var name = reader.GetString(1);
                var bkpSchema = reader.GetString(2);
                var dbName = reader.GetString(3);
                var srcSchema = reader.GetString(4);
                var totalTables = reader.GetInt32(5);
                var totalRows = reader.GetInt64(6);
                var status = reader.GetString(7);
                var json = reader.GetString(8);
                var createdAt = reader.GetDateTime(9);
                var createdBy = reader.GetString(10);
                var notes = reader.IsDBNull(11) ? null : reader.GetString(11);
                var lastRestoredAt = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12);
                var lastRestoredBy = reader.IsDBNull(13) ? null : reader.GetString(13);
                var droppedAt = reader.IsDBNull(14) ? (DateTime?)null : reader.GetDateTime(14);
                var droppedBy = reader.IsDBNull(15) ? null : reader.GetString(15);

                var tableInfos = JsonSerializer.Deserialize<List<SnapshotTableInfoDto>>(json) ?? new List<SnapshotTableInfoDto>();

                list.Add(new DatabaseSnapshotDto(
                    Id: id,
                    BackupName: name,
                    BackupSchemaName: bkpSchema,
                    DatabaseName: dbName,
                    SourceSchema: srcSchema,
                    TotalTables: totalTables,
                    TotalRows: totalRows,
                    Status: status,
                    CreatedAtUtc: createdAt,
                    CreatedBy: createdBy,
                    Notes: notes,
                    LastRestoredAtUtc: lastRestoredAt,
                    LastRestoredBy: lastRestoredBy,
                    DroppedAtUtc: droppedAt,
                    DroppedBy: droppedBy,
                    Tables: tableInfos
                ));
            }
        }

        // Live check if schema still exists in PostgreSQL for active backups
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item.Status == "Active")
            {
                var exists = await SchemaExistsAsync(conn, item.BackupSchemaName, ct);
                if (!exists)
                {
                    list[i] = item with { Status = "Dropped" };
                }
            }
        }

        return list;
    }

    public async Task<DatabaseRestoreResultDto> RestoreFromDatabaseSnapshotAsync(
        Guid snapshotId,
        string? targetSchemaOverride,
        IReadOnlyList<string>? selectedTables,
        Guid? userId,
        string userEmail,
        CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await EnsureBackupRegistryTablesAsync(conn, ct);

        string sourceSchema;
        string backupSchemaName;
        string backupName;
        string tablesJson;
        bool isFromTenantRegistry = false;

        await using (var cmd = new NpgsqlCommand("SELECT source_schema, backup_schema_name, backup_name, tables_json FROM tenant.database_backups WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", snapshotId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                sourceSchema = reader.GetString(0);
                backupSchemaName = reader.GetString(1);
                backupName = reader.GetString(2);
                tablesJson = reader.GetString(3);
                isFromTenantRegistry = true;
            }
            else
            {
                sourceSchema = "tenant";
                backupSchemaName = "admin_table_clear";
                backupName = "Backup";
                tablesJson = "[]";
            }
        }

        if (!isFromTenantRegistry)
        {
            await using var checkCmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema='admin_table_clear' AND table_name='database_backup_snapshots')", conn);
            if ((bool)(await checkCmd.ExecuteScalarAsync(ct))!)
            {
                await using var legCmd = new NpgsqlCommand("SELECT source_schema, backup_name, tables_json FROM admin_table_clear.database_backup_snapshots WHERE id = @id", conn);
                legCmd.Parameters.AddWithValue("id", snapshotId);
                await using var reader = await legCmd.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    sourceSchema = reader.GetString(0);
                    backupName = reader.GetString(1);
                    tablesJson = reader.GetString(2);
                }
                else
                {
                    throw new KeyNotFoundException($"Snapshot with ID '{snapshotId}' was not found in database backup registry.");
                }
            }
            else
            {
                throw new KeyNotFoundException($"Snapshot with ID '{snapshotId}' was not found in database backup registry.");
            }
        }

        var targetSchema = string.IsNullOrWhiteSpace(targetSchemaOverride) ? sourceSchema : targetSchemaOverride.Trim();
        ValidateIdentifier(targetSchema);

        var tableInfos = JsonSerializer.Deserialize<List<SnapshotTableInfoDto>>(tablesJson) ?? new List<SnapshotTableInfoDto>();
        var filteredTables = (selectedTables == null || selectedTables.Count == 0)
            ? tableInfos
            : tableInfos.Where(t => selectedTables.Contains(t.TableName, StringComparer.OrdinalIgnoreCase)).ToList();

        if (filteredTables.Count == 0)
        {
            return new DatabaseRestoreResultDto(
                Success: false,
                Message: "No matching tables selected for restore.",
                TargetSchema: targetSchema,
                BackupFileName: backupName,
                RestoredAtUtc: DateTime.UtcNow,
                TotalTablesInFile: tableInfos.Count,
                SelectedTablesCount: 0,
                TotalRowsProcessed: 0,
                TotalInserted: 0,
                TotalUpdated: 0,
                TotalUnchanged: 0,
                TotalErrors: 0,
                TableAudits: Array.Empty<TableRestoreAuditDto>()
            );
        }

        var topologicalOrder = await GetTopologicalTableOrderAsync(conn, targetSchema, filteredTables.Select(t => t.TableName).ToList(), ct);
        var orderedTables = filteredTables
            .OrderBy(s => topologicalOrder.IndexOf(s.TableName.ToLowerInvariant()))
            .ToList();

        var tableAudits = new List<TableRestoreAuditDto>();
        int totalRowsProcessed = 0;
        int totalInserted = 0;
        int totalUpdated = 0;
        int totalUnchanged = 0;
        long totalErrors = 0;

        await using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            try
            {
                await using var deferCmd = new NpgsqlCommand("SET CONSTRAINTS ALL DEFERRED;", conn, tx);
                await deferCmd.ExecuteNonQueryAsync(ct);
            }
            catch { /* immediate constraints fallback */ }

            foreach (var info in orderedTables)
            {
                ValidateIdentifier(info.TableName);
                var sourceSchemaForTable = isFromTenantRegistry ? backupSchemaName : "admin_table_clear";
                var sourceTable = isFromTenantRegistry ? info.TableName : info.TempTableName;

                var columns = await GetTableColumnsAsync(conn, targetSchema, info.TableName, ct);
                if (columns.Count == 0)
                {
                    tableAudits.Add(new TableRestoreAuditDto(
                        TableName: info.TableName,
                        DisplayName: info.DisplayName,
                        RowsInFile: (int)info.RowCount,
                        InsertedCount: 0,
                        UpdatedCount: 0,
                        UnchangedCount: 0,
                        ErrorCount: (int)info.RowCount,
                        Status: "Failed",
                        SampleDifferences: Array.Empty<string>(),
                        ErrorMessage: $"Destination table '{targetSchema}.{info.TableName}' does not exist in target database."
                    ));
                    totalErrors += (int)info.RowCount;
                    continue;
                }

                await using var checkTemp = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema=@schema AND table_name=@name)", conn, tx);
                checkTemp.Parameters.AddWithValue("schema", sourceSchemaForTable);
                checkTemp.Parameters.AddWithValue("name", sourceTable);
                var tempExists = (bool)(await checkTemp.ExecuteScalarAsync(ct))!;
                if (!tempExists)
                {
                    tableAudits.Add(new TableRestoreAuditDto(
                        TableName: info.TableName,
                        DisplayName: info.DisplayName,
                        RowsInFile: (int)info.RowCount,
                        InsertedCount: 0,
                        UpdatedCount: 0,
                        UnchangedCount: 0,
                        ErrorCount: (int)info.RowCount,
                        Status: "Failed",
                        SampleDifferences: Array.Empty<string>(),
                        ErrorMessage: $"Backup table '{sourceSchemaForTable}.{sourceTable}' does not exist in database."
                    ));
                    totalErrors += (int)info.RowCount;
                    continue;
                }

                var colMap = columns.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
                var pkCol = await GetPrimaryKeyColumnAsync(conn, targetSchema, info.TableName, ct) ?? columns[0].Name;
                var colNamesList = columns.Select(c => Quote(c.Name)).ToList();
                var colNamesStr = string.Join(", ", colNamesList);

                int tableInserted = 0;
                int tableUpdated = 0;
                int tableUnchanged = 0;
                int tableErrors = 0;
                var sampleDiffs = new List<string>();

                var selectFromTempSql = $"SELECT {colNamesStr} FROM {Quote(sourceSchemaForTable)}.{Quote(sourceTable)} ORDER BY {Quote(pkCol)} ASC";
                var tempRows = new List<Dictionary<string, object?>>();

                await using (var readCmd = new NpgsqlCommand(selectFromTempSql, conn, tx))
                await using (var reader = await readCmd.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                        for (var c = 0; c < columns.Count; c++)
                        {
                            dict[columns[c].Name] = reader.IsDBNull(c) ? null : reader.GetValue(c);
                        }
                        tempRows.Add(dict);
                    }
                }

                foreach (var rowDict in tempRows)
                {
                    totalRowsProcessed++;
                    var pkVal = rowDict.TryGetValue(pkCol, out var val) ? val : null;
                    var spName = $"sp_row_{Guid.NewGuid():N}";
                    await using (var spCmd = new NpgsqlCommand($"SAVEPOINT {spName};", conn, tx))
                    {
                        await spCmd.ExecuteNonQueryAsync(ct);
                    }

                    try
                    {
                        if (pkVal == null || pkVal == DBNull.Value)
                        {
                            await ExecuteInsertAsync(conn, tx, targetSchema, info.TableName, rowDict, colMap, ct);
                            tableInserted++;
                            totalInserted++;
                        }
                        else
                        {
                            var existingRow = await GetExistingRowAsync(conn, tx, targetSchema, info.TableName, pkCol, pkVal, ct);
                            if (existingRow == null)
                            {
                                await ExecuteInsertAsync(conn, tx, targetSchema, info.TableName, rowDict, colMap, ct);
                                tableInserted++;
                                totalInserted++;
                            }
                            else
                            {
                                var differences = FindDifferences(existingRow, rowDict);
                                if (differences.Count > 0)
                                {
                                    await ExecuteUpdateAsync(conn, tx, targetSchema, info.TableName, pkCol, pkVal, rowDict, colMap, ct);
                                    tableUpdated++;
                                    totalUpdated++;
                                    if (sampleDiffs.Count < 5)
                                    {
                                        sampleDiffs.Add($"Row '{pkVal}': {string.Join(", ", differences.Take(3))}");
                                    }
                                }
                                else
                                {
                                    tableUnchanged++;
                                    totalUnchanged++;
                                }
                            }
                        }

                        await using var relCmd = new NpgsqlCommand($"RELEASE SAVEPOINT {spName};", conn, tx);
                        await relCmd.ExecuteNonQueryAsync(ct);
                    }
                    catch (Exception ex)
                    {
                        await using var rbCmd = new NpgsqlCommand($"ROLLBACK TO SAVEPOINT {spName};", conn, tx);
                        await rbCmd.ExecuteNonQueryAsync(ct);
                        tableErrors++;
                        totalErrors++;
                        if (sampleDiffs.Count < 5)
                        {
                            sampleDiffs.Add($"Row '{pkVal}': Error - {ex.Message}");
                        }
                    }
                }

                tableAudits.Add(new TableRestoreAuditDto(
                    TableName: info.TableName,
                    DisplayName: info.DisplayName,
                    RowsInFile: (int)info.RowCount,
                    InsertedCount: tableInserted,
                    UpdatedCount: tableUpdated,
                    UnchangedCount: tableUnchanged,
                    ErrorCount: tableErrors,
                    Status: tableErrors == 0 ? "Success" : tableInserted + tableUpdated > 0 ? "Partial" : "Failed",
                    SampleDifferences: sampleDiffs
                ));
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            logger.LogError(ex, "Transaction rolled back during database snapshot restore.");
            throw;
        }

        // Update tenant registry with restore record and log
        if (isFromTenantRegistry)
        {
            var now = DateTime.UtcNow;
            await using (var updCmd = new NpgsqlCommand(@"
                UPDATE tenant.database_backups
                SET status = 'Restored', last_restored_at_utc = @now, last_restored_by = @user
                WHERE id = @id", conn))
            {
                updCmd.Parameters.AddWithValue("id", snapshotId);
                updCmd.Parameters.AddWithValue("now", now);
                updCmd.Parameters.AddWithValue("user", userEmail);
                await updCmd.ExecuteNonQueryAsync(ct);
            }

            await using (var logCmd = new NpgsqlCommand(@"
                INSERT INTO tenant.database_backup_restore_logs
                (id, backup_id, backup_schema_name, target_schema, action, status, total_rows_processed, total_inserted, total_updated, total_errors, details_json, message, executed_at_utc, executed_by)
                VALUES (@id, @backupId, @schema_name, @target_schema, 'RESTORE_EXECUTED', @status, @rows, @ins, @upd, @err, @details, @msg, @now, @user)", conn))
            {
                logCmd.Parameters.AddWithValue("id", Guid.NewGuid());
                logCmd.Parameters.AddWithValue("backupId", snapshotId);
                logCmd.Parameters.AddWithValue("schema_name", backupSchemaName);
                logCmd.Parameters.AddWithValue("target_schema", targetSchema);
                logCmd.Parameters.AddWithValue("status", totalErrors == 0 ? "Success" : "Partial");
                logCmd.Parameters.AddWithValue("rows", (long)totalRowsProcessed);
                logCmd.Parameters.AddWithValue("ins", totalInserted);
                logCmd.Parameters.AddWithValue("upd", totalUpdated);
                logCmd.Parameters.AddWithValue("err", (int)totalErrors);
                logCmd.Parameters.AddWithValue("details", JsonSerializer.Serialize(tableAudits));
                logCmd.Parameters.AddWithValue("msg", $"Restored {totalInserted} inserted and {totalUpdated} updated records from backup schema '{backupSchemaName}'.");
                logCmd.Parameters.AddWithValue("now", now);
                logCmd.Parameters.AddWithValue("user", userEmail);
                await logCmd.ExecuteNonQueryAsync(ct);
            }
        }

        return new DatabaseRestoreResultDto(
            Success: totalErrors == 0,
            Message: totalErrors == 0
                ? $"Successfully restored {totalInserted} new and {totalUpdated} updated records directly from backup schema '{backupSchemaName}' ({totalUnchanged} unchanged)."
                : $"Restore completed with errors. {totalInserted} inserted, {totalUpdated} updated, {totalErrors} errors.",
            TargetSchema: targetSchema,
            BackupFileName: backupName,
            RestoredAtUtc: DateTime.UtcNow,
            TotalTablesInFile: tableInfos.Count,
            SelectedTablesCount: orderedTables.Count,
            TotalRowsProcessed: totalRowsProcessed,
            TotalInserted: totalInserted,
            TotalUpdated: totalUpdated,
            TotalUnchanged: totalUnchanged,
            TotalErrors: (int)totalErrors,
            TableAudits: tableAudits
        );
    }

    public async Task<bool> DeleteDatabaseBackupAndDropSchemaAsync(Guid backupId, string userEmail, CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await EnsureBackupRegistryTablesAsync(conn, ct);

        string? backupSchemaName = null;
        await using (var cmd = new NpgsqlCommand("SELECT backup_schema_name FROM tenant.database_backups WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", backupId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                backupSchemaName = reader.GetString(0);
            }
        }

        if (string.IsNullOrWhiteSpace(backupSchemaName))
        {
            return await DeleteDatabaseSnapshotAsync(backupId, ct);
        }

        ValidateIdentifier(backupSchemaName);

        // Drop the backup schema CASCADE
        await using (var dropCmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {Quote(backupSchemaName)} CASCADE;", conn))
        {
            await dropCmd.ExecuteNonQueryAsync(ct);
        }

        var now = DateTime.UtcNow;

        // Update status in tenant.database_backups
        await using (var updCmd = new NpgsqlCommand(@"
            UPDATE tenant.database_backups
            SET status = 'Dropped', dropped_at_utc = @now, dropped_by = @user
            WHERE id = @id", conn))
        {
            updCmd.Parameters.AddWithValue("id", backupId);
            updCmd.Parameters.AddWithValue("now", now);
            updCmd.Parameters.AddWithValue("user", userEmail);
            await updCmd.ExecuteNonQueryAsync(ct);
        }

        // Insert log in tenant.database_backup_restore_logs
        await using (var logCmd = new NpgsqlCommand(@"
            INSERT INTO tenant.database_backup_restore_logs
            (id, backup_id, backup_schema_name, target_schema, action, status, total_rows_processed, message, executed_at_utc, executed_by)
            VALUES (@id, @backupId, @schema_name, 'None', 'SCHEMA_DROPPED', 'Success', 0, @msg, @now, @user)", conn))
        {
            logCmd.Parameters.AddWithValue("id", Guid.NewGuid());
            logCmd.Parameters.AddWithValue("backupId", backupId);
            logCmd.Parameters.AddWithValue("schema_name", backupSchemaName);
            logCmd.Parameters.AddWithValue("msg", $"Dropped backup schema '{backupSchemaName}' and removed all backed up data.");
            logCmd.Parameters.AddWithValue("now", now);
            logCmd.Parameters.AddWithValue("user", userEmail);
            await logCmd.ExecuteNonQueryAsync(ct);
        }

        return true;
    }

    public async Task<bool> DeleteDatabaseSnapshotAsync(Guid snapshotId, CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        // Check if exists in tenant.database_backups first
        await using (var checkCmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_schema='tenant' AND table_name='database_backups')", conn))
        {
            if ((bool)(await checkCmd.ExecuteScalarAsync(ct))!)
            {
                await using var checkBkp = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM tenant.database_backups WHERE id = @id)", conn);
                checkBkp.Parameters.AddWithValue("id", snapshotId);
                if ((bool)(await checkBkp.ExecuteScalarAsync(ct))!)
                {
                    return await DeleteDatabaseBackupAndDropSchemaAsync(snapshotId, "SuperAdmin", ct);
                }
            }
        }

        string? tablesJson = null;
        await using (var cmd = new NpgsqlCommand("SELECT tables_json FROM admin_table_clear.database_backup_snapshots WHERE id = @id", conn))
        {
            cmd.Parameters.AddWithValue("id", snapshotId);
            tablesJson = await cmd.ExecuteScalarAsync(ct) as string;
        }

        if (tablesJson != null)
        {
            try
            {
                var tables = JsonSerializer.Deserialize<List<SnapshotTableInfoDto>>(tablesJson);
                if (tables != null)
                {
                    foreach (var t in tables)
                    {
                        await using var dropCmd = new NpgsqlCommand($"DROP TABLE IF EXISTS admin_table_clear.{Quote(t.TempTableName)};", conn);
                        await dropCmd.ExecuteNonQueryAsync(ct);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not drop temporary tables for snapshot {SnapshotId}", snapshotId);
            }
        }

        await using (var delCmd = new NpgsqlCommand("DELETE FROM admin_table_clear.database_backup_snapshots WHERE id = @id", conn))
        {
            delCmd.Parameters.AddWithValue("id", snapshotId);
            var rows = await delCmd.ExecuteNonQueryAsync(ct);
            return rows > 0;
        }
    }

    public async Task<IReadOnlyList<DatabaseBackupRestoreLogDto>> GetRestoreLogsAsync(CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        await EnsureBackupRegistryTablesAsync(conn, ct);

        var list = new List<DatabaseBackupRestoreLogDto>();
        var sql = @"
            SELECT id, backup_id, backup_schema_name, target_schema, action, status,
                   total_rows_processed, total_inserted, total_updated, total_errors,
                   details_json, message, executed_at_utc, executed_by
            FROM tenant.database_backup_restore_logs
            ORDER BY executed_at_utc DESC
            LIMIT 200";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new DatabaseBackupRestoreLogDto(
                Id: reader.GetGuid(0),
                BackupId: reader.IsDBNull(1) ? null : reader.GetGuid(1),
                BackupSchemaName: reader.GetString(2),
                TargetSchema: reader.GetString(3),
                Action: reader.GetString(4),
                Status: reader.GetString(5),
                TotalRowsProcessed: reader.GetInt64(6),
                TotalInserted: reader.GetInt32(7),
                TotalUpdated: reader.GetInt32(8),
                TotalErrors: reader.GetInt32(9),
                DetailsJson: reader.IsDBNull(10) ? null : reader.GetString(10),
                Message: reader.IsDBNull(11) ? null : reader.GetString(11),
                ExecutedAtUtc: reader.GetDateTime(12),
                ExecutedBy: reader.GetString(13)
            ));
        }
        return list;
    }

    public async Task<DropBackupSchemaResultDto> DropBackupTempDataAndSchemaAsync(CancellationToken ct)
    {
        var connStr = GetConnectionString();
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync(ct);

        var droppedTables = new List<string>();

        // 1. Gather all tables currently in admin_table_clear if schema exists
        await using (var cmd = new NpgsqlCommand(
            "SELECT table_name FROM information_schema.tables WHERE table_schema = 'admin_table_clear'", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                droppedTables.Add(reader.GetString(0));
            }
        }

        // 2. Drop admin_table_clear CASCADE
        await using (var dropCmd = new NpgsqlCommand("DROP SCHEMA IF EXISTS admin_table_clear CASCADE;", conn))
        {
            await dropCmd.ExecuteNonQueryAsync(ct);
        }

        // 3. Clean up any legacy table_clear_* schemas if lingering
        var legacySchemas = new List<string>();
        await using (var legacyCmd = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'table_clear_%'", conn))
        await using (var reader = await legacyCmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                legacySchemas.Add(reader.GetString(0));
            }
        }

        foreach (var legacy in legacySchemas)
        {
            await using var dropLegacyCmd = new NpgsqlCommand($"DROP SCHEMA IF EXISTS {Quote(legacy)} CASCADE;", conn);
            await dropLegacyCmd.ExecuteNonQueryAsync(ct);
        }

        logger.LogInformation("Super Admin dropped temporary backup schema 'admin_table_clear' and {Count} temporary tables.", droppedTables.Count);

        return new DropBackupSchemaResultDto(
            Success: true,
            Message: $"Successfully dropped temporary backup schema 'admin_table_clear' and {droppedTables.Count} temporary backup tables.",
            DroppedTablesCount: droppedTables.Count,
            DroppedTables: droppedTables,
            SchemaDropped: true
        );
    }

    private static async Task EnsureBackupRegistryTablesAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"
            CREATE SCHEMA IF NOT EXISTS tenant;
            CREATE TABLE IF NOT EXISTS tenant.database_backups (
                id uuid PRIMARY KEY,
                backup_name varchar(200) NOT NULL,
                backup_schema_name varchar(200) NOT NULL,
                database_name varchar(100) NOT NULL,
                source_schema varchar(100) NOT NULL,
                total_tables int NOT NULL,
                total_rows bigint NOT NULL,
                tables_json text NOT NULL,
                status varchar(50) NOT NULL DEFAULT 'Active',
                notes text,
                created_at_utc timestamptz NOT NULL DEFAULT now(),
                created_by varchar(200) NOT NULL,
                last_restored_at_utc timestamptz,
                last_restored_by varchar(200),
                dropped_at_utc timestamptz,
                dropped_by varchar(200)
            );

            CREATE TABLE IF NOT EXISTS tenant.database_backup_restore_logs (
                id uuid PRIMARY KEY,
                backup_id uuid,
                backup_schema_name varchar(200) NOT NULL,
                target_schema varchar(100) NOT NULL,
                action varchar(50) NOT NULL,
                status varchar(50) NOT NULL DEFAULT 'Success',
                total_rows_processed bigint NOT NULL DEFAULT 0,
                total_inserted int NOT NULL DEFAULT 0,
                total_updated int NOT NULL DEFAULT 0,
                total_errors int NOT NULL DEFAULT 0,
                details_json text,
                message text,
                executed_at_utc timestamptz NOT NULL DEFAULT now(),
                executed_by varchar(200) NOT NULL
            );", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> SchemaExistsAsync(NpgsqlConnection conn, string schemaName, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM information_schema.schemata WHERE schema_name = @s)", conn);
        cmd.Parameters.AddWithValue("s", schemaName);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    #endregion

    #region Helper Methods

    private static void SetCellValueFormatted(IXLCell cell, object val, string dataType)
    {
        switch (val)
        {
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
                break;
            case DateOnly dOnly:
                cell.Value = dOnly.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case decimal dec:
                cell.Value = dec;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double dbl:
                cell.Value = dbl;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case float flt:
                cell.Value = flt;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int i:
                cell.Value = i;
                cell.Style.NumberFormat.Format = "0";
                break;
            case long l:
                cell.Value = l;
                cell.Style.NumberFormat.Format = "0";
                break;
            case short s:
                cell.Value = s;
                cell.Style.NumberFormat.Format = "0";
                break;
            case bool b:
                cell.Value = b;
                break;
            case Guid g:
                cell.Value = g.ToString();
                break;
            case byte[] bytes:
                cell.Value = Convert.ToBase64String(bytes);
                break;
            default:
                cell.Value = val.ToString() ?? string.Empty;
                break;
        }
    }

    private static object? ConvertCellValue(IXLCell cell, string dataType, bool isPrimaryKey = false)
    {
        if (cell.IsEmpty())
        {
            if (isPrimaryKey && dataType.ToLowerInvariant().Contains("uuid"))
            {
                return Guid.NewGuid();
            }
            return DBNull.Value;
        }

        var text = cell.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            if (isPrimaryKey && dataType.ToLowerInvariant().Contains("uuid"))
            {
                return Guid.NewGuid();
            }
            return DBNull.Value;
        }

        dataType = dataType.ToLowerInvariant();

        if (dataType.Contains("uuid"))
        {
            if (Guid.TryParse(text, out var g)) return g;
            return isPrimaryKey ? Guid.NewGuid() : DBNull.Value;
        }

        if (dataType.Contains("bool"))
        {
            if (bool.TryParse(text, out var b)) return b;
            return text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        if (dataType.Contains("int") || dataType.Contains("serial"))
        {
            if (dataType.Contains("bigint"))
            {
                return long.TryParse(text, out var l) ? l : DBNull.Value;
            }
            if (dataType.Contains("smallint"))
            {
                return short.TryParse(text, out var s) ? s : DBNull.Value;
            }
            return int.TryParse(text, out var i) ? i : DBNull.Value;
        }

        if (dataType.Contains("numeric") || dataType.Contains("decimal") || dataType.Contains("money"))
        {
            return decimal.TryParse(text, out var dec) ? dec : DBNull.Value;
        }

        if (dataType.Contains("double") || dataType.Contains("real") || dataType.Contains("float"))
        {
            return double.TryParse(text, out var dbl) ? dbl : DBNull.Value;
        }

        if (dataType.Contains("timestamp") || dataType.Contains("date"))
        {
            if (cell.DataType == XLDataType.DateTime)
            {
                return cell.GetDateTime();
            }
            if (DateTime.TryParse(text, out var dt))
            {
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            }
            return DBNull.Value;
        }

        if (dataType.Contains("json"))
        {
            try
            {
                using var doc = JsonDocument.Parse(text);
                return text;
            }
            catch
            {
                return JsonSerializer.Serialize(text);
            }
        }

        return text;
    }

    private static List<string> FindDifferences(Dictionary<string, object?> existing, Dictionary<string, object?> incoming)
    {
        var diffs = new List<string>();
        foreach (var (key, inVal) in incoming)
        {
            if (!existing.TryGetValue(key, out var exVal)) continue;

            var inStr = NormalizeValue(inVal);
            var exStr = NormalizeValue(exVal);

            if (!string.Equals(inStr, exStr, StringComparison.Ordinal))
            {
                diffs.Add($"'{key}' ({exStr} -> {inStr})");
            }
        }
        return diffs;
    }

    private static string NormalizeValue(object? val)
    {
        if (val == null || val == DBNull.Value) return "<NULL>";
        if (val is DateTime dt) return dt.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is DateTimeOffset dto) return dto.ToString("yyyy-MM-dd HH:mm:ss");
        if (val is decimal dec) return dec.ToString("G29");
        return val.ToString()?.Trim() ?? "<NULL>";
    }

    private static async Task<Dictionary<string, object?>?> GetExistingRowAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string schema, string table, string pkCol, object pkVal, CancellationToken ct)
    {
        string sql = $"SELECT * FROM \"{schema}\".\"{table}\" WHERE \"{pkCol}\" = @pk LIMIT 1";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        cmd.Parameters.AddWithValue("pk", pkVal);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;

        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            result[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
        }
        return result;
    }

    private static async Task ExecuteInsertAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string schema,
        string table,
        Dictionary<string, object?> row,
        Dictionary<string, TableColumnMeta> colMap,
        CancellationToken ct)
    {
        var cols = row.Keys.ToList();
        var colList = string.Join(", ", cols.Select(c => $"\"{c}\""));
        var paramExpressions = cols.Select((c, i) =>
        {
            if (colMap.TryGetValue(c, out var meta) && meta.DataType.Contains("json"))
            {
                return meta.DataType.Contains("jsonb") ? $"CAST(@p{i} AS jsonb)" : $"CAST(@p{i} AS json)";
            }
            return $"@p{i}";
        });
        var paramList = string.Join(", ", paramExpressions);

        string sql = $"INSERT INTO \"{schema}\".\"{table}\" ({colList}) VALUES ({paramList})";
        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        for (var i = 0; i < cols.Count; i++)
        {
            var pVal = row[cols[i]] ?? DBNull.Value;
            cmd.Parameters.AddWithValue($"p{i}", pVal);
        }
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ExecuteUpdateAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string schema,
        string table,
        string pkCol,
        object pkVal,
        Dictionary<string, object?> row,
        Dictionary<string, TableColumnMeta> colMap,
        CancellationToken ct)
    {
        var cols = row.Keys.Where(k => !k.Equals(pkCol, StringComparison.OrdinalIgnoreCase)).ToList();
        if (cols.Count == 0) return;

        var setClauses = string.Join(", ", cols.Select((c, i) =>
        {
            if (colMap.TryGetValue(c, out var meta) && meta.DataType.Contains("json"))
            {
                return meta.DataType.Contains("jsonb") ? $"\"{c}\" = CAST(@p{i} AS jsonb)" : $"\"{c}\" = CAST(@p{i} AS json)";
            }
            return $"\"{c}\" = @p{i}";
        }));

        string sql = $"UPDATE \"{schema}\".\"{table}\" SET {setClauses} WHERE \"{pkCol}\" = @pk";

        await using var cmd = new NpgsqlCommand(sql, conn, tx);
        for (var i = 0; i < cols.Count; i++)
        {
            var pVal = row[cols[i]] ?? DBNull.Value;
            cmd.Parameters.AddWithValue($"p{i}", pVal);
        }
        cmd.Parameters.AddWithValue("pk", pkVal);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task ResyncTableSequencesAsync(
        NpgsqlConnection conn,
        NpgsqlTransaction tx,
        string schema,
        string table,
        string pkCol,
        CancellationToken ct)
    {
        try
        {
            var sql = $@"
                DO $$
                DECLARE
                    seq_name text;
                    max_val bigint;
                BEGIN
                    seq_name := pg_get_serial_sequence('""{schema}"".""{table}""', '{pkCol}');
                    IF seq_name IS NOT NULL THEN
                        EXECUTE format('SELECT COALESCE(MAX(""{pkCol}""), 0) FROM ""{schema}"".""{table}""') INTO max_val;
                        IF max_val > 0 THEN
                            PERFORM setval(seq_name, max_val, true);
                        END IF;
                    END IF;
                EXCEPTION WHEN OTHERS THEN
                    -- Ignore sequence resync error if not a serial/identity column
                    NULL;
                END $$;";

            await using var cmd = new NpgsqlCommand(sql, conn, tx);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch
        {
            // Ignore non-sequence primary keys (e.g., UUIDs)
        }
    }

    private static async Task<List<TableColumnMeta>> GetTableColumnsAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        var list = new List<TableColumnMeta>();
        await using var cmd = new NpgsqlCommand(@"
            SELECT column_name, data_type, is_nullable
            FROM information_schema.columns
            WHERE LOWER(table_schema) = LOWER(@schema) AND table_name = @table
            ORDER BY ordinal_position", conn);
        cmd.Parameters.AddWithValue("schema", schema.Trim());
        cmd.Parameters.AddWithValue("table", table);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TableColumnMeta(reader.GetString(0), reader.GetString(1), reader.GetString(2) == "YES"));
        }
        return list;
    }

    private static async Task<string?> GetPrimaryKeyColumnAsync(NpgsqlConnection conn, string schema, string table, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(@"
            SELECT kcu.column_name
            FROM information_schema.table_constraints tc
            JOIN information_schema.key_column_usage kcu 
              ON tc.constraint_name = kcu.constraint_name 
             AND tc.table_schema = kcu.table_schema
            WHERE tc.constraint_type = 'PRIMARY KEY' 
              AND LOWER(tc.table_schema) = LOWER(@schema) 
              AND tc.table_name = @table
            ORDER BY kcu.ordinal_position
            LIMIT 1", conn);
        cmd.Parameters.AddWithValue("schema", schema.Trim());
        cmd.Parameters.AddWithValue("table", table);
        var scalar = await cmd.ExecuteScalarAsync(ct);
        return scalar?.ToString();
    }

    private static async Task<List<string>> GetTopologicalTableOrderAsync(
        NpgsqlConnection conn, string schema, List<string> tables, CancellationToken ct)
    {
        var tableSet = new HashSet<string>(tables, StringComparer.OrdinalIgnoreCase);
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tableSet)
        {
            dependencies[t] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        // Fetch foreign key constraints
        await using (var cmd = new NpgsqlCommand(@"
            SELECT
                tc.table_name AS dependent_table,
                ccu.table_name AS referenced_table
            FROM information_schema.table_constraints AS tc
            JOIN information_schema.key_column_usage AS kcu
              ON tc.constraint_name = kcu.constraint_name
             AND tc.table_schema = kcu.table_schema
            JOIN information_schema.constraint_column_usage AS ccu
              ON ccu.constraint_name = tc.constraint_name
             AND ccu.table_schema = tc.table_schema
            WHERE tc.constraint_type = 'FOREIGN KEY'
              AND LOWER(tc.table_schema) = LOWER(@schema)", conn))
        {
            cmd.Parameters.AddWithValue("schema", schema.Trim());
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dep = reader.GetString(0);
                var refTable = reader.GetString(1);

                if (tableSet.Contains(dep) && tableSet.Contains(refTable) && !dep.Equals(refTable, StringComparison.OrdinalIgnoreCase))
                {
                    dependencies[dep].Add(refTable);
                }
            }
        }

        // Topological Sort (Kahn's Algorithm)
        var sorted = new List<string>();
        var remaining = new HashSet<string>(tableSet, StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            var next = remaining.FirstOrDefault(t => dependencies[t].Count == 0);
            if (next == null)
            {
                // Circular dependency detected: break cycle by picking any table
                next = remaining.First();
            }

            sorted.Add(next.ToLowerInvariant());
            remaining.Remove(next);

            foreach (var r in remaining)
            {
                dependencies[r].Remove(next);
            }
        }

        return sorted;
    }

    private static string SanitizeSheetName(string name)
    {
        var sanitized = name
            .Replace(":", "_")
            .Replace("\\", "_")
            .Replace("/", "_")
            .Replace("?", "_")
            .Replace("*", "_")
            .Replace("[", "_")
            .Replace("]", "_");
        return sanitized.Length > 31 ? sanitized[..31] : sanitized;
    }

    private static bool IsValidSqlIdentifier(string? id)
    {
        return !string.IsNullOrWhiteSpace(id)
            && id.Length <= 128
            && id.All(c => char.IsLetterOrDigit(c) || c == '_');
    }

    private static void ValidateIdentifier(string id)
    {
        if (!IsValidSqlIdentifier(id))
        {
            throw new ArgumentException($"Invalid SQL identifier: '{id}'.");
        }
    }

    private static string FormatRowError(int rowNumber, Exception ex)
    {
        var root = ex;
        while (root.InnerException != null)
        {
            root = root.InnerException;
        }

        if (root is Npgsql.PostgresException pex)
        {
            var detail = pex.Detail;
            var col = pex.ColumnName;
            var text = pex.MessageText;

            return pex.SqlState switch
            {
                "23505" => $"Row {rowNumber}: Duplicate value. {(string.IsNullOrWhiteSpace(detail) ? text : detail)}. Check for duplicates.",
                "23503" => $"Row {rowNumber}: Missing related record. {(string.IsNullOrWhiteSpace(detail) ? text : detail)}. Please restore master/parent tables first.",
                "23502" => $"Row {rowNumber}: Required field '{(string.IsNullOrWhiteSpace(col) ? "unknown" : col)}' is blank.",
                "42703" => $"Row {rowNumber}: Column not found '{text}'. Check if table schema matches current version.",
                "22P02" => $"Row {rowNumber}: Invalid data format for column. {text}",
                _ => $"Row {rowNumber}: Database error: {text}"
            };
        }

        var msg = root.Message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        return $"Row {rowNumber}: {msg}";
    }

    private static string FormatDisplayName(string rawName)
    {
        var parts = rawName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    private sealed record TableColumnMeta(string Name, string DataType, bool IsNullable);

    #endregion
}

