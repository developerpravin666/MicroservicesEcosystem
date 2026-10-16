using MediatR;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CreateRcmSelfInvoice;

public sealed record CreateRcmSelfInvoiceCommand(
    DateOnly VoucherDate,
    Guid VendorId,
    string Section,
    string ServiceCategory,
    string? SupplierInvoiceNumber,
    DateOnly? SupplierInvoiceDate,
    int SupplyType,
    string PlaceOfSupplyStateCode,
    string PlaceOfSupplyStateName,
    string HsnSacCode,
    string ItemDescription,
    decimal TaxableAmount,
    decimal GstRate,
    bool IsItcEligible = true,
    string PaymentMode = "Bank",
    string? PaymentReference = null,
    Guid? BankAccountId = null,
    Guid? ExpenseAccountId = null,
    string? Notes = null,
    string? CustomVoucherNumber = null) : IRequest<Guid>;

