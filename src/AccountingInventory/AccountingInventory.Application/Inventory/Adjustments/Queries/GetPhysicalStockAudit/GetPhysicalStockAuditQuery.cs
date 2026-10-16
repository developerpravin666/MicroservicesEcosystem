using MediatR;

namespace AccountingInventory.Application.Inventory.Adjustments.Queries.GetPhysicalStockAudit;

public sealed record StockAuditItemDto(
    Guid? ProductId,
    Guid? SkuId,
    string ItemType, // "Serialized" or "Sku"
    string ItemName,
    string? SerialNumber,
    string? SerialNumber1,
    decimal BookQuantity,
    decimal UnitCost,
    decimal TotalBookValue);

public sealed record PhysicalStockAuditSummaryDto(
    Guid GodownId,
    string GodownName,
    string GodownCode,
    bool IsPrimary,
    int TotalItemsCount,
    decimal TotalBookQuantity,
    decimal TotalBookValue,
    IReadOnlyList<StockAuditItemDto> Items);

public sealed record GetPhysicalStockAuditQuery(Guid GodownId) : IRequest<PhysicalStockAuditSummaryDto>;

