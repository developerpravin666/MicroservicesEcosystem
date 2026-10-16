using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class EWayBill : AggregateRoot
{
    public Guid SalesInvoiceId { get; private set; }
    public string? EwbNumber { get; private set; }
    public DateTimeOffset? EwbDate { get; private set; }
    public DateTimeOffset? ValidUpto { get; private set; }
    public string DocType { get; private set; } = "INV"; // INV, BIL, CHL
    public string SupplyType { get; private set; } = "O"; // O = Outward, I = Inward
    public string SubSupplyType { get; private set; } = "1"; // 1 = Supply, 2 = Export, 3 = Job Work
    public int TransMode { get; private set; } = 1; // 1 = Road, 2 = Rail, 3 = Air, 4 = Ship
    public int DistanceKm { get; private set; }
    public string? TransporterId { get; private set; }
    public string? TransporterName { get; private set; }
    public string? TransDocNo { get; private set; }
    public DateOnly? TransDocDate { get; private set; }
    public string? VehicleNo { get; private set; }
    public string VehicleType { get; private set; } = "R"; // R = Regular, O = Over Dimensional Cargo
    public string Status { get; private set; } = "Generated"; // Generated, Cancelled
    public string? CancelReason { get; private set; }
    public DateTimeOffset? CancelDate { get; private set; }
    public string PayloadJson { get; private set; } = null!;

    public static EWayBill Create(
        Guid salesInvoiceId,
        int distanceKm,
        int transMode = 1,
        string? vehicleNo = null,
        string vehicleType = "R",
        string? transporterId = null,
        string? transporterName = null,
        string? transDocNo = null,
        DateOnly? transDocDate = null,
        string docType = "INV",
        string supplyType = "O",
        string subSupplyType = "1",
        string? ewbNumber = null,
        DateTimeOffset? ewbDate = null,
        DateTimeOffset? validUpto = null,
        string payloadJson = "")
    {
        if (salesInvoiceId == Guid.Empty) throw new ArgumentException("Sales invoice ID is required.");
        if (distanceKm <= 0) throw new ArgumentException("Transport distance in KM must be greater than zero.");
        if (transMode == 1 && string.IsNullOrWhiteSpace(vehicleNo) && string.IsNullOrWhiteSpace(transporterId))
            throw new ArgumentException("Road transport requires either Vehicle Number or Transporter ID.");

        var now = DateTimeOffset.UtcNow;
        var generatedEwb = !string.IsNullOrWhiteSpace(ewbNumber)
            ? ewbNumber.Trim()
            : $"{DateTime.UtcNow:yyMMdd}{new Random().Next(100000, 999999)}";

        // NIC validity rule: 1 day per 200 km for regular cargo (minimum 1 day)
        var validityDays = Math.Max(1, (int)Math.Ceiling(distanceKm / 200.0));
        var validUntil = validUpto ?? now.AddDays(validityDays);

        return new EWayBill
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            SalesInvoiceId = salesInvoiceId,
            EwbNumber = generatedEwb,
            EwbDate = ewbDate ?? now,
            ValidUpto = validUntil,
            DocType = docType.Trim().ToUpperInvariant(),
            SupplyType = supplyType.Trim().ToUpperInvariant(),
            SubSupplyType = subSupplyType.Trim(),
            TransMode = transMode,
            DistanceKm = distanceKm,
            TransporterId = string.IsNullOrWhiteSpace(transporterId) ? null : transporterId.Trim().ToUpperInvariant(),
            TransporterName = string.IsNullOrWhiteSpace(transporterName) ? null : transporterName.Trim(),
            TransDocNo = string.IsNullOrWhiteSpace(transDocNo) ? null : transDocNo.Trim(),
            TransDocDate = transDocDate,
            VehicleNo = string.IsNullOrWhiteSpace(vehicleNo) ? null : vehicleNo.Trim().ToUpperInvariant().Replace(" ", ""),
            VehicleType = vehicleType.Trim().ToUpperInvariant(),
            Status = "Generated",
            PayloadJson = payloadJson
        };
    }

    public void SetPayloadJson(string json)
    {
        PayloadJson = json;
    }

    public void Cancel(string reason)
    {
        if (Status == "Cancelled") throw new InvalidOperationException("E-Way Bill is already cancelled.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Cancellation reason is required.");

        Status = "Cancelled";
        CancelReason = reason.Trim();
        CancelDate = DateTimeOffset.UtcNow;
    }

    private EWayBill() { }
}

