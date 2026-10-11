using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum StagingRowStatus
{
    Valid = 1,
    Invalid = 2,
    Warning = 3
}

public enum StagingRowAction
{
    Create = 1,
    Update = 2,
    Skip = 3
}

/// <summary>
/// Individual row in a master data import batch.
/// Holds the staged record (Vendor, Brand, ProductType, Model, Variant, Color),
/// validation outcome, action (Create vs Update), and verification status.
/// </summary>
public sealed class MasterImportStagingRow : AggregateRoot
{
    public Guid BatchId { get; private set; }
    public int RowIndex { get; private set; }
    public string EntityType { get; private set; } = null!; // "Vendor", "Brand", "ProductType", "Model", "Variant", "Color"
    public StagingRowAction Action { get; private set; } = StagingRowAction.Create;
    public StagingRowStatus Status { get; private set; } = StagingRowStatus.Valid;
    public string EntityKey { get; private set; } = null!; // e.g., Vendor Code or Name
    public string EntityName { get; private set; } = null!;
    public string? ValidationErrors { get; private set; }
    public string RawDataJson { get; private set; } = null!;
    public bool IsApproved { get; private set; } = true;
    public bool IsImported { get; private set; } = false;
    public DateTimeOffset? ImportedAt { get; private set; }
    public string? ImportMessage { get; private set; }

    public static MasterImportStagingRow Create(
        Guid batchId,
        int rowIndex,
        string entityType,
        StagingRowAction action,
        StagingRowStatus status,
        string entityKey,
        string entityName,
        string rawDataJson,
        string? validationErrors = null)
    {
        return new MasterImportStagingRow
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            RowIndex = rowIndex,
            EntityType = entityType.Trim(),
            Action = action,
            Status = status,
            EntityKey = entityKey.Trim(),
            EntityName = entityName.Trim(),
            RawDataJson = rawDataJson,
            ValidationErrors = validationErrors,
            IsApproved = status == StagingRowStatus.Valid || status == StagingRowStatus.Warning,
            IsImported = false,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            IsDeleted = false
        };
    }

    public void SetApproved(bool approved)
    {
        IsApproved = approved;
    }

    public void MarkImported(string? message = null)
    {
        IsImported = true;
        ImportedAt = DateTimeOffset.UtcNow;
        ImportMessage = message ?? "Successfully imported to main database.";
    }

    public void MarkFailed(string reason)
    {
        IsImported = false;
        Status = StagingRowStatus.Invalid;
        ImportMessage = reason;
        ValidationErrors = string.IsNullOrWhiteSpace(ValidationErrors)
            ? reason
            : $"{ValidationErrors} | {reason}";
    }

    private MasterImportStagingRow() { }
}

