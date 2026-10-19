using AccountingInventory.Application.OCR.Models;
using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.OCR;

public interface IOcrService
{
    // Providers
    Task<List<OcrProviderDto>> GetAllProvidersAsync(CancellationToken cancellationToken = default);
    Task<OcrProviderDto?> GetProviderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OcrProviderDto> CreateProviderAsync(CreateOcrProviderRequest request, CancellationToken cancellationToken = default);
    Task<OcrProviderDto> UpdateProviderAsync(Guid id, UpdateOcrProviderRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteProviderAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OcrConnectionTestResult> TestProviderConnectionAsync(Guid id, CancellationToken cancellationToken = default);

    // Tenant OCR Settings
    Task<List<TenantOcrSettingDto>> GetAllTenantOcrSettingsAsync(CancellationToken cancellationToken = default);
    Task<TenantOcrSettingDto> GetTenantOcrSettingAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantOcrSettingDto> UpdateTenantOcrSettingAsync(Guid tenantId, UpdateTenantOcrSettingRequest request, CancellationToken cancellationToken = default);
    Task<TenantOcrStatusDto> GetTenantOcrStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // Invoice Processing
    Task<StructuredInvoiceOcrResult> ProcessInvoiceAsync(
        Guid tenantId,
        Stream documentStream,
        string fileName,
        string mimeType,
        string? clientRawText,
        string scannedBy,
        CancellationToken cancellationToken = default);

    // Logs & Audit
    Task<List<TenantOcrScanLog>> GetTenantScanLogsAsync(Guid? tenantId = null, int limit = 50, CancellationToken cancellationToken = default);
}

