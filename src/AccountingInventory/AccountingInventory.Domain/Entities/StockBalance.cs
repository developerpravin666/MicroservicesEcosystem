using BuildingBlocks.Domain;

namespace AccountingInventory.Domain.Entities;

public sealed class StockBalance : AggregateRoot
{
    public Guid GodownId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? SkuId { get; private set; }
    public string ItemType { get; private set; } = null!; // "Serialized" or "Sku"
    public string? SerialNumber { get; private set; }
    public decimal Quantity { get; private set; }
    public DateOnly LastMovementDate { get; private set; }

    public static StockBalance CreateSerialized(Guid godownId, Guid productId, string serialNumber, DateOnly date)
    {
        if (godownId == Guid.Empty) throw new ArgumentException("Godown ID is required.");
        if (productId == Guid.Empty) throw new ArgumentException("Product ID is required.");
        return new StockBalance
        {
            Id = Guid.NewGuid(),
            GodownId = godownId,
            ProductId = productId,
            SkuId = null,
            ItemType = "Serialized",
            SerialNumber = serialNumber.Trim(),
            Quantity = 1m,
            LastMovementDate = date,
            IsActive = true
        };
    }

    public static StockBalance CreateSku(Guid godownId, Guid skuId, decimal quantity, DateOnly date)
    {
        if (godownId == Guid.Empty) throw new ArgumentException("Godown ID is required.");
        if (skuId == Guid.Empty) throw new ArgumentException("SKU ID is required.");
        return new StockBalance
        {
            Id = Guid.NewGuid(),
            GodownId = godownId,
            ProductId = null,
            SkuId = skuId,
            ItemType = "Sku",
            SerialNumber = null,
            Quantity = quantity,
            LastMovementDate = date,
            IsActive = true
        };
    }

    public void AddQuantity(decimal qty, DateOnly date)
    {
        Quantity += qty;
        LastMovementDate = date;
    }

    public void DeductQuantity(decimal qty, DateOnly date)
    {
        if (qty > Quantity) throw new InvalidOperationException("Insufficient stock in godown.");
        Quantity -= qty;
        LastMovementDate = date;
    }

    public void UpdateGodown(Guid newGodownId, DateOnly date)
    {
        GodownId = newGodownId;
        LastMovementDate = date;
    }

    public void SetQuantity(decimal qty, DateOnly date)
    {
        Quantity = qty;
        LastMovementDate = date;
    }

    private StockBalance() { }
}

