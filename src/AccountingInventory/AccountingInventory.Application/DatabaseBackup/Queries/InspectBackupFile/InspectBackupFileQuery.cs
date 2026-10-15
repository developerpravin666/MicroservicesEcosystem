using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.InspectBackupFile;

public sealed record InspectBackupFileQuery(
    Stream ExcelStream,
    string FileName) : IRequest<InspectBackupFileResultDto>;

