using AccountingInventory.Domain.Entities;

namespace AccountingInventory.Application.Sales.Invoices;

public sealed record SalesInvoiceSummaryDto(
    Guid Id,
    string BillNumber,
    Guid CustomerId,
    string CustomerName,
    string CustomerMobile,
    string CustomerAddress,
    string? CustomerEmail,
    DateOnly InvoiceDate,
    int PaymentTermsDays,
    DateOnly DueDate,
    int ItemCount,
    decimal SubTotal,
    decimal Discount,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalAmount,
    decimal AmountPaid,
    decimal Balance,
    string PaymentStatus,
    string? Notes,
    GstSupplyType SupplyType = GstSupplyType.IntraState,
    string? PlaceOfSupplyStateCode = null,
    string? PlaceOfSupplyStateName = null,
    string? CustomerGstin = null);

public sealed record SalesInvoiceLineDto(
    Guid Id,
    int LineNumber,
    InvoiceItemType ItemType,
    Guid? ProductId,
    string ItemDescription,
    string? SerialNumber,
    string? SerialNumber1,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    Guid? TaxId,
    decimal CgstRate,
    decimal SgstRate,
    decimal IgstRate,
    decimal TaxableAmount,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalAmount, string? HsnSac = null, string UnitOfMeasure = "NOS", Guid? SkuId = null);

public sealed record SalesInvoiceReceiptDto(
    Guid Id,
    decimal Amount,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber,
    string? Note);

public sealed record SalesInvoiceDetailsDto(
    SalesInvoiceSummaryDto Invoice,
    IReadOnlyList<SalesInvoiceLineDto> Lines,
    IReadOnlyList<SalesInvoiceReceiptDto> Payments);

public sealed record CreateSalesInvoiceLineCommandDto(
    InvoiceItemType ItemType,
    Guid? ProductId,
    string ItemDescription,
    string? SerialNumber,
    string? SerialNumber1,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount = 0m,
    Guid? TaxId = null,
    decimal CgstRate = 0m,
    decimal SgstRate = 0m,
    decimal IgstRate = 0m, string? HsnSac = null, string UnitOfMeasure = "NOS", Guid? SkuId = null);

public sealed record CreateSalesInvoiceInitialPaymentDto(
    decimal Amount,
    string PaymentMode,
    DateOnly PaymentDate,
    string? ReferenceNumber = null,
    string? Note = null);

