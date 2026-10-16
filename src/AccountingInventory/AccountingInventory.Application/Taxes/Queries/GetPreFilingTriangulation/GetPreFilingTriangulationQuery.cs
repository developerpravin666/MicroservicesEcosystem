using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetPreFilingTriangulation;

public sealed record GetPreFilingTriangulationQuery(
    DateOnly? FromDate,
    DateOnly? ToDate
) : IRequest<PreFilingTriangulationResultDto>;

public sealed record PreFilingTriangulationResultDto(
    string TenantGstin,
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalVouchersScanned,
    int TotalExceptionsCount,
    int CriticalErrorsCount,
    int WarningsCount,
    bool IsFilingReady,
    IReadOnlyList<TriangulationExceptionDto> Exceptions
);

public sealed record TriangulationExceptionDto(
    string Severity, // "Critical" or "Warning"
    string Category, // "InvalidGSTIN", "MissingHSN", "TaxMismatch", "InterStateTaxMismatch", "MismatchedLedgers", "MissingStateCode"
    string VoucherType, // "SalesInvoice", "PurchaseBill", "JournalEntry", "CreditNote"
    string VoucherNumber,
    DateOnly? VoucherDate,
    string? PartyName,
    string? PartyGstin,
    decimal ImpactedAmount,
    string Description,
    string RecommendedAction
);

