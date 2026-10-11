using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Sales.Products.Queries.GetSalesProducts;

public sealed record GetSalesProductsQuery(int Page = 1, int PageSize = 20, string? Search = null, string? SortBy = null, string? SortDirection = null)
    : IRequest<PagedResult<SalesProductSummary>>;
