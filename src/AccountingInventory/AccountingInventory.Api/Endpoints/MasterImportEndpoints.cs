using System.Text;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.MasterImport;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Persistence;
using BuildingBlocks.WebDefaults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Api.Endpoints;

public static class MasterImportEndpoints
{
    public static RouteGroupBuilder MapMasterImportEndpoints(this IEndpointRouteBuilder app)
    {
        // 1. Tenant-scoped endpoints under /api/masters
        var group = app.MapGroup("/api/masters")
            .WithTags("Master Import & Export")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        // Download Template (Multi-sheet Excel .xlsx only)
        group.MapGet("/template", (IMasterDataImportService importService, HttpContext httpContext) =>
        {
            var bytes = importService.GenerateMultiSheetExcelTemplateXlsx();
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "siddhi_master_import_template.xlsx");
        })
        .WithName("DownloadMasterImportTemplate")
        .Produces(StatusCodes.Status200OK);

        // Export Live Master Catalog (Multi-sheet Excel .xlsx only)
        group.MapGet("/export", async (IMasterDataImportService importService, HttpContext httpContext, CancellationToken ct) =>
        {
            var bytes = await importService.ExportExistingMastersXlsxAsync(ct);
            var excelFileName = $"siddhi_masters_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                excelFileName);
        })
        .WithName("ExportMasterCatalog")
        .Produces(StatusCodes.Status200OK);

        // Upload and Stage Master Data (Multi-sheet Excel .xlsx only)
        group.MapPost("/import/upload", async (
            HttpRequest request,
            IMasterDataImportService importService,
            ICurrentUserProvider currentUser,
            CancellationToken ct) =>
        {
            string fileName = "masters_import.xlsx";
            string content = "";

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(ct);
                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (file == null || file.Length == 0)
                {
                    return Results.BadRequest(new { message = "No file was uploaded." });
                }

                fileName = file.FileName;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                content = Convert.ToBase64String(ms.ToArray());
            }
            else
            {
                var body = await request.ReadFromJsonAsync<MasterImportUploadRequest>(cancellationToken: ct);
                if (body == null || (string.IsNullOrWhiteSpace(body.Base64Content) && string.IsNullOrWhiteSpace(body.CsvContent)))
                {
                    return Results.BadRequest(new { message = "Request body must contain base64Content for the Excel (.xlsx) file." });
                }

                fileName = body.FileName ?? "masters_import.xlsx";
                content = !string.IsNullOrWhiteSpace(body.Base64Content) ? body.Base64Content : body.CsvContent!;
            }

            var summary = await importService.StageImportAsync(fileName, content, currentUser.UserId, ct);
            return Results.Created($"/api/masters/import/batches/{summary.Id}", summary);
        })
        .WithName("StageMasterImport")
        .Produces<MasterImportBatchSummaryDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // List Staging Batches
        group.MapGet("/import/batches", async (IMasterDataImportService importService, CancellationToken ct) =>
            Results.Ok(await importService.GetBatchesAsync(ct)))
            .WithName("GetMasterImportBatches")
            .Produces<IReadOnlyList<MasterImportBatchSummaryDto>>();

        // Get Specific Batch Details & Staged Rows
        group.MapGet("/import/batches/{id:guid}", async (Guid id, IMasterDataImportService importService, CancellationToken ct) =>
        {
            var batch = await importService.GetBatchDetailsAsync(id, ct);
            return batch is not null ? Results.Ok(batch) : Results.NotFound(new { message = $"Batch with ID '{id}' was not found." });
        })
        .WithName("GetMasterImportBatchById")
        .Produces<MasterImportBatchSummaryDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Delete Batch
        group.MapDelete("/import/batches/{id:guid}", async (
            Guid id,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var success = await importService.DeleteBatchAsync(id, ct);
            return success ? Results.NoContent() : Results.NotFound(new { message = $"Batch with ID '{id}' was not found." });
        })
        .WithName("DeleteMasterImportBatch")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Toggle Single Staging Row Approval
        group.MapPut("/import/batches/{id:guid}/rows/{rowId:guid}/toggle", async (
            Guid id,
            Guid rowId,
            ToggleRowApprovalRequest request,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var success = await importService.ToggleRowApprovalAsync(id, rowId, request.IsApproved, ct);
            return success ? Results.Ok(new { success = true }) : Results.NotFound(new { message = "Row not found." });
        })
        .WithName("ToggleMasterStagingRowApproval");

        // Approve and Commit Batch into Main Database Tables
        group.MapPost("/import/batches/{id:guid}/approve", async (
            Guid id,
            [FromBody] MasterImportApprovalRequest? request,
            IMasterDataImportService importService,
            ICurrentUserProvider currentUser,
            CancellationToken ct) =>
        {
            var reviewer = currentUser.UserId?.ToString() ?? "Tenant Administrator";
            var result = await importService.ApproveAndImportBatchAsync(id, reviewer, request?.Notes, ct);
            return Results.Ok(result);
        })
        .WithName("ApproveMasterImportBatch")
        .Produces<ApproveImportResult>()
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // Reject Batch
        group.MapPost("/import/batches/{id:guid}/reject", async (
            Guid id,
            [FromBody] RejectBatchRequest? request,
            IMasterDataImportService importService,
            ICurrentUserProvider currentUser,
            CancellationToken ct) =>
        {
            var reviewer = currentUser.UserId?.ToString() ?? "Tenant Administrator";
            var summary = await importService.RejectBatchAsync(id, request?.Reason, reviewer, ct);
            return Results.Ok(summary);
        })
        .WithName("RejectMasterImportBatch")
        .Produces<MasterImportBatchSummaryDto>();

        // 2. Product Owner Admin endpoints under /api/admin/tenants/{tenantId:guid}/masters/import
        var adminGroup = app.MapGroup("/api/admin/tenants/{tenantId:guid}/masters/import")
            .WithTags("Admin Master Imports")
            .RequireAuthorization("AuthenticatedUser");

        adminGroup.MapGet("/template", (Guid tenantId, IMasterDataImportService importService, HttpContext httpContext) =>
        {
            var bytes = importService.GenerateMultiSheetExcelTemplateXlsx();
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "siddhi_master_import_template.xlsx");
        })
        .WithName("AdminDownloadMasterImportTemplate")
        .Produces(StatusCodes.Status200OK);

        adminGroup.MapGet("/export", async (
            Guid tenantId,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var bytes = await importService.ExportExistingMastersXlsxAsync(ct);
            var excelFileName = $"siddhi_masters_export_{tenant.SchemaName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            httpContext.Response.Headers.Append("Access-Control-Expose-Headers", "Content-Disposition");
            return Results.File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                excelFileName);
        })
        .WithName("AdminExportTenantMasterCatalog")
        .Produces(StatusCodes.Status200OK);

        adminGroup.MapGet("/batches", async (
            Guid tenantId,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var batches = await importService.GetBatchesAsync(ct);
            return Results.Ok(batches);
        })
        .WithName("AdminGetTenantMasterBatches");

        adminGroup.MapGet("/batches/{batchId:guid}", async (
            Guid tenantId,
            Guid batchId,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var batch = await importService.GetBatchDetailsAsync(batchId, ct);
            return batch is not null ? Results.Ok(batch) : Results.NotFound(new { message = "Batch not found." });
        })
        .WithName("AdminGetTenantMasterBatchDetails");

        adminGroup.MapPost("/batches/{batchId:guid}/approve", async (
            Guid tenantId,
            Guid batchId,
            [FromBody] MasterImportApprovalRequest? request,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var result = await importService.ApproveAndImportBatchAsync(batchId, "Product Owner (Super Admin)", request?.Notes, ct);
            return Results.Ok(result);
        })
        .WithName("AdminApproveTenantMasterBatch");

        adminGroup.MapPost("/batches/{batchId:guid}/reject", async (
            Guid tenantId,
            Guid batchId,
            [FromBody] RejectBatchRequest? request,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var result = await importService.RejectBatchAsync(batchId, request?.Reason, "Product Owner (Super Admin)", ct);
            return Results.Ok(result);
        })
        .WithName("AdminRejectTenantMasterBatch");

        adminGroup.MapDelete("/batches/{batchId:guid}", async (
            Guid tenantId,
            Guid batchId,
            ITenantDirectoryContext directory,
            ITenantContextAccessor tenantContextAccessor,
            IAccountingInventoryDbContext db,
            IMasterDataImportService importService,
            CancellationToken ct) =>
        {
            var tenant = await directory.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
            if (tenant == null) return Results.NotFound(new { message = $"Tenant '{tenantId}' not found." });

            tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
            await db.ResetConnectionAsync(ct);

            var deleted = await importService.DeleteBatchAsync(batchId, ct);
            return deleted ? Results.NoContent() : Results.NotFound(new { message = "Batch not found." });
        })
        .WithName("AdminDeleteTenantMasterBatch")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }
}

public sealed record MasterImportApprovalRequest(string? Notes = null);

