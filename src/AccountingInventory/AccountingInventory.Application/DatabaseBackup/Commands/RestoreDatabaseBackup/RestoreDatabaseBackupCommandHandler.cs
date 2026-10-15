using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Commands.RestoreDatabaseBackup;

public sealed class RestoreDatabaseBackupCommandHandler(IDatabaseBackupService backupService)
    : IRequestHandler<RestoreDatabaseBackupCommand, DatabaseRestoreResultDto>
{
    public async Task<DatabaseRestoreResultDto> Handle(RestoreDatabaseBackupCommand request, CancellationToken cancellationToken)
    {
        return await backupService.RestoreFromExcelAsync(
            request.Schema,
            request.ExcelStream,
            request.FileName,
            request.SelectedTables,
            request.UserId,
            request.UserEmail,
            cancellationToken);
    }
}

