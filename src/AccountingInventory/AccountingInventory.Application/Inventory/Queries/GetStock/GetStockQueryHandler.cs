using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Queries.GetStock;

public sealed class GetStockQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockQuery, PagedResult<StockGroupSummary>>
{
    public async Task<PagedResult<StockGroupSummary>> Handle(
        GetStockQuery request,
        CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking()
            .Where(product => product.IsActive && !product.IsSold);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            products = products.Where(product =>
                db.Brands.Any(item => item.Id == product.BrandId && item.Name.ToLower().Contains(search))
                || db.ProductModels.Any(item => item.Id == product.ProductModelId && item.Name.ToLower().Contains(search))
                || db.Variants.Any(item => item.Id == product.VariantId && item.Name.ToLower().Contains(search))
                || db.Colors.Any(item => item.Id == product.ColorId && item.Name.ToLower().Contains(search))
            );
        }

        var aggregates = products
            .GroupBy(product => new { product.BrandId, product.ProductModelId, product.VariantId, product.ColorId })
            .Select(group => new
            {
                group.Key.BrandId,
                group.Key.ProductModelId,
                group.Key.VariantId,
                group.Key.ColorId,
                TotalCostWithoutGst = group.Sum(product => product.PurchasePrice - product.Discount),
                TotalCostWithGst = group.Sum(product => product.TotalAmount),
                GstRate = group.Average(product => product.Tax),
                TotalQuantity = group.Count()
            });
        var totalCount = await aggregates.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var groupRows = (from aggregate in aggregates
                         join brand in db.Brands.AsNoTracking() on aggregate.BrandId equals brand.Id
                         join model in db.ProductModels.AsNoTracking() on aggregate.ProductModelId equals model.Id
                         join variant in db.Variants.AsNoTracking() on aggregate.VariantId equals variant.Id
                         join color in db.Colors.AsNoTracking() on aggregate.ColorId equals color.Id
                         select new
                         {
                             aggregate.BrandId,
                             aggregate.ProductModelId,
                             aggregate.VariantId,
                             BrandName = brand.Name,
                             ModelName = model.Name,
                             VariantName = variant.Name,
                             ColorName = color.Name,
                             TotalProductCostWithoutGst = aggregate.TotalCostWithoutGst,
                             TotalGstAmount = aggregate.TotalCostWithGst - aggregate.TotalCostWithoutGst,
                             TotalProductCostWithGst = aggregate.TotalCostWithGst,
                             GstRate = aggregate.GstRate,
                             aggregate.TotalQuantity,
                             TotalProductCost = aggregate.TotalCostWithoutGst
                         });
        var rows = await GridSorting.Apply(groupRows, request.SortBy, request.SortDirection,
            "BrandName,ModelName,VariantName,BrandId,ProductModelId,VariantId,ColorId,TotalQuantity,TotalProductCost,TotalProductCostWithoutGst,TotalProductCostWithGst,TotalGstAmount,GstRate")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var groups = rows.Select(row =>
        {
            var unitCostWithoutGst = row.TotalQuantity > 0 ? Math.Round(row.TotalProductCostWithoutGst / row.TotalQuantity, 2) : 0m;
            var unitCostWithGst = row.TotalQuantity > 0 ? Math.Round(row.TotalProductCostWithGst / row.TotalQuantity, 2) : 0m;
            var gstRate = Math.Round(row.GstRate, 2);
            return new StockGroupSummary(
                row.BrandId, row.ProductModelId, row.VariantId,
                row.BrandName, row.ModelName, row.VariantName,
                row.ColorName,
                row.TotalProductCostWithoutGst,
                row.TotalGstAmount,
                row.TotalProductCostWithGst,
                gstRate,
                unitCostWithoutGst,
                unitCostWithGst,
                row.TotalQuantity,
                row.TotalProductCost);
        }).ToArray();

        var overallQuantity = await products.CountAsync(cancellationToken);
        var overallCostWithoutGst = overallQuantity > 0
            ? await products.SumAsync(product => product.PurchasePrice - product.Discount, cancellationToken)
            : 0m;
        var overallCostWithGst = overallQuantity > 0
            ? await products.SumAsync(product => product.TotalAmount, cancellationToken)
            : 0m;
        var overallSummary = new StockTotalsSummary(
            overallQuantity,
            overallCostWithoutGst,
            overallCostWithGst - overallCostWithoutGst,
            overallCostWithGst);

        return new PagedResult<StockGroupSummary>(groups, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize), overallSummary);
    }
}

public sealed class GetStockSummaryQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockSummaryQuery, StockTotalsSummary>
{
    public async Task<StockTotalsSummary> Handle(
        GetStockSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking()
            .Where(product => product.IsActive && !product.IsSold);

        var totalQuantity = await products.CountAsync(cancellationToken);
        var costWithoutGst = totalQuantity > 0
            ? await products.SumAsync(product => product.PurchasePrice - product.Discount, cancellationToken)
            : 0m;
        var costWithGst = totalQuantity > 0
            ? await products.SumAsync(product => product.TotalAmount, cancellationToken)
            : 0m;

        return new StockTotalsSummary(
            totalQuantity,
            costWithoutGst,
            costWithGst - costWithoutGst,
            costWithGst);
    }
}

public sealed class GetAvailableStockProductsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetAvailableStockProductsQuery, PagedResult<AvailableStockProductSummary>>
{
    public async Task<PagedResult<AvailableStockProductSummary>> Handle(
        GetAvailableStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        var products = from product in db.Products.AsNoTracking()
                       join color in db.Colors.AsNoTracking() on product.ColorId equals color.Id
                       where product.IsActive && !product.IsSold
                           && product.BrandId == request.BrandId
                           && product.ProductModelId == request.ProductModelId
                           && product.VariantId == request.VariantId
                       select new
                       {
                           product.Id,
                           product.SerialNumber,
                           product.SerialNumber1,
                           ColorName = color.Name,
                           CostWithoutGst = product.PurchasePrice - product.Discount,
                           GstRate = product.Tax,
                           GstAmount = product.TotalAmount - (product.PurchasePrice - product.Discount),
                           product.TotalAmount
                       };

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            products = products.Where(item => item.SerialNumber.ToLower().Contains(search)
                || (item.SerialNumber1 != null && item.SerialNumber1.ToLower().Contains(search))
                || item.ColorName.ToLower().Contains(search));
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var groupCostWithoutGst = totalCount > 0
            ? await products.SumAsync(p => p.CostWithoutGst, cancellationToken)
            : 0m;
        var groupCostWithGst = totalCount > 0
            ? await products.SumAsync(p => p.TotalAmount, cancellationToken)
            : 0m;
        var groupSummary = new StockTotalsSummary(
            totalCount,
            groupCostWithoutGst,
            groupCostWithGst - groupCostWithoutGst,
            groupCostWithGst);

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var rows = await GridSorting.Apply(products, request.SortBy, request.SortDirection, "SerialNumber")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(item => new AvailableStockProductSummary(
            item.Id, item.SerialNumber, item.SerialNumber1, item.ColorName,
            item.CostWithoutGst, item.GstRate, item.GstAmount, item.TotalAmount)).ToArray();

        return new PagedResult<AvailableStockProductSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize), groupSummary);
    }
}
