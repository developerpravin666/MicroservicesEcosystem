namespace AccountingInventory.Application.OCR.Models;

public sealed record OcrProviderDto(
    Guid Id,
    string Name,
    string ProviderType,
    string? EndpointUrl,
    string? ApiKeyOrSecret,
    string? ModelIdOrProcessorId,
    string? Region,
    string? AdditionalConfigJson,
    bool IsEnabled,
    bool IsDefault,
    string? Description,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc
);

public sealed record CreateOcrProviderRequest(
    string Name,
    string ProviderType,
    string? EndpointUrl,
    string? ApiKeyOrSecret,
    string? ModelIdOrProcessorId,
    string? Region,
    string? AdditionalConfigJson,
    bool IsEnabled,
    bool IsDefault,
    string? Description
);

public sealed record UpdateOcrProviderRequest(
    string Name,
    string ProviderType,
    string? EndpointUrl,
    string? ApiKeyOrSecret,
    string? ModelIdOrProcessorId,
    string? Region,
    string? AdditionalConfigJson,
    bool IsEnabled,
    bool IsDefault,
    string? Description
);

public sealed record TenantOcrSettingDto(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    bool IsOcrEnabled,
    Guid? PreferredProviderId,
    string? PreferredProviderName,
    int MaxScansLimit,
    int UsedScansCount,
    int RemainingScansCount,
    bool AllowOcrUploadPurchaseInvoice,
    DateTime? LastScanAtUtc,
    string? Notes,
    DateTime CreatedAtUtc
);

public sealed record UpdateTenantOcrSettingRequest(
    bool IsOcrEnabled,
    Guid? PreferredProviderId,
    int MaxScansLimit,
    bool AllowOcrUploadPurchaseInvoice,
    string? Notes,
    bool? ResetScansCount
);

public sealed record OcrConnectionTestResult(
    bool Success,
    string Message,
    long ResponseTimeMs
);

public sealed record ExtractedInvoiceHeader(
    string VendorName,
    string VendorGstin,
    string VendorAddress,
    string? VendorPhone,
    string VendorEmail,
    string VendorState,
    string VendorStateCode,
    string BuyerName,
    string BuyerGstin,
    string BuyerState,
    string BuyerStateCode,
    string BillNumber,
    string BillDate,
    string DueDate,
    int PaymentTermsDays,
    string BankName,
    string BankAccountNo,
    string BankIfsc,
    string Pan
);

public sealed record ExtractedInvoiceItem(
    string Id,
    int LineNumber,
    string Description,
    string Brand,
    string Model,
    string Variant,
    string Color,
    string SerialNumber,
    string? SerialNumber1,
    string HsnCode,
    int Quantity,
    string Unit,
    decimal TaxableRate,
    decimal TaxableAmount,
    decimal CgstRate,
    decimal SgstRate,
    decimal GstRate,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal TotalTaxAmount,
    decimal TotalAmount
);

public sealed record ExtractedInvoiceSummary(
    int TotalItems,
    int TotalQuantity,
    decimal SubtotalTaxable,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal TotalTax,
    decimal RoundOff,
    decimal GrandTotal,
    string? AmountInWords
);

public sealed record StructuredInvoiceOcrResult(
    ExtractedInvoiceHeader Header,
    List<ExtractedInvoiceItem> Items,
    ExtractedInvoiceSummary Summary,
    string RawText,
    double Confidence,
    string ProviderUsed,
    long ProcessingTimeMs,
    int RemainingScans
);

public sealed record TenantAvailableProviderDto(
    Guid Id,
    string Name,
    string ProviderType,
    string? Description,
    bool IsDefault,
    bool IsAssignedToTenant
);

public sealed record TenantOcrStatusDto(
    bool IsOcrEnabled,
    bool CanUploadInvoice,
    string ActiveProviderName,
    string ActiveProviderType,
    Guid? ActiveProviderId,
    int MaxScansLimit,
    int UsedScansCount,
    int RemainingScans,
    DateTime? LastScanAtUtc,
    List<TenantAvailableProviderDto> AvailableProviders
);


