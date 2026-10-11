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
                || db.Variants.Any(item => item.Id == product.VariantId && item.Name.ToLower().Contains(search)));
        }

        var aggregates = products
            .GroupBy(product => new { product.BrandId, product.ProductModelId, product.VariantId })
            .Select(group => new
            {
                group.Key.BrandId,
                group.Key.ProductModelId,
                group.Key.VariantId,
                TotalProductCost = group.Sum(product => product.PurchasePrice - product.Discount),
                TotalQuantity = group.Count()
            });
        var totalCount = await aggregates.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var groupRows = (from aggregate in aggregates
            join brand in db.Brands.AsNoTracking() on aggregate.BrandId equals brand.Id
            join model in db.ProductModels.AsNoTracking() on aggregate.ProductModelId equals model.Id
            join variant in db.Variants.AsNoTracking() on aggregate.VariantId equals variant.Id
            select new
            {
                aggregate.BrandId, aggregate.ProductModelId, aggregate.VariantId,
                BrandName = brand.Name, ModelName = model.Name, VariantName = variant.Name,
                aggregate.TotalProductCost, aggregate.TotalQuantity
            });
        var rows = await GridSorting.Apply(groupRows, request.SortBy, request.SortDirection,
            "BrandName,ModelName,VariantName,BrandId,ProductModelId,VariantId")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var groups = rows.Select(row => new StockGroupSummary(row.BrandId, row.ProductModelId,
            row.VariantId, row.BrandName, row.ModelName, row.VariantName, row.TotalProductCost, row.TotalQuantity)).ToArray();

        return new PagedResult<StockGroupSummary>(groups, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
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
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var rows = await GridSorting.Apply(products, request.SortBy, request.SortDirection, "SerialNumber")
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = rows.Select(item => new AvailableStockProductSummary(
            item.Id, item.SerialNumber, item.SerialNumber1, item.ColorName, item.TotalAmount)).ToArray();

        return new PagedResult<AvailableStockProductSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
