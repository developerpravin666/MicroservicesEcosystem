using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Commands.RestoreDatabaseBackup;
using AccountingInventory.Application.DatabaseBackup.Models;
using AccountingInventory.Application.DatabaseBackup.Queries.ExportDatabaseBackup;
using AccountingInventory.Application.DatabaseBackup.Queries.GetBackupTables;
using AccountingInventory.Application.DatabaseBackup.Queries.InspectBackupFile;
using AccountingInventory.Infrastructure.Persistence;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace AccountingInventory.Api.Endpoints;

public static class DatabaseBackupEndpoints
{
    public static RouteGroupBuilder MapDatabaseBackupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/backup")
            .WithTags("Database Backup & Restore")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser()
                .RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString()));

        // 1. GET /api/admin/backup/tables - Get all tables eligible for backup/restore
        group.MapGet("/tables", async (
            [FromQuery] string? schema,
            ISender sender,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            var query = new GetBackupTablesQuery(targetSchema);
            var tables = await sender.Send(query, ct);
            return Results.Ok(new
            {
                schema = targetSchema,
                totalTables = tables.Count,
                tables
            });
        })
        .WithName("GetBackupEligibleTables")
        .Produces<object>();

        // 2. POST /api/admin/backup/export - Export selected (or all) tables to formatted Excel
        group.MapPost("/export", async (
            [FromQuery] string? schema,
            [FromBody] ExportTablesRequest? request,
            ISender sender,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema ?? request?.Schema, tenantContext, tenantDb);
            var requestedBy = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "SuperAdmin";

            var query = new ExportDatabaseBackupQuery(targetSchema, request?.Tables, requestedBy);
            var exportResult = await sender.Send(query, ct);

            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                exportResult.Content,
                exportResult.ContentType,
                exportResult.FileName);
        })
        .WithName("ExportDatabaseBackupToExcel")
        .Produces(StatusCodes.Status200OK);

        // 2b. GET /api/admin/backup/export - Direct download via GET
        group.MapGet("/export", async (
            [FromQuery] string? schema,
            [FromQuery] string? tables,
            ISender sender,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            ClaimsPrincipal user,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema, tenantContext, tenantDb);
            var requestedBy = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "SuperAdmin";
            var tableList = string.IsNullOrWhiteSpace(tables)
                ? null
                : tables.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray();

            var query = new ExportDatabaseBackupQuery(targetSchema, tableList, requestedBy);
            var exportResult = await sender.Send(query, ct);

            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                exportResult.Content,
                exportResult.ContentType,
                exportResult.FileName);
        })
        .WithName("ExportDatabaseBackupToExcelGet")
        .Produces(StatusCodes.Status200OK);

        // 3. POST /api/admin/backup/inspect - Inspect uploaded Excel file before restoring
        group.MapPost("/inspect", async (
            IFormFile file,
            ISender sender,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Please upload a valid Excel (.xlsx) file." });
            }

            if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new { message = "Only Excel workbook files (.xlsx) are supported." });
            }

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            stream.Seek(0, SeekOrigin.Begin);

            var query = new InspectBackupFileQuery(stream, file.FileName);
            var result = await sender.Send(query, ct);

            return Results.Ok(result);
        })
        .WithName("InspectBackupFile")
        .DisableAntiforgery()
        .Produces<InspectBackupFileResultDto>();

        // 4. POST /api/admin/backup/restore - Restore data from Excel with audit diffs
        group.MapPost("/restore", async (
            HttpRequest httpRequest,
            ISender sender,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            ClaimsPrincipal user,
            ICurrentUserProvider currentUser,
            CancellationToken ct) =>
        {
            if (!httpRequest.HasFormContentType)
            {
                return Results.BadRequest(new { message = "Multipart form data with file is required." });
            }

            var form = await httpRequest.ReadFormAsync(ct);
            var file = form.Files["file"] ?? form.Files.FirstOrDefault();
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { message = "Please attach an Excel backup file to restore." });
            }

            var requestedSchema = form["schema"].ToString();
            var targetSchema = ResolveTargetSchema(requestedSchema, tenantContext, tenantDb);

            // Parse selected tables if specified
            List<string>? selectedTables = null;
            var tablesField = form["tables"].ToString();
            if (!string.IsNullOrWhiteSpace(tablesField))
            {
                try
                {
                    selectedTables = JsonSerializer.Deserialize<List<string>>(tablesField);
                }
                catch
                {
                    selectedTables = tablesField.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim())
                        .ToList();
                }
            }

            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? "SuperAdmin";
            var userId = currentUser.UserId;

            await using var stream = new MemoryStream();
            await file.CopyToAsync(stream, ct);
            stream.Seek(0, SeekOrigin.Begin);

            var command = new RestoreDatabaseBackupCommand(
                Schema: targetSchema,
                ExcelStream: stream,
                FileName: file.FileName,
                SelectedTables: selectedTables,
                UserId: userId,
                UserEmail: userEmail
            );

            var result = await sender.Send(command, ct);

            return Results.Ok(result);
        })
        .WithName("RestoreDatabaseBackupFromExcel")
        .DisableAntiforgery()
        .Produces<DatabaseRestoreResultDto>();

        // 5. POST /api/admin/backup/create-snapshot - Create in-database backup stored in temp tables in same DB
        group.MapPost("/create-snapshot", async (
            [FromBody] CreateSnapshotRequest? request,
            [FromQuery] string? schema,
            IDatabaseBackupService backupService,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema ?? request?.Schema, tenantContext, tenantDb);
            var requestedBy = user.Identity?.Name ?? user.FindFirst(ClaimTypes.Email)?.Value ?? "SuperAdmin";

            var result = await backupService.CreateDatabaseSnapshotAsync(
                schema: targetSchema,
                selectedTables: request?.Tables,
                backupName: request?.BackupName,
                notes: request?.Notes,
                createdBy: requestedBy,
                ct: ct
            );

            return Results.Ok(result);
        })
        .WithName("CreateDatabaseSnapshotInSameDb")
        .Produces<DatabaseSnapshotDto>();

        // 6. GET /api/admin/backup/snapshots - List in-database backup snapshots
        group.MapGet("/snapshots", async (
            [FromQuery] string? schema,
            IDatabaseBackupService backupService,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = (string.IsNullOrWhiteSpace(schema) || schema.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                ? null
                : ResolveTargetSchema(schema, tenantContext, tenantDb);
            var snapshots = await backupService.GetDatabaseSnapshotsAsync(targetSchema, ct);
            return Results.Ok(snapshots);
        })
        .WithName("GetDatabaseSnapshots")
        .Produces<IReadOnlyList<DatabaseSnapshotDto>>();

        // 7. POST /api/admin/backup/restore-snapshot/{id} - Restore directly from database temp tables
        group.MapPost("/restore-snapshot/{id:guid}", async (
            Guid id,
            [FromBody] RestoreSnapshotRequest? request,
            [FromQuery] string? schema,
            IDatabaseBackupService backupService,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            ClaimsPrincipal user,
            ICurrentUserProvider currentUser,
            CancellationToken ct) =>
        {
            var targetSchema = ResolveTargetSchema(schema ?? request?.Schema, tenantContext, tenantDb);
            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? "SuperAdmin";
            var userId = currentUser.UserId;

            var result = await backupService.RestoreFromDatabaseSnapshotAsync(
                snapshotId: id,
                targetSchemaOverride: targetSchema,
                selectedTables: request?.Tables,
                userId: userId,
                userEmail: userEmail,
                ct: ct
            );

            return Results.Ok(result);
        })
        .WithName("RestoreFromDatabaseSnapshot")
        .Produces<DatabaseRestoreResultDto>();

        // 7b. GET /api/admin/backup/backups - Alias for snapshots
        group.MapGet("/backups", async (
            [FromQuery] string? schema,
            IDatabaseBackupService backupService,
            TenantDbContext tenantDb,
            ITenantContext? tenantContext,
            CancellationToken ct) =>
        {
            var targetSchema = (string.IsNullOrWhiteSpace(schema) || schema.Equals("ALL", StringComparison.OrdinalIgnoreCase))
                ? null
                : ResolveTargetSchema(schema, tenantContext, tenantDb);
            var snapshots = await backupService.GetDatabaseSnapshotsAsync(targetSchema, ct);
            return Results.Ok(snapshots);
        })
        .WithName("GetDatabaseBackups")
        .Produces<IReadOnlyList<DatabaseSnapshotDto>>();

        // 7c. GET /api/admin/backup/logs - Get backup & restore audit logs from tenant.database_backup_restore_logs
        group.MapGet("/logs", async (
            IDatabaseBackupService backupService,
            CancellationToken ct) =>
        {
            var logs = await backupService.GetRestoreLogsAsync(ct);
            return Results.Ok(logs);
        })
        .WithName("GetDatabaseBackupRestoreLogs")
        .Produces<IReadOnlyList<DatabaseBackupRestoreLogDto>>();

        // 8. DELETE /api/admin/backup/snapshots/{id} & DELETE /api/admin/backup/backups/{id} - Delete snapshot and drop temporary backup schema
        group.MapDelete("/snapshots/{id:guid}", async (
            Guid id,
            IDatabaseBackupService backupService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? "SuperAdmin";
            var success = await backupService.DeleteDatabaseBackupAndDropSchemaAsync(id, userEmail, ct);
            return success ? Results.Ok(new { message = "Snapshot and temporary schema dropped successfully." }) : Results.NotFound();
        })
        .WithName("DeleteDatabaseSnapshot")
        .Produces(StatusCodes.Status200OK);

        group.MapDelete("/backups/{id:guid}", async (
            Guid id,
            IDatabaseBackupService backupService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userEmail = user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? "SuperAdmin";
            var success = await backupService.DeleteDatabaseBackupAndDropSchemaAsync(id, userEmail, ct);
            return success ? Results.Ok(new { message = "Backup schema dropped and record updated." }) : Results.NotFound();
        })
        .WithName("DeleteDatabaseBackupAndDropSchema")
        .Produces(StatusCodes.Status200OK);

        // 9. POST /api/admin/backup/drop-backup-schema - Manually drop all temporary backup tables and admin_table_clear schema
        group.MapPost("/drop-backup-schema", async (
            IDatabaseBackupService backupService,
            CancellationToken ct) =>
        {
            var result = await backupService.DropBackupTempDataAndSchemaAsync(ct);
            return Results.Ok(result);
        })
        .WithName("DropBackupTempDataAndSchema")
        .Produces<DropBackupSchemaResultDto>();

        return group;
    }

    private static string ResolveTargetSchema(string? requestedSchema, ITenantContext? context, TenantDbContext tenantDb)
    {
        var cleaned = requestedSchema?.Trim();
        if (!string.IsNullOrWhiteSpace(cleaned) && !string.Equals(cleaned, "tenant", StringComparison.OrdinalIgnoreCase))
        {
            return cleaned;
        }

        if (context?.SchemaName is { Length: > 0 } userSchema && userSchema != "tenant")
        {
            return userSchema;
        }

        var firstTenant = tenantDb.Tenants.AsNoTracking()
            .Where(t => t.IsActive && !t.IsDeleted && t.SchemaName != "tenant")
            .Select(t => t.SchemaName)
            .FirstOrDefault();

        return firstTenant ?? (string.IsNullOrWhiteSpace(cleaned) ? "tenant" : cleaned);
    }
}

public sealed record ExportTablesRequest(string[]? Tables = null, string? Schema = null);
public sealed record CreateSnapshotRequest(string? Schema = null, string[]? Tables = null, string? BackupName = null, string? Notes = null);
public sealed record RestoreSnapshotRequest(string? Schema = null, string[]? Tables = null);

