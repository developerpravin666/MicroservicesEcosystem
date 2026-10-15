namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record DatabaseRestoreResultDto(
    bool Success,
    string Message,
    string TargetSchema,
    string BackupFileName,
    DateTime RestoredAtUtc,
    int TotalTablesInFile,
    int SelectedTablesCount,
    int TotalRowsProcessed,
    int TotalInserted,
    int TotalUpdated,
    int TotalUnchanged,
    int TotalErrors,
    IReadOnlyList<TableRestoreAuditDto> TableAudits,
    Guid? AuditLogId = null);

