using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetStockValuationSummary;

public sealed class GetStockValuationSummaryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockValuationSummaryQuery, StockValuationSummaryDto>
{
    public async Task<StockValuationSummaryDto> Handle(GetStockValuationSummaryQuery request, CancellationToken ct)
    {
        var asOfDate = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var search = request.Search?.Trim().ToLowerInvariant();
        var selectedMethod = string.IsNullOrWhiteSpace(request.Method) ? "FIFO" : request.Method.Trim().ToUpperInvariant();

        var resultItems = new List<StockValuationItemDto>();

        // 1. Process SKU Inventory
        var skuQuery = db.StockSkus.AsNoTracking().Where(s => s.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
        {
            skuQuery = skuQuery.Where(s => s.Code.ToLower().Contains(search) || s.Name.ToLower().Contains(search));
        }

        var skus = await skuQuery.ToListAsync(ct);
        var skuIds = skus.Select(s => s.Id).ToList();

        var skuMovements = await db.SkuMovements
            .AsNoTracking()
            .Where(m => skuIds.Contains(m.SkuId) && m.MovementDate <= asOfDate)
            .OrderBy(m => m.MovementDate)
            .ThenBy(m => m.CreatedAt)
            .ToListAsync(ct);

        var movementsBySku = skuMovements.GroupBy(m => m.SkuId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var sku in skus)
        {
            movementsBySku.TryGetValue(sku.Id, out var movements);
            movements ??= [];

            var inwardMovements = movements.Where(m => m.Quantity > 0).ToList();
            var outwardMovements = movements.Where(m => m.Quantity < 0).ToList();

            var totalInwardQty = inwardMovements.Sum(m => m.Quantity);
            var totalOutwardQty = outwardMovements.Sum(m => Math.Abs(m.Quantity));

            var closingQty = totalInwardQty - totalOutwardQty;
            if (inwardMovements.Count == 0 && movements.Count == 0)
            {
                // Unmoved stock SKU with initial quantity
                closingQty = sku.Quantity;
            }

            if (closingQty <= 0)
            {
                continue;
            }

            // A) FIFO Layer Depletion
            var unconsumedLots = new List<StockValuationLotDto>();
            var cumulativeIssued = totalOutwardQty;
            decimal fifoTotalValue = 0m;

            foreach (var lot in inwardMovements)
            {
                var lotQty = lot.Quantity;
                var lotUnitCost = lotQty > 0 ? decimal.Round(lot.InventoryValue / lotQty, 4, MidpointRounding.AwayFromZero) : 0m;

                if (cumulativeIssued >= lotQty)
                {
                    // Fully consumed by past sales/issues
                    cumulativeIssued -= lotQty;
                }
                else
                {
                    // Partially or fully available in closing stock
                    var availableQty = lotQty - cumulativeIssued;
                    cumulativeIssued = 0m;

                    var lotValue = decimal.Round(availableQty * lotUnitCost, 2, MidpointRounding.AwayFromZero);
                    fifoTotalValue += lotValue;

                    unconsumedLots.Add(new StockValuationLotDto(
                        lot.MovementDate,
                        lot.BillNumber ?? lot.Kind,
                        lotQty,
                        availableQty,
                        lotUnitCost,
                        lotValue));
                }
            }

            // Fallback for SKU with opening balance before movements tracked
            if (fifoTotalValue == 0m && closingQty > 0)
            {
                var defaultCost = sku.Quantity > 0 ? decimal.Round(sku.InventoryValue / sku.Quantity, 4, MidpointRounding.AwayFromZero) : 0m;
                fifoTotalValue = decimal.Round(closingQty * defaultCost, 2, MidpointRounding.AwayFromZero);
                unconsumedLots.Add(new StockValuationLotDto(
                    sku.LastMovementDate ?? asOfDate,
                    "Opening Balance",
                    closingQty,
                    closingQty,
                    defaultCost,
                    fifoTotalValue));
            }

            var fifoRate = closingQty > 0 ? decimal.Round(fifoTotalValue / closingQty, 2, MidpointRounding.AwayFromZero) : 0m;

            // B) Weighted Average Rate
            decimal weightedAvgRate = 0m;
            decimal weightedAvgValue = 0m;
            var totalInwardCost = inwardMovements.Sum(m => m.InventoryValue);
            if (totalInwardQty > 0)
            {
                weightedAvgRate = decimal.Round(totalInwardCost / totalInwardQty, 4, MidpointRounding.AwayFromZero);
                weightedAvgValue = decimal.Round(closingQty * weightedAvgRate, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                weightedAvgRate = fifoRate;
                weightedAvgValue = fifoTotalValue;
            }

            // C) Last Purchase Cost
            decimal lastPurchaseRate = 0m;
            var latestInward = inwardMovements.LastOrDefault();
            if (latestInward != null && latestInward.Quantity > 0)
            {
                lastPurchaseRate = decimal.Round(latestInward.InventoryValue / latestInward.Quantity, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                lastPurchaseRate = fifoRate;
            }
            var lastPurchaseValue = decimal.Round(closingQty * lastPurchaseRate, 2, MidpointRounding.AwayFromZero);

            // D) Alternate Unit Calculation
            decimal? altQty = null;
            if (!string.IsNullOrWhiteSpace(sku.AlternateUnit) && sku.ConversionFactor.HasValue && sku.ConversionFactor.Value > 0)
            {
                altQty = decimal.Round(closingQty / sku.ConversionFactor.Value, 2, MidpointRounding.AwayFromZero);
            }

            resultItems.Add(new StockValuationItemDto(
                sku.Id,
                sku.Code,
                sku.Name,
                "SKU",
                sku.UnitOfMeasure,
                sku.AlternateUnit,
                sku.ConversionFactor,
                closingQty,
                altQty,
                fifoRate,
                fifoTotalValue,
                decimal.Round(weightedAvgRate, 2, MidpointRounding.AwayFromZero),
                weightedAvgValue,
                lastPurchaseRate,
                lastPurchaseValue,
                decimal.Round(fifoTotalValue - weightedAvgValue, 2, MidpointRounding.AwayFromZero),
                unconsumedLots));
        }

        // 2. Process Serialized Products
        var productQuery = db.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.PurchaseDate <= asOfDate);

        if (request.GodownId.HasValue)
        {
            productQuery = productQuery.Where(p => p.GodownId == request.GodownId.Value);
        }

        var products = await productQuery.ToListAsync(ct);

        // Filter unsold as of date (either not sold, or sold after asOfDate)
        var activeSerialized = products
            .Where(p => !p.IsSold)
            .ToList();

        if (activeSerialized.Count > 0)
        {
            var productModels = await db.ProductModels.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
            var productBrands = await db.Brands.AsNoTracking().ToDictionaryAsync(b => b.Id, ct);

            var groupedProducts = activeSerialized.GroupBy(p => p.ProductModelId);

            foreach (var group in groupedProducts)
            {
                productModels.TryGetValue(group.Key, out var model);
                var brandName = model != null && productBrands.TryGetValue(model.BrandId, out var brand) ? brand.Name : "General";
                var modelName = model?.Name ?? "Serialized Device";
                var displayName = $"{brandName} - {modelName}";

                if (!string.IsNullOrWhiteSpace(search) && !displayName.ToLower().Contains(search))
                {
                    continue;
                }

                var orderedItems = group.OrderBy(p => p.PurchaseDate).ThenBy(p => p.CreatedAt).ToList();
                var count = orderedItems.Count;
                var totalCost = orderedItems.Sum(p => p.PurchasePrice - p.Discount);

                var avgCost = count > 0 ? decimal.Round(totalCost / count, 2, MidpointRounding.AwayFromZero) : 0m;
                var latestCost = orderedItems.Last().PurchasePrice - orderedItems.Last().Discount;

                var lots = orderedItems.Select(p => new StockValuationLotDto(
                    p.PurchaseDate,
                    string.IsNullOrWhiteSpace(p.SerialNumber) ? p.Id.ToString() : p.SerialNumber,
                    1m,
                    1m,
                    p.PurchasePrice - p.Discount,
                    p.PurchasePrice - p.Discount
                )).ToList();

                resultItems.Add(new StockValuationItemDto(
                    group.Key,
                    model?.Code ?? "MODEL",
                    displayName,
                    "Serialized",
                    "NOS",
                    null,
                    null,
                    count,
                    null,
                    avgCost, // Individual lots carry exact historical acquisition cost
                    totalCost,
                    avgCost,
                    totalCost,
                    latestCost,
                    decimal.Round(count * latestCost, 2, MidpointRounding.AwayFromZero),
                    decimal.Round(totalCost - (count * latestCost), 2, MidpointRounding.AwayFromZero),
                    lots
                ));
            }
        }

        // Summary Aggregations
        var totalQty = resultItems.Sum(i => i.Quantity);
        var totalFifo = decimal.Round(resultItems.Sum(i => i.FifoValue), 2, MidpointRounding.AwayFromZero);
        var totalWeightedAvg = decimal.Round(resultItems.Sum(i => i.WeightedAverageValue), 2, MidpointRounding.AwayFromZero);
        var totalLastPurchase = decimal.Round(resultItems.Sum(i => i.LastPurchaseValue), 2, MidpointRounding.AwayFromZero);

        var selectedValuation = selectedMethod switch
        {
            "WEIGHTEDAVERAGE" => totalWeightedAvg,
            "LASTPURCHASECOST" => totalLastPurchase,
            _ => totalFifo
        };

        return new StockValuationSummaryDto(
            resultItems.Count,
            totalQty,
            totalFifo,
            totalWeightedAvg,
            totalLastPurchase,
            selectedMethod,
            selectedValuation,
            decimal.Round(totalFifo - totalWeightedAvg, 2, MidpointRounding.AwayFromZero),
            asOfDate,
            resultItems.OrderBy(i => i.Category).ThenBy(i => i.ItemName).ToList());
    }
}
