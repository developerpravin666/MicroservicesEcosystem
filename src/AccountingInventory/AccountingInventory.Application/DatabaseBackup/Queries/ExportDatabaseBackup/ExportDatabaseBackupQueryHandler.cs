using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.ExportDatabaseBackup;

public sealed class ExportDatabaseBackupQueryHandler(IDatabaseBackupService backupService)
    : IRequestHandler<ExportDatabaseBackupQuery, DatabaseBackupExportResult>
{
    public async Task<DatabaseBackupExportResult> Handle(ExportDatabaseBackupQuery request, CancellationToken cancellationToken)
    {
        return await backupService.ExportTablesToExcelAsync(
            request.Schema,
            request.SelectedTables,
            request.RequestedBy,
            cancellationToken);
    }
}

