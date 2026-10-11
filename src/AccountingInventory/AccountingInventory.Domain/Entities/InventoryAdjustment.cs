using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class InventoryAdjustment : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public DateOnly AdjustmentDate { get; private set; }
    public string Reason { get; private set; } = null!;
    public decimal Cost { get; private set; }

    public static InventoryAdjustment WriteOff(Guid productId, DateOnly date, string reason, decimal cost)
    {
        if (productId == Guid.Empty || date == default || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Product, date, and reason are required.");
        if (cost <= 0) throw new ArgumentOutOfRangeException(nameof(cost), "Write-off cost must be greater than zero.");
        return new InventoryAdjustment
        {
            Id = Guid.NewGuid(), IsActive = true, ProductId = productId, AdjustmentDate = date,
            Reason = reason.Trim(), Cost = decimal.Round(cost, 2, MidpointRounding.AwayFromZero)
        };
    }

    private InventoryAdjustment() { }
}
