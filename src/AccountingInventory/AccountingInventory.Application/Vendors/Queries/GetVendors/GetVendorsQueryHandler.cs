using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Vendors.Queries.GetVendors;

public sealed class GetVendorsQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetVendorsQuery, PagedResult<VendorSummary>>
{
    public async Task<PagedResult<VendorSummary>> Handle(GetVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = accountingInventoryDbContext.Vendors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(search) ||
                v.Code.ToLower().Contains(search) || v.Mobile.ToLower().Contains(search) ||
                v.Email.ToLower().Contains(search) ||
                (v.Description != null && v.Description.ToLower().Contains(search)) ||
                (v.Address != null && v.Address.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(v => new VendorSummary(v.Id, v.Name, v.Code, v.Mobile, v.Email, v.Description!, v.Address!, v.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<VendorSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
