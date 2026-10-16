using MediatR;

namespace AccountingInventory.Application.Inventory.Adjustments.Queries.GetStockAdjustments;

public sealed record StockAdjustmentDto(
    Guid Id,
    string AdjustmentNumber,
    DateOnly AdjustmentDate,
    Guid GodownId,
    string GodownName,
    string AdjustmentType,
    string Reason,
    string ItemType,
    string ItemName,
    string? SerialNumber,
    decimal BookQuantity,
    decimal PhysicalQuantity,
    decimal DiscrepancyQuantity,
    decimal UnitCost,
    decimal TotalValue,
    Guid? JournalEntryId);

public sealed record GetStockAdjustmentsQuery(
    Guid? GodownId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<IReadOnlyList<StockAdjustmentDto>>;

