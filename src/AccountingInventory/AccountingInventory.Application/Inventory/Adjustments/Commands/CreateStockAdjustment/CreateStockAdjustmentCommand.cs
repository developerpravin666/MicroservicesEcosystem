using MediatR;

namespace AccountingInventory.Application.Inventory.Adjustments.Commands.CreateStockAdjustment;

public sealed record CreateStockAdjustmentCommand(
    DateOnly AdjustmentDate,
    Guid GodownId,
    string AdjustmentType, // "Shortage" or "Surplus"
    string Reason,
    string ItemType, // "Serialized" or "Sku"
    Guid? ProductId,
    Guid? SkuId,
    string ItemName,
    string? SerialNumber,
    decimal BookQuantity,
    decimal PhysicalQuantity,
    decimal UnitCost) : IRequest<Guid>;

