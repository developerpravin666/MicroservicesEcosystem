namespace AccountingInventory.Application.MasterImport;

public sealed class MatchedExistingFieldDto
{
    public string FieldName { get; set; } = "";
    public string? ExistingValue { get; set; }
    public string? IncomingValue { get; set; }
    public bool IsMatching { get; set; } // true if same, false if differs/updated
}

public sealed class MatchedExistingRecordDto
{
    public string ExistingId { get; set; } = "";
    public string MatchReason { get; set; } = ""; // e.g. "Matched existing Vendor by Code 'VEN-APP-001'"
    public List<MatchedExistingFieldDto> FieldComparisons { get; set; } = new();
}

public sealed class MasterImportRowDto
{
    public string EntityType { get; set; } = ""; // Vendor, Brand, ProductType, Model, Variant, Color
    public string Name { get; set; } = "";
    public string? Code { get; set; }
    public string? Mobile { get; set; }
    public string? Email { get; set; }
    public string? Brand { get; set; } // Brand name or code for Model
    public string? ProductType { get; set; } // ProductType name for Model
    public string? Description { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public MatchedExistingRecordDto? MatchedRecord { get; set; }
}

public sealed record MasterImportStagingRowDto(
    Guid Id,
    Guid BatchId,
    int RowIndex,
    string EntityType,
    string Action,
    string Status,
    string EntityKey,
    string EntityName,
    string? ValidationErrors,
    MasterImportRowDto? RawData,
    bool IsApproved,
    bool IsImported,
    DateTimeOffset? ImportedAt,
    string? ImportMessage,
    MatchedExistingRecordDto? MatchedRecord = null);

public sealed record MasterImportBatchSummaryDto(
    Guid Id,
    string BatchNumber,
    string FileName,
    string FileType,
    string Status,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int WarningRows,
    int CreatedCount,
    int UpdatedCount,
    string? SummaryJson,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset? ReviewedAt,
    string? ReviewedBy,
    string? ReviewNotes,
    IReadOnlyList<MasterImportStagingRowDto>? Rows = null);

public sealed record ApproveImportResult(
    bool Success,
    Guid BatchId,
    string BatchNumber,
    string Status,
    int TotalImported,
    int BrandsImported,
    int ProductTypesImported,
    int ModelsImported,
    int VariantsImported,
    int ColorsImported,
    int VendorsImported,
    string Message,
    DateTimeOffset CompletedAt);

public sealed record MasterImportUploadRequest(
    string? FileName,
    string? CsvContent,
    string? Notes,
    string? Base64Content = null);

public sealed record ToggleRowApprovalRequest(
    bool IsApproved);

public sealed record RejectBatchRequest(
    string? Reason);

