namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record TableRestoreAuditDto(
    string TableName,
    string DisplayName,
    long RowsInFile,
    long InsertedCount,
    long UpdatedCount,
    long UnchangedCount,
    long ErrorCount,
    string Status, // "Success", "Partial", "Skipped", "Failed"
    IReadOnlyList<string> SampleDifferences,
    string? ErrorMessage = null);

