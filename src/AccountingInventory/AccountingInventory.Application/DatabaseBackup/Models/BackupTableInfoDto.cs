namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record BackupTableInfoDto(
    string TableName,
    string DisplayName,
    string TableType,
    string Category,
    long RowCount,
    bool IsSelected = true,
    bool IsSystem = false);

