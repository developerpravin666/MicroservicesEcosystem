using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using MediatR;
using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProducts;

public sealed class GetProductsQueryHandler(IAccountingInventoryDbContext dbContext)
    : IRequestHandler<GetProductsQuery, PagedResult<ProductSummary>>
{
    public async Task<PagedResult<ProductSummary>> Handle(GetProductsQuery request, CancellationToken cancellationToken)
    {
        var query = dbContext.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.SerialNumber.ToLower().Contains(search) ||
                (x.SerialNumber1 != null && x.SerialNumber1.ToLower().Contains(search)));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection, "SerialNumber", selectors: new SortSelectors<Product>
        {
            ["vendorName"] = x => dbContext.Vendors.Where(v => v.Id == x.VendorId).Select(v => v.Name).FirstOrDefault(),
            ["brandName"] = x => dbContext.Brands.Where(v => v.Id == x.BrandId).Select(v => v.Name).FirstOrDefault(),
            ["productTypeName"] = x => dbContext.ProductTypes.Where(v => v.Id == x.ProductTypeId).Select(v => v.Name).FirstOrDefault(),
            ["productModelName"] = x => dbContext.ProductModels.Where(v => v.Id == x.ProductModelId).Select(v => v.Name).FirstOrDefault(),
            ["variantName"] = x => dbContext.Variants.Where(v => v.Id == x.VariantId).Select(v => v.Name).FirstOrDefault(),
            ["colorName"] = x => dbContext.Colors.Where(v => v.Id == x.ColorId).Select(v => v.Name).FirstOrDefault(),
        }).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProductSummary(x.Id, x.VendorId, x.BrandId, x.ProductTypeId, x.ProductModelId,
                x.VariantId, x.ColorId, x.SerialNumber, x.SerialNumber1, x.PurchasePrice,
                x.TotalAmount, x.Discount, x.Cgst, x.Sgst, x.Tax, x.IsActive, x.IsSold, x.BillNumber, x.PurchaseDate, x.PaymentTermsDays, x.DueDate,
                dbContext.Vendors.Where(v => v.Id == x.VendorId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Brands.Where(v => v.Id == x.BrandId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.ProductTypes.Where(v => v.Id == x.ProductTypeId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.ProductModels.Where(v => v.Id == x.ProductModelId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Variants.Where(v => v.Id == x.VariantId).Select(v => v.Name).FirstOrDefault() ?? string.Empty,
                dbContext.Colors.Where(v => v.Id == x.ColorId).Select(v => v.Name).FirstOrDefault() ?? string.Empty)).ToListAsync(cancellationToken);
        return new PagedResult<ProductSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
