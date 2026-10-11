using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Vendors.Queries.GetVendorsForDDL;

public sealed class GetVendorsForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetVendorsForDDL, IEnumerable<GetVendorsForDDLSummary>>
{
    public async Task<IEnumerable<GetVendorsForDDLSummary>> Handle(GetVendorsForDDL request, CancellationToken cancellationToken)
    {
        var vendors = await accountingInventoryDbContext.Vendors
            .AsNoTracking()
            .Where(v => v.IsActive)
            .Select(v => new GetVendorsForDDLSummary(v.Id, v.Name))
            .ToListAsync(cancellationToken);

        return vendors;
    }
}
