using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Taxes;
using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxes;

public sealed record GetTaxesQuery(int Page = 1, int PageSize = 20, string? SortBy = null, string? SortDirection = null) : IRequest<PagedResult<TaxSummary>>;
