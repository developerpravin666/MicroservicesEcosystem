using MediatR;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetTradingProfitLossValuation;

public sealed record ValuationCogsMetricDto(
    string MethodName,
    decimal OpeningStock,
    decimal Purchases,
    decimal ClosingStock,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal GrossProfitMarginPercent);

public sealed record TradingProfitLossValuationDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal SalesRevenue,
    decimal DirectExpenses,
    ValuationCogsMetricDto Fifo,
    ValuationCogsMetricDto WeightedAverage,
    ValuationCogsMetricDto LastPurchaseCost,
    decimal ValuationVariance,
    string SummaryNotes);

public sealed record GetTradingProfitLossValuationQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<TradingProfitLossValuationDto>;

