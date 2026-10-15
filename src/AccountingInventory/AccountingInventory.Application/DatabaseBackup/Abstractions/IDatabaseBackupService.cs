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
}

