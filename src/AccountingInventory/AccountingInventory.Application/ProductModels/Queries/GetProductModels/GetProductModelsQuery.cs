using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModels;

public sealed record GetProductModelsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<ProductModelSummary>>;

public sealed record ProductModelSummary(Guid Id, string brandName,string productTypeName,string code, string Name, string Description, bool IsActive);


