using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products.Queries.GetSalesProducts;

public sealed class GetSalesProductsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetSalesProductsQuery, PagedResult<SalesProductSummary>>
{
    public async Task<PagedResult<SalesProductSummary>> Handle(GetSalesProductsQuery request, CancellationToken cancellationToken)
    {
        var query = db.SalesProducts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            var matchingProductIds = await db.Products.AsNoTracking()
                .Where(product => product.SerialNumber.ToLower().Contains(search)
                    || (product.SerialNumber1 != null && product.SerialNumber1.ToLower().Contains(search)))
                .Select(product => product.Id.ToString())
                .ToListAsync(cancellationToken);
            query = query.Where(x => x.ProductId.ToLower().Contains(search)
                || db.Customers.Any(customer => customer.Id == x.CustomerId
                    && (customer.Name.ToLower().Contains(search) || customer.Mobile.ToLower().Contains(search)))
                || matchingProductIds.Contains(x.ProductId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var sales = await SalesGridSorting.Apply(db, query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        var items = await SalesProductSummaryMapper.MapAsync(db, sales, cancellationToken);
        return new(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
