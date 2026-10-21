using AccountingInventory.Application.DatabaseBackup.Models;

namespace AccountingInventory.Application.DatabaseBackup.Abstractions;

public interface IDatabaseBackupService
{
    Task<IReadOnlyList<BackupTableInfoDto>> GetEligibleTablesAsync(string schema, CancellationToken ct);

    Task<DatabaseBackupExportResult> ExportTablesToExcelAsync(
        string schema,
        IReadOnlyList<string>? selectedTables,
        string requestedBy,
        CancellationToken ct);

    Task<InspectBackupFileResultDto> InspectBackupFileAsync(
        Stream excelStream,
        string fileName,
        CancellationToken ct);

    Task<DatabaseRestoreResultDto> RestoreFromExcelAsync(
        string schema,
        Stream excelStream,
        string fileName,
        IReadOnlyList<string>? selectedTables,
        Guid? userId,
        string userEmail,
        CancellationToken ct);

    Task<DatabaseSnapshotDto> CreateDatabaseSnapshotAsync(
        string schema,
        IReadOnlyList<string>? selectedTables,
        string? backupName,
        string? notes,
        string createdBy,
        CancellationToken ct);

    Task<IReadOnlyList<DatabaseSnapshotDto>> GetDatabaseSnapshotsAsync(
        string? schema,
        CancellationToken ct);

    Task<DatabaseRestoreResultDto> RestoreFromDatabaseSnapshotAsync(
        Guid snapshotId,
        string? targetSchemaOverride,
        IReadOnlyList<string>? selectedTables,
        Guid? userId,
        string userEmail,
        CancellationToken ct);

    Task<bool> DeleteDatabaseSnapshotAsync(
        Guid snapshotId,
        CancellationToken ct);

    Task<bool> DeleteDatabaseBackupAndDropSchemaAsync(
        Guid backupId,
        string userEmail,
        CancellationToken ct);

    Task<DropBackupSchemaResultDto> DropBackupTempDataAndSchemaAsync(
        CancellationToken ct);

    Task<IReadOnlyList<DatabaseBackupRestoreLogDto>> GetRestoreLogsAsync(
        CancellationToken ct);
}


