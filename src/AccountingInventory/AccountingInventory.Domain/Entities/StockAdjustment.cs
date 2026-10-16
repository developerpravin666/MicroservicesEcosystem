using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class StockAdjustment : AggregateRoot
{
    public string AdjustmentNumber { get; private set; } = null!;
    public DateOnly AdjustmentDate { get; private set; }
    public Guid GodownId { get; private set; }
    public string AdjustmentType { get; private set; } = null!; // "Shortage" (Shrinkage/Damage Write-Off) or "Surplus" (Excess Found)
    public string Reason { get; private set; } = null!;
    public string ItemType { get; private set; } = null!; // "Serialized" or "Sku"
    public Guid? ProductId { get; private set; }
    public Guid? SkuId { get; private set; }
    public string ItemName { get; private set; } = null!;
    public string? SerialNumber { get; private set; }
    public decimal BookQuantity { get; private set; }
    public decimal PhysicalQuantity { get; private set; }
    public decimal DiscrepancyQuantity { get; private set; }
    public decimal UnitCost { get; private set; }
    public decimal TotalValue { get; private set; }
    public Guid? JournalEntryId { get; private set; }

    public static StockAdjustment Create(
        string adjustmentNumber,
        DateOnly adjustmentDate,
        Guid godownId,
        string adjustmentType,
        string reason,
        string itemType,
        Guid? productId,
        Guid? skuId,
        string itemName,
        string? serialNumber,
        decimal bookQuantity,
        decimal physicalQuantity,
        decimal unitCost,
        Guid? journalEntryId = null)
    {
        if (string.IsNullOrWhiteSpace(adjustmentNumber)) throw new ArgumentException("Adjustment number is required.");
        if (godownId == Guid.Empty) throw new ArgumentException("Godown is required.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.");
        if (adjustmentType != "Shortage" && adjustmentType != "Surplus")
            throw new ArgumentException("Adjustment type must be Shortage or Surplus.");

        var discrepancy = physicalQuantity - bookQuantity;
        var totalVal = decimal.Round(Math.Abs(discrepancy) * unitCost, 2, MidpointRounding.AwayFromZero);

        return new StockAdjustment
        {
            Id = Guid.NewGuid(),
            AdjustmentNumber = adjustmentNumber.Trim(),
            AdjustmentDate = adjustmentDate,
            GodownId = godownId,
            AdjustmentType = adjustmentType,
            Reason = reason.Trim(),
            ItemType = itemType,
            ProductId = productId,
            SkuId = skuId,
            ItemName = itemName.Trim(),
            SerialNumber = string.IsNullOrWhiteSpace(serialNumber) ? null : serialNumber.Trim(),
            BookQuantity = bookQuantity,
            PhysicalQuantity = physicalQuantity,
            DiscrepancyQuantity = discrepancy,
            UnitCost = unitCost,
            TotalValue = totalVal,
            JournalEntryId = journalEntryId,
            IsActive = true
        };
    }

    private StockAdjustment() { }
}

