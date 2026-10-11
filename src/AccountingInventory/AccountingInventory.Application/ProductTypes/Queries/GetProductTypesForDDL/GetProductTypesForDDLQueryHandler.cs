using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypesForDDL;

public sealed class GetProductTypesForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductTypesForDDL, IEnumerable<GetProductTypesForDDLSummary>>
{
    public async Task<IEnumerable<GetProductTypesForDDLSummary>> Handle(GetProductTypesForDDL request, CancellationToken cancellationToken)
    {
        var productTypes = await accountingInventoryDbContext.ProductTypes
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new GetProductTypesForDDLSummary(b.Id, b.Name))
            .ToListAsync(cancellationToken);

        return productTypes;
    }
}
