using MediatR;

namespace AccountingInventory.Application.Purchases.Accounting;

public sealed record RecordPurchasePaymentCommand(
    Guid VendorId,
    string BillNumber,
    decimal Amount,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Note) : IRequest<PurchasePaymentSummary>;
