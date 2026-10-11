using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModels;

public sealed class GetProductModelsQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductModelsQuery, PagedResult<ProductModelSummary>>
{
    public async Task<PagedResult<ProductModelSummary>> Handle(GetProductModelsQuery request, CancellationToken cancellationToken)
    {
        var query = accountingInventoryDbContext.ProductModels.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(search) ||
                (v.Description != null && v.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection, selectors: new SortSelectors<ProductModel>
        {
            ["brandName"] = model => accountingInventoryDbContext.Brands.Where(item => item.Id == model.BrandId).Select(item => item.Name).FirstOrDefault(),
            ["productTypeName"] = model => accountingInventoryDbContext.ProductTypes.Where(item => item.Id == model.ProductTypeId).Select(item => item.Name).FirstOrDefault(),
        })
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(productModel => new ProductModelSummary(
                productModel.Id,
                brandName : 
                accountingInventoryDbContext.Brands
                    .Where(brand => brand.Id == productModel.BrandId)
                    .Select(brand => brand.Name)
                    .FirstOrDefault() ?? string.Empty,
                
                productTypeName : 
                accountingInventoryDbContext.ProductTypes
                    .Where(productType => productType.Id == productModel.ProductTypeId)
                    .Select(productType => productType.Name)
                    .FirstOrDefault() ?? string.Empty,

                productModel.Code,
                productModel.Name,
                productModel.Description ?? string.Empty,
                productModel.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductModelSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}


