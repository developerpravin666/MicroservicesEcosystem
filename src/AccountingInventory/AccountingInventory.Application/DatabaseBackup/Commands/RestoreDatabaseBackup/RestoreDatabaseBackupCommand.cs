using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Commands.RestoreDatabaseBackup;

public sealed record RestoreDatabaseBackupCommand(
    string Schema,
    Stream ExcelStream,
    string FileName,
    IReadOnlyList<string>? SelectedTables,
    Guid? UserId,
    string UserEmail) : IRequest<DatabaseRestoreResultDto>;

