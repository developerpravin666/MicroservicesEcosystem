namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record DatabaseSnapshotDto(
    Guid Id,
    string BackupName,
    string BackupSchemaName,
    string DatabaseName,
    string SourceSchema,
    int TotalTables,
    long TotalRows,
    string Status,
    DateTime CreatedAtUtc,
    string CreatedBy,
    string? Notes,
    DateTime? LastRestoredAtUtc,
    string? LastRestoredBy,
    DateTime? DroppedAtUtc,
    string? DroppedBy,
    IReadOnlyList<SnapshotTableInfoDto> Tables
);

public sealed record SnapshotTableInfoDto(
    string TableName,
    string DisplayName,
    long RowCount,
    string TempTableName
);

public sealed record DropBackupSchemaResultDto(
    bool Success,
    string Message,
    int DroppedTablesCount,
    IReadOnlyList<string> DroppedTables,
    bool SchemaDropped
);

public sealed record DatabaseBackupRestoreLogDto(
    Guid Id,
    Guid? BackupId,
    string BackupSchemaName,
    string TargetSchema,
    string Action,
    string Status,
    long TotalRowsProcessed,
    int TotalInserted,
    int TotalUpdated,
    int TotalErrors,
    string? DetailsJson,
    string? Message,
    DateTime ExecutedAtUtc,
    string ExecutedBy
);
