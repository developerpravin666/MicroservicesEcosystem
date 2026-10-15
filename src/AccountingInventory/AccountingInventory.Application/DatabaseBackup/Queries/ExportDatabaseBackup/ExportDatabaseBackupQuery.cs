using AccountingInventory.Application.DatabaseBackup.Models;
using MediatR;

namespace AccountingInventory.Application.DatabaseBackup.Queries.ExportDatabaseBackup;

public sealed record ExportDatabaseBackupQuery(
    string Schema,
    IReadOnlyList<string>? SelectedTables,
    string RequestedBy) : IRequest<DatabaseBackupExportResult>;

