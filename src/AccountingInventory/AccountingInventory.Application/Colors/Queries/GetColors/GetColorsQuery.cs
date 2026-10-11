using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Colors.Queries.GetColors;

public sealed record GetColorsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<ColorSummary>>;

public sealed record ColorSummary(Guid Id, string Name, string Description, bool IsActive);
