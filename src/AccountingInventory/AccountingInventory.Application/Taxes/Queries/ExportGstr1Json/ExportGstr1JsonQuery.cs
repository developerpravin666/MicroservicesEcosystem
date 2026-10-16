using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Json;

public sealed record ExportGstr1JsonQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<Gstr1JsonExportResult>;

public sealed record Gstr1JsonExportResult(
    string FileName,
    string ContentType,
    string JsonPayload
);

