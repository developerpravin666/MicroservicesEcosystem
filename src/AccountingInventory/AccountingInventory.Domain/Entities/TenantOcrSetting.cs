namespace AccountingInventory.Domain.Entities;

public sealed class TenantOcrSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public bool IsOcrEnabled { get; set; } = true;
    public Guid? PreferredProviderId { get; set; }
    public int MaxScansLimit { get; set; } = 100;
    public int UsedScansCount { get; set; } = 0;
    public bool AllowOcrUploadPurchaseInvoice { get; set; } = true;
    public DateTime? LastScanAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }
}

