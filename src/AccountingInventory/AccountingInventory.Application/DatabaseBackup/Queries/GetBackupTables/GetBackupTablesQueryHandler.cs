using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.GetBackupTables;

public sealed class GetBackupTablesQueryHandler(IDatabaseBackupService backupService)
    : IRequestHandler<GetBackupTablesQuery, IReadOnlyList<BackupTableInfoDto>>
{
    public async Task<IReadOnlyList<BackupTableInfoDto>> Handle(GetBackupTablesQuery request, CancellationToken cancellationToken)
    {
        return await backupService.GetEligibleTablesAsync(request.Schema, cancellationToken);
    }
}

