using MediatR;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetStockValuationSummary;

public sealed record StockValuationLotDto(
    DateOnly Date,
    string Reference,
    decimal OriginalQuantity,
    decimal RemainingQuantity,
    decimal UnitCost,
    decimal ValuationValue);

public sealed record StockValuationItemDto(
    Guid ItemId,
    string ItemCode,
    string ItemName,
    string Category,
    string BaseUnit,
    string? AlternateUnit,
    decimal? ConversionFactor,
    decimal Quantity,
    decimal? AlternateQuantity,
    decimal FifoRate,
    decimal FifoValue,
    decimal WeightedAverageRate,
    decimal WeightedAverageValue,
    decimal LastPurchaseRate,
    decimal LastPurchaseValue,
    decimal Variance,
    IReadOnlyList<StockValuationLotDto> Lots);

public sealed record StockValuationSummaryDto(
    int TotalItems,
    decimal TotalQuantity,
    decimal FifoValuation,
    decimal WeightedAverageValuation,
    decimal LastPurchaseValuation,
    string SelectedMethod,
    decimal SelectedTotalValuation,
    decimal ValuationVariance,
    DateOnly AsOfDate,
    IReadOnlyList<StockValuationItemDto> Items);

public sealed record GetStockValuationSummaryQuery(
    string Method = "FIFO",
    DateOnly? AsOfDate = null,
    Guid? GodownId = null,
    string? Search = null) : IRequest<StockValuationSummaryDto>;

