using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModelsForDDL;

public sealed class GetProductModelsForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductModelsForDDL, IEnumerable<GetProductModelsForDDLSummary>>
{
    public async Task<IEnumerable<GetProductModelsForDDLSummary>> Handle(GetProductModelsForDDL request, CancellationToken cancellationToken)
    {
        var ProductModels = await accountingInventoryDbContext.ProductModels
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new GetProductModelsForDDLSummary(b.Id, b.Name, b.BrandId, b.ProductTypeId))
            .ToListAsync(cancellationToken);

        return ProductModels;
    }
}

