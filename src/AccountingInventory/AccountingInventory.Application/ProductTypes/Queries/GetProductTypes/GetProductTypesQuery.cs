using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypes;

public sealed record GetProductTypesQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<ProductTypeSummary>>;

public sealed record ProductTypeSummary(Guid Id, string Name, string Description, bool IsActive);

