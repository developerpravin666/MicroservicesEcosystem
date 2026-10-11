using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Variants.Queries.GetVariants;

public sealed record GetVariantsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<VariantSummary>>;

public sealed record VariantSummary(Guid Id, string Name, string Description, bool IsActive);
