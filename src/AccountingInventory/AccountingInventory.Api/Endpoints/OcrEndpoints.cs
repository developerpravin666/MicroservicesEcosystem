using System.Security.Claims;
using AccountingInventory.Application.OCR;
using AccountingInventory.Application.OCR.Models;
using BuildingBlocks.Domain.MultiTenancy;
using BuildingBlocks.Persistence;
using BuildingBlocks.Security;
using BuildingBlocks.WebDefaults;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class OcrEndpoints
{
    public static IEndpointRouteBuilder MapOcrEndpoints(this IEndpointRouteBuilder app)
    {
        // -------------------------------------------------------------
        // SUPER ADMIN OCR MANAGEMENT ENDPOINTS
        // -------------------------------------------------------------
        var adminGroup = app.MapGroup("/api/admin/ocr")
            .WithTags("OCR & Document AI Admin")
            .RequireAuthorization(policy => policy.RequireAuthenticatedUser()
                .RequireRole("ProductOwner", "SuperAdmin")
                .RequireClaim(TenantClaimTypes.TenantId, Guid.Empty.ToString()));

        // Providers CRUD
        adminGroup.MapGet("/providers", async (IOcrService ocrService, CancellationToken ct) =>
        {
            var providers = await ocrService.GetAllProvidersAsync(ct);
            return Results.Ok(providers);
        })
        .WithName("AdminGetOcrProviders")
        .Produces<List<OcrProviderDto>>();

        adminGroup.MapGet("/providers/{id:guid}", async (Guid id, IOcrService ocrService, CancellationToken ct) =>
        {
            var provider = await ocrService.GetProviderByIdAsync(id, ct);
            return provider != null ? Results.Ok(provider) : Results.NotFound();
        })
        .WithName("AdminGetOcrProviderById")
        .Produces<OcrProviderDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/providers", async ([FromBody] CreateOcrProviderRequest request, IOcrService ocrService, CancellationToken ct) =>
        {
            var created = await ocrService.CreateProviderAsync(request, ct);
            return Results.Created($"/api/admin/ocr/providers/{created.Id}", created);
        })
        .WithName("AdminCreateOcrProvider")
        .Produces<OcrProviderDto>(StatusCodes.Status201Created);

        adminGroup.MapPut("/providers/{id:guid}", async (Guid id, [FromBody] UpdateOcrProviderRequest request, IOcrService ocrService, CancellationToken ct) =>
        {
            var updated = await ocrService.UpdateProviderAsync(id, request, ct);
            return Results.Ok(updated);
        })
        .WithName("AdminUpdateOcrProvider")
        .Produces<OcrProviderDto>();

        adminGroup.MapDelete("/providers/{id:guid}", async (Guid id, IOcrService ocrService, CancellationToken ct) =>
        {
            var deleted = await ocrService.DeleteProviderAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("AdminDeleteOcrProvider")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        adminGroup.MapPost("/providers/{id:guid}/test", async (Guid id, IOcrService ocrService, CancellationToken ct) =>
        {
            var testResult = await ocrService.TestProviderConnectionAsync(id, ct);
            return Results.Ok(testResult);
        })
        .WithName("AdminTestOcrProviderConnection")
        .Produces<OcrConnectionTestResult>();

        // Tenant OCR Settings & Limits
        adminGroup.MapGet("/tenants", async (IOcrService ocrService, CancellationToken ct) =>
        {
            var settings = await ocrService.GetAllTenantOcrSettingsAsync(ct);
            return Results.Ok(settings);
        })
        .WithName("AdminGetAllTenantOcrSettings")
        .Produces<List<TenantOcrSettingDto>>();

        adminGroup.MapGet("/tenants/{tenantId:guid}", async (Guid tenantId, IOcrService ocrService, CancellationToken ct) =>
        {
            var setting = await ocrService.GetTenantOcrSettingAsync(tenantId, ct);
            return Results.Ok(setting);
        })
        .WithName("AdminGetTenantOcrSetting")
        .Produces<TenantOcrSettingDto>();

        adminGroup.MapPut("/tenants/{tenantId:guid}", async (Guid tenantId, [FromBody] UpdateTenantOcrSettingRequest request, IOcrService ocrService, CancellationToken ct) =>
        {
            var updated = await ocrService.UpdateTenantOcrSettingAsync(tenantId, request, ct);
            return Results.Ok(updated);
        })
        .WithName("AdminUpdateTenantOcrSetting")
        .Produces<TenantOcrSettingDto>();

        // Audit Logs
        adminGroup.MapGet("/logs", async ([FromQuery] Guid? tenantId, [FromQuery] int? limit, IOcrService ocrService, CancellationToken ct) =>
        {
            var logs = await ocrService.GetTenantScanLogsAsync(tenantId, limit ?? 50, ct);
            return Results.Ok(logs);
        })
        .WithName("AdminGetOcrScanLogs")
        .Produces<List<Domain.Entities.TenantOcrScanLog>>();

        // -------------------------------------------------------------
        // TENANT-FACING OCR ENDPOINTS
        // -------------------------------------------------------------
        var tenantGroup = app.MapGroup("/api/ocr")
            .WithTags("Tenant Invoice OCR")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        tenantGroup.MapGet("/status", async (ITenantContext tenantContext, IOcrService ocrService, CancellationToken ct) =>
        {
            if (tenantContext.TenantId == null) return Results.Unauthorized();
            var status = await ocrService.GetTenantOcrStatusAsync(tenantContext.TenantId.Value, ct);
            return Results.Ok(status);
        })
        .WithName("GetTenantOcrStatus")
        .Produces<TenantOcrStatusDto>();

        tenantGroup.MapGet("/providers", async (ITenantContext tenantContext, IOcrService ocrService, CancellationToken ct) =>
        {
            if (tenantContext.TenantId == null) return Results.Unauthorized();
            var status = await ocrService.GetTenantOcrStatusAsync(tenantContext.TenantId.Value, ct);
            return Results.Ok(status.AvailableProviders);
        })
        .WithName("GetTenantOcrProviders")
        .Produces<List<TenantAvailableProviderDto>>();

        tenantGroup.MapPost("/process-invoice", async (
            IFormFile file,
            [FromForm] string? rawText,
            [FromForm] Guid? providerId,
            ITenantContext tenantContext,
            IOcrService ocrService,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            if (tenantContext.TenantId == null) return Results.Unauthorized();
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "An invoice image or PDF file is required for OCR scanning." });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
            {
                return Results.BadRequest(new { error = "Supported file formats: JPG, JPEG, PNG, WEBP, PDF." });
            }

            try
            {
                var userName = user.FindFirstValue(ClaimTypes.Name)
                    ?? user.FindFirstValue(ClaimTypes.Email)
                    ?? "Store User";

                await using var stream = file.OpenReadStream();
                var result = await ocrService.ProcessInvoiceAsync(
                    tenantId: tenantContext.TenantId.Value,
                    documentStream: stream,
                    fileName: file.FileName,
                    mimeType: file.ContentType,
                    clientRawText: rawText,
                    scannedBy: userName,
                    preferredProviderId: providerId,
                    cancellationToken: ct
                );

                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: ex.Message);
            }
            catch (Exception ex)
            {
                return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: $"OCR Extraction failed: {ex.Message}");
            }
        })
        .WithName("ProcessTenantPurchaseInvoice")
        .Produces<StructuredInvoiceOcrResult>()
        .DisableAntiforgery();

        return app;
    }
}

