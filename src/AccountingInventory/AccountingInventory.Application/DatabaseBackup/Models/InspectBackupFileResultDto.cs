namespace AccountingInventory.Application.DatabaseBackup.Models;

public sealed record InspectBackupFileResultDto(
    string FileName,
    string? SourceSchema,
    DateTime? ExportedAtUtc,
    string? FormatVersion,
    int TotalTablesInFile,
    long TotalRowsInFile,
    IReadOnlyList<BackupTableInfoDto> Tables);

