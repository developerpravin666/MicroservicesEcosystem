using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class StockTransferJournal : AggregateRoot
{
    public string TransferNumber { get; private set; } = null!;
    public DateOnly TransferDate { get; private set; }
    public Guid FromGodownId { get; private set; }
    public Guid ToGodownId { get; private set; }
    public string? VehicleNumber { get; private set; }
    public string? DispatchNote { get; private set; }
    public string? TransporterName { get; private set; }
    public string? Narration { get; private set; }
    public string Status { get; private set; } = "Completed";
    public decimal TotalQuantity { get; private set; }
    public decimal TotalValue { get; private set; }

    private readonly List<StockTransferLine> _lines = new();
    public IReadOnlyCollection<StockTransferLine> Lines => _lines.AsReadOnly();

    public static StockTransferJournal Create(
        string transferNumber,
        DateOnly transferDate,
        Guid fromGodownId,
        Guid toGodownId,
        string? vehicleNumber,
        string? dispatchNote,
        string? transporterName,
        string? narration,
        IEnumerable<StockTransferLine> lines)
    {
        if (string.IsNullOrWhiteSpace(transferNumber)) throw new ArgumentException("Transfer number is required.");
        if (fromGodownId == Guid.Empty || toGodownId == Guid.Empty) throw new ArgumentException("Source and destination godowns are required.");
        if (fromGodownId == toGodownId) throw new ArgumentException("Source and destination godowns cannot be identical.");

        var lineList = lines.ToList();
        if (lineList.Count == 0) throw new ArgumentException("Transfer must contain at least one line item.");

        var totalQty = lineList.Sum(l => l.Quantity);
        var totalVal = lineList.Sum(l => l.TotalValue);

        var journal = new StockTransferJournal
        {
            Id = Guid.NewGuid(),
            TransferNumber = transferNumber.Trim(),
            TransferDate = transferDate,
            FromGodownId = fromGodownId,
            ToGodownId = toGodownId,
            VehicleNumber = string.IsNullOrWhiteSpace(vehicleNumber) ? null : vehicleNumber.Trim(),
            DispatchNote = string.IsNullOrWhiteSpace(dispatchNote) ? null : dispatchNote.Trim(),
            TransporterName = string.IsNullOrWhiteSpace(transporterName) ? null : transporterName.Trim(),
            Narration = string.IsNullOrWhiteSpace(narration) ? null : narration.Trim(),
            Status = "Completed",
            TotalQuantity = totalQty,
            TotalValue = totalVal,
            IsActive = true
        };

        foreach (var l in lineList)
        {
            l.SetJournalId(journal.Id);
            journal._lines.Add(l);
        }

        return journal;
    }

    private StockTransferJournal() { }
}

public sealed class StockTransferLine : AggregateRoot
{
    public Guid StockTransferJournalId { get; private set; }
    public string ItemType { get; private set; } = null!; // "Serialized" or "Sku"
    public Guid? ProductId { get; private set; }
    public Guid? SkuId { get; private set; }
    public string ItemName { get; private set; } = null!;
    public string? SerialNumber { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalValue { get; private set; }

    public static StockTransferLine Create(
        string itemType,
        Guid? productId,
        Guid? skuId,
        string itemName,
        string? serialNumber,
        decimal quantity,
        decimal unitCost)
    {
        if (string.IsNullOrWhiteSpace(itemName)) throw new ArgumentException("Item name is required.");
        if (quantity <= 0) throw new ArgumentException("Quantity must be greater than zero.");
        return new StockTransferLine
        {
            Id = Guid.NewGuid(),
            ItemType = itemType,
            ProductId = productId,
            SkuId = skuId,
            ItemName = itemName.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            Quantity = quantity,
            UnitCost = unitCost,
            TotalValue = decimal.Round(quantity * unitCost, 2, MidpointRounding.AwayFromZero),
            IsActive = true
        };
    }

    public void SetJournalId(Guid journalId) => StockTransferJournalId = journalId;

    private StockTransferLine() { }
}

