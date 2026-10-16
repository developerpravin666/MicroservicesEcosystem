using MediatR;

namespace AccountingInventory.Application.Inventory.Godowns.Queries.GetGodownStock;

public sealed record GodownStockItemDto(
    Guid ItemId,
    string ItemType, // "Serialized" or "Sku"
    string Name,
    string? SerialNumber,
    string? SerialNumber1,
    decimal AvailableQuantity,
    decimal UnitCost);

public sealed record GetGodownStockQuery(Guid GodownId) : IRequest<IReadOnlyList<GodownStockItemDto>>;

