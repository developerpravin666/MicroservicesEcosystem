using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendors;

public sealed class GetFinanceVendorsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetFinanceVendorsQuery, PagedResult<FinanceVendorSummary>>
{
    public async Task<PagedResult<FinanceVendorSummary>> Handle(GetFinanceVendorsQuery request, CancellationToken cancellationToken)
    {
        var query = db.FinanceVendors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(search) || x.Code.ToLower().Contains(search) ||
                x.Mobile.ToLower().Contains(search) || x.Email.ToLower().Contains(search) ||
                x.ContactName.ToLower().Contains(search) || x.ContactMobile.ToLower().Contains(search) ||
                (x.Description != null && x.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new FinanceVendorSummary(x.Id, x.Name, x.Code, x.Mobile, x.Email,
                x.ContactName, x.ContactMobile, x.Description, x.IsActive))
            .ToListAsync(cancellationToken);
        return new(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
