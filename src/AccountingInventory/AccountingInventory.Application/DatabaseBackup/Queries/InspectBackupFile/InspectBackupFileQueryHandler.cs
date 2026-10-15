using AccountingInventory.Application.DatabaseBackup.Abstractions;
using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.InspectBackupFile;

public sealed class InspectBackupFileQueryHandler(IDatabaseBackupService backupService)
    : IRequestHandler<InspectBackupFileQuery, InspectBackupFileResultDto>
{
    public async Task<InspectBackupFileResultDto> Handle(InspectBackupFileQuery request, CancellationToken cancellationToken)
    {
        return await backupService.InspectBackupFileAsync(request.ExcelStream, request.FileName, cancellationToken);
    }
}

