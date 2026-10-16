using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Excel;

public sealed record ExportGstr1ExcelQuery(DateOnly? FromDate, DateOnly? ToDate) : IRequest<Gstr1ExcelExportResult>;

public sealed record Gstr1ExcelExportResult(
    string FileName,
    string ContentType,
    byte[] FileBytes
);

