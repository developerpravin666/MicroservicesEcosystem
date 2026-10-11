using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorsForDDL;

public sealed class GetFinanceVendorsForDDLQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetFinanceVendorsForDDLQuery, IEnumerable<GetFinanceVendorsForDDLSummary>>
{
    public async Task<IEnumerable<GetFinanceVendorsForDDLSummary>> Handle(GetFinanceVendorsForDDLQuery request, CancellationToken cancellationToken)
        => await db.FinanceVendors.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.Name).Select(x => new GetFinanceVendorsForDDLSummary(x.Id, x.Name, x.Code))
            .ToListAsync(cancellationToken);
}
