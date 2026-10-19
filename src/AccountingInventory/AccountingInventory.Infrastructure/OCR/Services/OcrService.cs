using System.Diagnostics;
using System.Text.Json;
using AccountingInventory.Application.OCR;
using AccountingInventory.Application.OCR.Models;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Infrastructure.OCR.Services;

public sealed class OcrService(
    TenantDbContext dbContext,
    IEnumerable<IOcrProviderEngine> engines,
    ILogger<OcrService> logger) : IOcrService
{
    private static string MaskSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret)) return "";
        if (secret.Length <= 8) return "********";
        return secret[..4] + "...." + secret[^4..];
    }

    // -------------------------------------------------------------
    // OCR PROVIDERS (SUPER ADMIN)
    // -------------------------------------------------------------
    public async Task<List<OcrProviderDto>> GetAllProvidersAsync(CancellationToken cancellationToken = default)
    {
        var list = await dbContext.OcrProviders
            .AsNoTracking()
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return list.Select(p => new OcrProviderDto(
            p.Id,
            p.Name,
            p.ProviderType,
            p.EndpointUrl,
            MaskSecret(p.ApiKeyOrSecret),
            p.ModelIdOrProcessorId,
            p.Region,
            p.AdditionalConfigJson,
            p.IsEnabled,
            p.IsDefault,
            p.Description,
            p.CreatedAtUtc,
            p.ModifiedAtUtc
        )).ToList();
    }

    public async Task<OcrProviderDto?> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var p = await dbContext.OcrProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (p == null) return null;

        return new OcrProviderDto(
            p.Id,
            p.Name,
            p.ProviderType,
            p.EndpointUrl,
            MaskSecret(p.ApiKeyOrSecret),
            p.ModelIdOrProcessorId,
            p.Region,
            p.AdditionalConfigJson,
            p.IsEnabled,
            p.IsDefault,
            p.Description,
            p.CreatedAtUtc,
            p.ModifiedAtUtc
        );
    }

    public async Task<OcrProviderDto> CreateProviderAsync(CreateOcrProviderRequest request, CancellationToken cancellationToken = default)
    {
        // If this provider is marked as default, unset other defaults
        if (request.IsDefault)
        {
            var existingDefaults = await dbContext.OcrProviders.Where(x => x.IsDefault).ToListAsync(cancellationToken);
            foreach (var d in existingDefaults)
            {
                d.IsDefault = false;
            }
        }

        var provider = new OcrProviderConfig
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ProviderType = request.ProviderType,
            EndpointUrl = request.EndpointUrl,
            ApiKeyOrSecret = request.ApiKeyOrSecret,
            ModelIdOrProcessorId = request.ModelIdOrProcessorId,
            Region = request.Region,
            AdditionalConfigJson = request.AdditionalConfigJson ?? "{}",
            IsEnabled = request.IsEnabled,
            IsDefault = request.IsDefault,
            Description = request.Description,
            CreatedAtUtc = DateTime.UtcNow
        };

        dbContext.OcrProviders.Add(provider);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetProviderByIdAsync(provider.Id, cancellationToken))!;
    }

    public async Task<OcrProviderDto> UpdateProviderAsync(Guid id, UpdateOcrProviderRequest request, CancellationToken cancellationToken = default)
    {
        var provider = await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"OCR Provider with ID {id} not found.");

        if (request.IsDefault && !provider.IsDefault)
        {
            var existingDefaults = await dbContext.OcrProviders.Where(x => x.IsDefault && x.Id != id).ToListAsync(cancellationToken);
            foreach (var d in existingDefaults)
            {
                d.IsDefault = false;
            }
        }

        provider.Name = request.Name;
        provider.ProviderType = request.ProviderType;
        provider.EndpointUrl = request.EndpointUrl;

        // If the secret is not masked placeholder or empty, update it
        if (!string.IsNullOrWhiteSpace(request.ApiKeyOrSecret) && !request.ApiKeyOrSecret.Contains("...."))
        {
            provider.ApiKeyOrSecret = request.ApiKeyOrSecret;
        }

        provider.ModelIdOrProcessorId = request.ModelIdOrProcessorId;
        provider.Region = request.Region;
        provider.AdditionalConfigJson = request.AdditionalConfigJson ?? provider.AdditionalConfigJson;
        provider.IsEnabled = request.IsEnabled;
        provider.IsDefault = request.IsDefault;
        provider.Description = request.Description;
        provider.ModifiedAtUtc = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetProviderByIdAsync(provider.Id, cancellationToken))!;
    }

    public async Task<bool> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (provider == null) return false;

        dbContext.OcrProviders.Remove(provider);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OcrConnectionTestResult> TestProviderConnectionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var provider = await dbContext.OcrProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"OCR Provider with ID {id} not found.");

        var engine = engines.FirstOrDefault(e => e.ProviderType.Equals(provider.ProviderType, StringComparison.OrdinalIgnoreCase))
            ?? engines.First(e => e.ProviderType == "TesseractLocal");

        return await engine.TestConnectionAsync(provider, cancellationToken);
    }

    // -------------------------------------------------------------
    // TENANT OCR SETTINGS & LIMITS (SUPER ADMIN)
    // -------------------------------------------------------------
    public async Task<List<TenantOcrSettingDto>> GetAllTenantOcrSettingsAsync(CancellationToken cancellationToken = default)
    {
        var tenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var settings = await dbContext.TenantOcrSettings
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var providers = await dbContext.OcrProviders
            .AsNoTracking()
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        var defaultProvider = await dbContext.OcrProviders
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.IsDefault && p.IsEnabled, cancellationToken);

        var result = new List<TenantOcrSettingDto>();

        foreach (var tenant in tenants)
        {
            var setting = settings.FirstOrDefault(s => s.TenantId == tenant.Id);
            if (setting == null)
            {
                // Unconfigured tenant - returns default projection
                result.Add(new TenantOcrSettingDto(
                    Id: Guid.Empty,
                    TenantId: tenant.Id,
                    TenantName: tenant.Name,
                    TenantSlug: tenant.Slug,
                    IsOcrEnabled: true,
                    PreferredProviderId: defaultProvider?.Id,
                    PreferredProviderName: defaultProvider?.Name ?? "Tesseract Local Engine",
                    MaxScansLimit: 100,
                    UsedScansCount: 0,
                    RemainingScansCount: 100,
                    AllowOcrUploadPurchaseInvoice: true,
                    LastScanAtUtc: null,
                    Notes: null,
                    CreatedAtUtc: tenant.CreatedAt.UtcDateTime
                ));
            }
            else
            {
                string providerName = "System Default";
                if (setting.PreferredProviderId.HasValue && providers.TryGetValue(setting.PreferredProviderId.Value, out var pName))
                {
                    providerName = pName;
                }
                else if (defaultProvider != null)
                {
                    providerName = defaultProvider.Name;
                }

                result.Add(new TenantOcrSettingDto(
                    Id: setting.Id,
                    TenantId: tenant.Id,
                    TenantName: tenant.Name,
                    TenantSlug: tenant.Slug,
                    IsOcrEnabled: setting.IsOcrEnabled,
                    PreferredProviderId: setting.PreferredProviderId,
                    PreferredProviderName: providerName,
                    MaxScansLimit: setting.MaxScansLimit,
                    UsedScansCount: setting.UsedScansCount,
                    RemainingScansCount: Math.Max(0, setting.MaxScansLimit - setting.UsedScansCount),
                    AllowOcrUploadPurchaseInvoice: setting.AllowOcrUploadPurchaseInvoice,
                    LastScanAtUtc: setting.LastScanAtUtc,
                    Notes: setting.Notes,
                    CreatedAtUtc: setting.CreatedAtUtc
                ));
            }
        }

        return result;
    }

    public async Task<TenantOcrSettingDto> GetTenantOcrSettingAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        var setting = await dbContext.TenantOcrSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var defaultProvider = await dbContext.OcrProviders.AsNoTracking().FirstOrDefaultAsync(p => p.IsDefault && p.IsEnabled, cancellationToken);

        if (setting == null)
        {
            setting = new TenantOcrSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IsOcrEnabled = true,
                PreferredProviderId = defaultProvider?.Id,
                MaxScansLimit = 100,
                UsedScansCount = 0,
                AllowOcrUploadPurchaseInvoice = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.TenantOcrSettings.Add(setting);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        string providerName = "System Default";
        if (setting.PreferredProviderId.HasValue)
        {
            var p = await dbContext.OcrProviders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == setting.PreferredProviderId.Value, cancellationToken);
            if (p != null) providerName = p.Name;
        }
        else if (defaultProvider != null)
        {
            providerName = defaultProvider.Name;
        }

        return new TenantOcrSettingDto(
            Id: setting.Id,
            TenantId: tenant.Id,
            TenantName: tenant.Name,
            TenantSlug: tenant.Slug,
            IsOcrEnabled: setting.IsOcrEnabled,
            PreferredProviderId: setting.PreferredProviderId,
            PreferredProviderName: providerName,
            MaxScansLimit: setting.MaxScansLimit,
            UsedScansCount: setting.UsedScansCount,
            RemainingScansCount: Math.Max(0, setting.MaxScansLimit - setting.UsedScansCount),
            AllowOcrUploadPurchaseInvoice: setting.AllowOcrUploadPurchaseInvoice,
            LastScanAtUtc: setting.LastScanAtUtc,
            Notes: setting.Notes,
            CreatedAtUtc: setting.CreatedAtUtc
        );
    }

    public async Task<TenantOcrSettingDto> UpdateTenantOcrSettingAsync(Guid tenantId, UpdateTenantOcrSettingRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        var setting = await dbContext.TenantOcrSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (setting == null)
        {
            setting = new TenantOcrSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.TenantOcrSettings.Add(setting);
        }

        setting.IsOcrEnabled = request.IsOcrEnabled;
        setting.PreferredProviderId = request.PreferredProviderId;
        setting.MaxScansLimit = request.MaxScansLimit;
        setting.AllowOcrUploadPurchaseInvoice = request.AllowOcrUploadPurchaseInvoice;
        setting.Notes = request.Notes;
        setting.ModifiedAtUtc = DateTime.UtcNow;

        if (request.ResetScansCount == true)
        {
            setting.UsedScansCount = 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetTenantOcrSettingAsync(tenantId, cancellationToken);
    }

    public async Task<TenantOcrStatusDto> GetTenantOcrStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var setting = await dbContext.TenantOcrSettings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        var activeProviders = await dbContext.OcrProviders.AsNoTracking()
            .Where(p => p.IsEnabled)
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var defaultProvider = activeProviders.FirstOrDefault(p => p.IsDefault);

        Guid? activeProviderId = null;
        string providerName = "Tesseract Local Engine";
        string providerType = "TesseractLocal";

        if (setting?.PreferredProviderId.HasValue == true)
        {
            var assigned = activeProviders.FirstOrDefault(p => p.Id == setting.PreferredProviderId.Value);
            if (assigned != null)
            {
                activeProviderId = assigned.Id;
                providerName = assigned.Name;
                providerType = assigned.ProviderType;
            }
        }

        if (activeProviderId == null && defaultProvider != null)
        {
            activeProviderId = defaultProvider.Id;
            providerName = defaultProvider.Name;
            providerType = defaultProvider.ProviderType;
        }

        var availableList = activeProviders.Select(p => new TenantAvailableProviderDto(
            Id: p.Id,
            Name: p.Name,
            ProviderType: p.ProviderType,
            Description: p.Description,
            IsDefault: p.IsDefault,
            IsAssignedToTenant: setting?.PreferredProviderId.HasValue == true
                ? (p.Id == setting.PreferredProviderId.Value)
                : p.IsDefault
        )).ToList();

        if (setting == null)
        {
            return new TenantOcrStatusDto(
                IsOcrEnabled: true,
                CanUploadInvoice: true,
                ActiveProviderName: providerName,
                ActiveProviderType: providerType,
                ActiveProviderId: activeProviderId,
                MaxScansLimit: 100,
                UsedScansCount: 0,
                RemainingScans: 100,
                LastScanAtUtc: null,
                AvailableProviders: availableList
            );
        }

        int remaining = Math.Max(0, setting.MaxScansLimit - setting.UsedScansCount);
        bool canUpload = setting.IsOcrEnabled && setting.AllowOcrUploadPurchaseInvoice && (remaining > 0);

        return new TenantOcrStatusDto(
            IsOcrEnabled: setting.IsOcrEnabled,
            CanUploadInvoice: canUpload,
            ActiveProviderName: providerName,
            ActiveProviderType: providerType,
            ActiveProviderId: activeProviderId,
            MaxScansLimit: setting.MaxScansLimit,
            UsedScansCount: setting.UsedScansCount,
            RemainingScans: remaining,
            LastScanAtUtc: setting.LastScanAtUtc,
            AvailableProviders: availableList
        );
    }

    // -------------------------------------------------------------
    // INVOICE PROCESSING & AUDIT LOGGING
    // -------------------------------------------------------------
    public async Task<StructuredInvoiceOcrResult> ProcessInvoiceAsync(
        Guid tenantId,
        Stream documentStream,
        string fileName,
        string mimeType,
        string? clientRawText,
        string scannedBy,
        Guid? preferredProviderId = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        // 1. Verify tenant & settings
        var setting = await dbContext.TenantOcrSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken);
        if (setting == null)
        {
            setting = new TenantOcrSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                IsOcrEnabled = true,
                MaxScansLimit = 100,
                UsedScansCount = 0,
                AllowOcrUploadPurchaseInvoice = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            dbContext.TenantOcrSettings.Add(setting);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!setting.IsOcrEnabled || !setting.AllowOcrUploadPurchaseInvoice)
        {
            await LogScanAsync(tenantId, null, "Disabled", "Disabled", fileName, documentStream.Length, mimeType,
                "Disabled", "OCR scanning is disabled for this tenant by platform super admin.", 0, null, null, null, null, null, scannedBy, cancellationToken);

            throw new InvalidOperationException("OCR purchase bill scanning is disabled for your store by the platform administrator.");
        }

        // 2. Check quota
        if (setting.UsedScansCount >= setting.MaxScansLimit)
        {
            await LogScanAsync(tenantId, null, "QuotaExceeded", "QuotaExceeded", fileName, documentStream.Length, mimeType,
                "QuotaExceeded", $"OCR scan quota limit reached ({setting.UsedScansCount}/{setting.MaxScansLimit}).", 0, null, null, null, null, null, scannedBy, cancellationToken);

            throw new InvalidOperationException($"OCR scan quota limit reached ({setting.UsedScansCount}/{setting.MaxScansLimit} scans used). Please contact platform administrator to increase your scan limit.");
        }

        // 3. Resolve active provider
        OcrProviderConfig? activeProvider = null;

        // 3a. Explicit requested provider from invoice upload (if valid and enabled)
        if (preferredProviderId.HasValue)
        {
            activeProvider = await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.Id == preferredProviderId.Value && x.IsEnabled, cancellationToken);
        }

        // 3b. Assigned provider method configured for tenant by super admin
        if (activeProvider == null && setting.PreferredProviderId.HasValue)
        {
            activeProvider = await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.Id == setting.PreferredProviderId.Value && x.IsEnabled, cancellationToken);
        }

        // 3c. Platform default or any enabled provider
        activeProvider ??= await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.IsDefault && x.IsEnabled, cancellationToken);
        activeProvider ??= await dbContext.OcrProviders.FirstOrDefaultAsync(x => x.IsEnabled, cancellationToken);
        activeProvider ??= new OcrProviderConfig
        {
            Id = Guid.Parse("c1a00000-0000-0000-0000-000000000001"),
            Name = "Tesseract Local Engine",
            ProviderType = "TesseractLocal",
            IsEnabled = true,
            IsDefault = true
        };

        // 4. Resolve engine
        var engine = engines.FirstOrDefault(e => e.ProviderType.Equals(activeProvider.ProviderType, StringComparison.OrdinalIgnoreCase))
            ?? engines.First(e => e.ProviderType == "TesseractLocal");

        StructuredInvoiceOcrResult result;
        try
        {
            result = await engine.ExtractInvoiceAsync(documentStream, fileName, mimeType, clientRawText, activeProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "OCR provider {Provider} failed for invoice {FileName}. Falling back to Local Engine...", activeProvider.Name, fileName);

            // Resilient fallback to Local Tesseract if cloud provider failed
            if (engine.ProviderType != "TesseractLocal")
            {
                var fallbackEngine = engines.First(e => e.ProviderType == "TesseractLocal");
                result = await fallbackEngine.ExtractInvoiceAsync(documentStream, fileName, mimeType, clientRawText, activeProvider, cancellationToken);
            }
            else
            {
                await LogScanAsync(tenantId, activeProvider.Id, activeProvider.Name, activeProvider.ProviderType, fileName, documentStream.Length, mimeType,
                    "Failed", ex.Message, sw.ElapsedMilliseconds, null, null, null, null, null, scannedBy, cancellationToken);
                throw;
            }
        }

        sw.Stop();

        // 5. Increment usage count and save
        setting.UsedScansCount += 1;
        setting.LastScanAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        int remaining = Math.Max(0, setting.MaxScansLimit - setting.UsedScansCount);

        // 6. Log successful scan
        await LogScanAsync(
            tenantId,
            activeProvider.Id,
            activeProvider.Name,
            activeProvider.ProviderType,
            fileName,
            documentStream.Length,
            mimeType,
            "Success",
            null,
            sw.ElapsedMilliseconds,
            result.Header.VendorName,
            result.Header.BillNumber,
            result.Summary.GrandTotal,
            result.Items.Count,
            JsonSerializer.Serialize(result),
            scannedBy,
            cancellationToken
        );

        return result with { RemainingScans = remaining, ProcessingTimeMs = sw.ElapsedMilliseconds };
    }

    private async Task LogScanAsync(
        Guid tenantId,
        Guid? providerId,
        string providerName,
        string providerType,
        string fileName,
        long fileSize,
        string mimeType,
        string status,
        string? errorMessage,
        long processingTimeMs,
        string? vendorName,
        string? billNumber,
        decimal? grandTotal,
        int? itemCount,
        string? dataJson,
        string scannedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = new TenantOcrScanLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ProviderId = providerId,
                ProviderName = providerName,
                ProviderType = providerType,
                FileName = fileName,
                FileSize = fileSize,
                MimeType = mimeType,
                Status = status,
                ErrorMessage = errorMessage,
                ProcessingTimeMs = processingTimeMs,
                ExtractedVendorName = vendorName,
                ExtractedBillNumber = billNumber,
                ExtractedGrandTotal = grandTotal,
                ExtractedItemCount = itemCount,
                ExtractedDataJson = dataJson,
                ScannedBy = string.IsNullOrWhiteSpace(scannedBy) ? "System" : scannedBy,
                ScannedAtUtc = DateTime.UtcNow
            };

            dbContext.TenantOcrScanLogs.Add(log);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist TenantOcrScanLog.");
        }
    }

    public async Task<List<TenantOcrScanLog>> GetTenantScanLogsAsync(Guid? tenantId = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var query = dbContext.TenantOcrScanLogs.AsNoTracking().AsQueryable();
        if (tenantId.HasValue && tenantId.Value != Guid.Empty)
        {
            query = query.Where(l => l.TenantId == tenantId.Value);
        }

        return await query.OrderByDescending(l => l.ScannedAtUtc).Take(limit).ToListAsync(cancellationToken);
    }
}
