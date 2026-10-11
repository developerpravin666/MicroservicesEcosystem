using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendors;

public sealed record GetFinanceVendorsQuery(int Page = 1, int PageSize = 20, string? SortBy = null,
    string? SortDirection = null, string? Search = null) : IRequest<PagedResult<FinanceVendorSummary>>;
