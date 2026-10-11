using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Colors.Queries.GetColors;

public sealed class GetColorsQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetColorsQuery, PagedResult<ColorSummary>>
{
    public async Task<PagedResult<ColorSummary>> Handle(GetColorsQuery request, CancellationToken cancellationToken)
    {
        var query = accountingInventoryDbContext.Colors.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();
            query = query.Where(v => v.Name.ToLower().Contains(search) ||
                (v.Description != null && v.Description.ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await GridSorting.Apply(query, request.SortBy, request.SortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(color => new ColorSummary(
                color.Id,               
                color.Name,
                color.Description ?? string.Empty,
                color.IsActive))
            .ToListAsync(cancellationToken);

        return new PagedResult<ColorSummary>(items, page, pageSize, totalCount,
            totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize));
    }
}
