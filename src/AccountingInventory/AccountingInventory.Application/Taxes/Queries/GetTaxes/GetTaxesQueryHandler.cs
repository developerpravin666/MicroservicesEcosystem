using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxes;

public sealed class GetTaxesQueryHandler(IAccountingInventoryDbContext db) : IRequestHandler<GetTaxesQuery, PagedResult<TaxSummary>>
{
    public async Task<PagedResult<TaxSummary>> Handle(GetTaxesQuery request, CancellationToken cancellationToken)
    {
        var query = GridSorting.Apply(db.Taxes.AsNoTracking(), request.SortBy, request.SortDirection, "Cgst,Sgst");
        var total = await query.CountAsync(cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new TaxSummary(
            x.Id, x.Cgst, x.Sgst, x.TotalTax, x.IsActive, x.CreatedAt, x.CreatedBy, x.ModifiedAt, x.ModifiedBy)).ToListAsync(cancellationToken);
        return new(items, page, pageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize));
    }
}
