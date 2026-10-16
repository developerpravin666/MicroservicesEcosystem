namespace AccountingInventory.Application.Compliance.EWayBills.Models;

public sealed record EWayBillDto(
    Guid Id,
    Guid SalesInvoiceId,
    string BillNumber,
    DateOnly InvoiceDate,
    string CustomerName,
    string? CustomerGstin,
    decimal TotalAmount,
    string? EwbNumber,
    DateTimeOffset? EwbDate,
    DateTimeOffset? ValidUpto,
    string DocType,
    string SupplyType,
    string SubSupplyType,
    int TransMode,
    int DistanceKm,
    string? TransporterId,
    string? TransporterName,
    string? TransDocNo,
    DateOnly? TransDocDate,
    string? VehicleNo,
    string VehicleType,
    string Status,
    string? CancelReason,
    DateTimeOffset? CancelDate);

