using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.GetBackupTables;

public sealed record GetBackupTablesQuery(string Schema) : IRequest<IReadOnlyList<BackupTableInfoDto>>;

