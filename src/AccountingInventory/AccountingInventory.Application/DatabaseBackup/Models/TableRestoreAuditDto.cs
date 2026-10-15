namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record TableRestoreAuditDto(
    string TableName,
    string DisplayName,
    int RowsInFile,
    int InsertedCount,
    int UpdatedCount,
    int UnchangedCount,
    int ErrorCount,
    string Status, // "Success", "Partial", "Skipped", "Failed"
    IReadOnlyList<string> SampleDifferences,
    string? ErrorMessage = null);

