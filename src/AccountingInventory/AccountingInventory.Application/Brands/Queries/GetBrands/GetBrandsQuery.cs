using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Brands.Queries.GetBrands;

public sealed record GetBrandsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<BrandSummary>>;

public sealed record BrandSummary(Guid Id, string Name, string Description, bool IsActive);
