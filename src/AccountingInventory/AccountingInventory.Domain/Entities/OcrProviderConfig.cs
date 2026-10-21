namespace AccountingInventory.Domain.Entities;

public sealed class OcrProviderConfig
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string ProviderType { get; set; } = "TesseractLocal"; // TesseractLocal, AzureDocumentIntelligence, GoogleDocumentAi, AwsTextract, Custom
    public string? EndpointUrl { get; set; }
    public string? ApiKeyOrSecret { get; set; }
    public string? ModelIdOrProcessorId { get; set; }
    public string? Region { get; set; }
    public string? AdditionalConfigJson { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ModifiedAtUtc { get; set; }
}

