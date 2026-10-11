using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public enum MasterImportStatus
{
    PendingVerification = 1,
    Approved = 2,
    Rejected = 3,
    PartiallyApproved = 4,
    Failed = 5
}

/// <summary>
/// Represents a master data import batch (Vendors, Brands, Product Types, Models, Variants, Colors).
/// Staged in the tenant database first for review, verification, and explicit approval before
/// committing to the main production tables.
/// </summary>
public sealed class MasterImportBatch : AggregateRoot
{
    public string BatchNumber { get; private set; } = null!;
    public string FileName { get; private set; } = null!;
    public string FileType { get; private set; } = "Excel/CSV";
    public MasterImportStatus Status { get; private set; } = MasterImportStatus.PendingVerification;
    public int TotalRows { get; private set; }
    public int ValidRows { get; private set; }
    public int ErrorRows { get; private set; }
    public int WarningRows { get; private set; }
    public int CreatedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public string? SummaryJson { get; private set; }
    public string? ReviewNotes { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public string? ReviewedBy { get; private set; }

    public List<MasterImportStagingRow> Rows { get; private set; } = new();

    public static MasterImportBatch Create(
        string batchNumber,
        string fileName,
        string fileType,
        int totalRows,
        int validRows,
        int errorRows,
        int warningRows,
        int createdCount,
        int updatedCount,
        string? summaryJson = null,
        Guid? createdBy = null)
    {
        return new MasterImportBatch
        {
            Id = Guid.NewGuid(),
            BatchNumber = batchNumber.Trim(),
            FileName = fileName.Trim(),
            FileType = string.IsNullOrWhiteSpace(fileType) ? "Excel/CSV" : fileType.Trim(),
            Status = MasterImportStatus.PendingVerification,
            TotalRows = totalRows,
            ValidRows = validRows,
            ErrorRows = errorRows,
            WarningRows = warningRows,
            CreatedCount = createdCount,
            UpdatedCount = updatedCount,
            SummaryJson = summaryJson,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            IsActive = true,
            IsDeleted = false
        };
    }

    public void UpdateStats(int total, int valid, int error, int warning, int created, int updated, string? summaryJson)
    {
        TotalRows = total;
        ValidRows = valid;
        ErrorRows = error;
        WarningRows = warning;
        CreatedCount = created;
        UpdatedCount = updated;
        SummaryJson = summaryJson;
    }

    public void Approve(string? reviewedBy, string? notes = null)
    {
        Status = MasterImportStatus.Approved;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedBy = reviewedBy?.Trim() ?? "System";
        ReviewNotes = notes?.Trim();
    }

    public void Reject(string? reviewedBy, string? notes = null)
    {
        Status = MasterImportStatus.Rejected;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewedBy = reviewedBy?.Trim() ?? "System";
        ReviewNotes = notes?.Trim();
    }

    public void MarkFailed(string? errorReason)
    {
        Status = MasterImportStatus.Failed;
        ReviewedAt = DateTimeOffset.UtcNow;
        ReviewNotes = errorReason?.Trim();
    }

    private MasterImportBatch() { }
}

