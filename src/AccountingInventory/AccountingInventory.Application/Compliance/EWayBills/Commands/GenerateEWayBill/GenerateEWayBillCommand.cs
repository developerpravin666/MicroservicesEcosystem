using MediatR;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.GenerateEWayBill;

public sealed record GenerateEWayBillCommand(
    Guid SalesInvoiceId,
    int DistanceKm,
    int TransMode = 1, // 1 = Road, 2 = Rail, 3 = Air, 4 = Ship
    string? VehicleNo = null,
    string VehicleType = "R", // R = Regular, O = Over Dimensional Cargo
    string? TransporterId = null,
    string? TransporterName = null,
    string? TransDocNo = null,
    DateOnly? TransDocDate = null,
    string DocType = "INV",
    string SupplyType = "O",
    string SubSupplyType = "1",
    string? CustomEwbNumber = null) : IRequest<Guid>;

