namespace AccountingInventory.Application.Compliance.EInvoices.Models;

public sealed record EInvoiceDto(
    Guid Id,
    Guid SalesInvoiceId,
    string BillNumber,
    DateOnly InvoiceDate,
    string CustomerName,
    string? CustomerGstin,
    decimal TotalAmount,
    string Irn,
    string AckNo,
    DateTimeOffset AckDate,
    string SignedQrCode,
    string? SignedInvoice,
    string Status,
    string? CancelReason,
    DateTimeOffset? CancelDate);

