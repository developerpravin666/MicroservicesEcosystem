using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Inventory.Valuation.Queries.GetStockValuationSummary;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetTradingProfitLossValuation;

public sealed class GetTradingProfitLossValuationQueryHandler(
    IAccountingInventoryDbContext db,
    ISender sender)
    : IRequestHandler<GetTradingProfitLossValuationQuery, TradingProfitLossValuationDto>
{
    public async Task<TradingProfitLossValuationDto> Handle(GetTradingProfitLossValuationQuery request, CancellationToken ct)
    {
        var toDate = request.ToDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = request.FromDate ?? new DateOnly(toDate.Year, toDate.Month, 1);

        // 1. Compute Closing Valuation & Opening Valuation using the Valuation Engine
        var closingValuation = await sender.Send(new GetStockValuationSummaryQuery("ALL", toDate), ct);
        var openingDate = fromDate.AddDays(-1);
        var openingValuation = await sender.Send(new GetStockValuationSummaryQuery("ALL", openingDate), ct);

        // 2. Compute Net Sales Revenue in the period
        var salesRevenue = await db.SalesInvoices
            .AsNoTracking()
            .Where(i => !i.IsCancelled && i.InvoiceDate >= fromDate && i.InvoiceDate <= toDate)
            .SumAsync(i => (decimal?)i.TaxableAmount, ct) ?? 0m;

        // 3. Compute Inward Purchases in the period
        var skuPurchases = await db.SkuMovements
            .AsNoTracking()
            .Where(m => m.Kind == "Purchase" && m.MovementDate >= fromDate && m.MovementDate <= toDate)
            .SumAsync(m => (decimal?)m.InventoryValue, ct) ?? 0m;

        var productPurchases = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.PurchaseDate >= fromDate && p.PurchaseDate <= toDate)
            .SumAsync(p => (decimal?)(p.PurchasePrice - p.Discount), ct) ?? 0m;

        var totalPurchases = decimal.Round(skuPurchases + productPurchases, 2, MidpointRounding.AwayFromZero);
        salesRevenue = decimal.Round(salesRevenue, 2, MidpointRounding.AwayFromZero);

        // 4. Build Comparative COGS & Gross Profit Metrics
        var fifoCogs = Math.Max(0m, decimal.Round(openingValuation.FifoValuation + totalPurchases - closingValuation.FifoValuation, 2, MidpointRounding.AwayFromZero));
        var fifoGp = decimal.Round(salesRevenue - fifoCogs, 2, MidpointRounding.AwayFromZero);
        var fifoMargin = salesRevenue > 0 ? decimal.Round((fifoGp / salesRevenue) * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

        var fifoMetric = new ValuationCogsMetricDto(
            "FIFO (First In, First Out)",
            openingValuation.FifoValuation,
            totalPurchases,
            closingValuation.FifoValuation,
            fifoCogs,
            fifoGp,
            fifoMargin);

        var wtAvgCogs = Math.Max(0m, decimal.Round(openingValuation.WeightedAverageValuation + totalPurchases - closingValuation.WeightedAverageValuation, 2, MidpointRounding.AwayFromZero));
        var wtAvgGp = decimal.Round(salesRevenue - wtAvgCogs, 2, MidpointRounding.AwayFromZero);
        var wtAvgMargin = salesRevenue > 0 ? decimal.Round((wtAvgGp / salesRevenue) * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

        var wtAvgMetric = new ValuationCogsMetricDto(
            "Monthly / Periodic Weighted Average",
            openingValuation.WeightedAverageValuation,
            totalPurchases,
            closingValuation.WeightedAverageValuation,
            wtAvgCogs,
            wtAvgGp,
            wtAvgMargin);

        var lastCostCogs = Math.Max(0m, decimal.Round(openingValuation.LastPurchaseValuation + totalPurchases - closingValuation.LastPurchaseValuation, 2, MidpointRounding.AwayFromZero));
        var lastCostGp = decimal.Round(salesRevenue - lastCostCogs, 2, MidpointRounding.AwayFromZero);
        var lastCostMargin = salesRevenue > 0 ? decimal.Round((lastCostGp / salesRevenue) * 100m, 2, MidpointRounding.AwayFromZero) : 0m;

        var lastCostMetric = new ValuationCogsMetricDto(
            "Last Purchase Cost",
            openingValuation.LastPurchaseValuation,
            totalPurchases,
            closingValuation.LastPurchaseValuation,
            lastCostCogs,
            lastCostGp,
            lastCostMargin);

        var variance = decimal.Round(closingValuation.FifoValuation - closingValuation.WeightedAverageValuation, 2, MidpointRounding.AwayFromZero);

        var notes = variance >= 0
            ? $"FIFO yields a closing valuation ₹{Math.Abs(variance):N2} higher than Weighted Average, resulting in a lower Cost of Goods Sold (COGS) and higher reported Gross Profit."
            : $"Weighted Average yields a closing valuation ₹{Math.Abs(variance):N2} higher than FIFO, dampening short-term purchase price volatility on Gross Profit.";

        return new TradingProfitLossValuationDto(
            fromDate,
            toDate,
            salesRevenue,
            0m,
            fifoMetric,
            wtAvgMetric,
            lastCostMetric,
            variance,
            notes);
    }
}

