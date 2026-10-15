namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record DatabaseBackupExportResult(
    byte[] Content,
    string FileName,
    string ContentType,
    int TableCount,
    long TotalRows);

