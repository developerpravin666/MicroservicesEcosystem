using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Brands.Queries.GetBrandsForDDL;

public sealed class GetBrandsForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetBrandsForDDL, IEnumerable<GetBrandsForDDLSummary>>
{
    public async Task<IEnumerable<GetBrandsForDDLSummary>> Handle(GetBrandsForDDL request, CancellationToken cancellationToken)
    {
        var brands = await accountingInventoryDbContext.Brands
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new GetBrandsForDDLSummary(b.Id, b.Name))
            .ToListAsync(cancellationToken);

        return brands;
    }
}
