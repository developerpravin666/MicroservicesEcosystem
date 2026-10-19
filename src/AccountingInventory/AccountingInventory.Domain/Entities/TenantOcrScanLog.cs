namespace AccountingInventory.Domain.Entities;

public sealed class TenantOcrScanLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public string Status { get; set; } = "Success"; // Success, Failed, QuotaExceeded, Disabled
    public string? ErrorMessage { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? ExtractedVendorName { get; set; }
    public string? ExtractedBillNumber { get; set; }
    public decimal? ExtractedGrandTotal { get; set; }
    public int? ExtractedItemCount { get; set; }
    public string? ExtractedDataJson { get; set; }
    public string ScannedBy { get; set; } = string.Empty;
    public DateTime ScannedAtUtc { get; set; } = DateTime.UtcNow;
}

