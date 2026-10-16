using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.ReconcileGstr2b;

public sealed record ReconcileGstr2bCommand(
    string JsonContent,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null
) : IRequest<Gstr2bReconciliationResultDto>;

public sealed record Gstr2bReconciliationResultDto(
    string TenantGstin,
    string ReturnPeriod,
    DateOnly FromDate,
    DateOnly ToDate,
    int TotalInvoicesCount,
    int MatchedCount,
    int BooksOnlyCount,
    int PortalOnlyCount,
    int ValueMismatchCount,
    decimal TotalMatchedItc,
    decimal TotalBooksOnlyItc,
    decimal TotalPortalOnlyItc,
    decimal TotalValueMismatchItcDiff,
    IReadOnlyList<Gstr2bReconciliationItemDto> Items
);

public sealed record Gstr2bReconciliationItemDto(
    string Status, // "MATCHED", "BOOKS_ONLY", "PORTAL_ONLY", "VALUE_MISMATCH"
    string SupplierGstin,
    string SupplierName,
    string InvoiceNumber,
    DateOnly? InvoiceDate,
    decimal BooksTaxableValue,
    decimal BooksTaxAmount,
    decimal BooksCgst,
    decimal BooksSgst,
    decimal BooksIgst,
    decimal PortalTaxableValue,
    decimal PortalTaxAmount,
    decimal PortalCgst,
    decimal PortalSgst,
    decimal PortalIgst,
    decimal DifferenceAmount,
    string? DiscrepancyReason
);

