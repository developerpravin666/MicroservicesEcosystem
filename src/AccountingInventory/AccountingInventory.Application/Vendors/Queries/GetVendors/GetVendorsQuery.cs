using AccountingInventory.Application.Common.Models;
using MediatR;

namespace AccountingInventory.Application.Vendors.Queries.GetVendors;

public sealed record GetVendorsQuery(
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    string? SortDirection = null,
    string? Search = null) : IRequest<PagedResult<VendorSummary>>;

public sealed record VendorSummary(Guid Id, string Name, string Code, string Mobile, string Email, string Description, string Address, bool IsActive);

