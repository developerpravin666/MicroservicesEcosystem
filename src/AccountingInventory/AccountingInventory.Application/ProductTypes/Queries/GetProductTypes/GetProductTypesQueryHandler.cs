using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypes;

public sealed class GetProductTypesQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductTypesQuery, PagedResult<ProductTypeSummary>>
{
    public async Task<PagedResult<ProductTypeSummary>> Handle(GetProductTypesQuery request, CancellationToken cancellationToken)
    {
        var query = accountingInventoryDbContext.ProductTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(search) ||
                (v.Description != null && v.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ProductType => new ProductTypeSummary(
                ProductType.Id,
                ProductType.Name,
                ProductType.Description ?? string.Empty,
                ProductType.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductTypeSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}

