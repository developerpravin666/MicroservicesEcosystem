using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Variants.Queries.GetVariantsForDDL;

public sealed class GetVariantsForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetVariantsForDDL, IEnumerable<GetVariantsForDDLSummary>>
{
    public async Task<IEnumerable<GetVariantsForDDLSummary>> Handle(GetVariantsForDDL request, CancellationToken cancellationToken)
    {
        var variants = await accountingInventoryDbContext.Variants
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new GetVariantsForDDLSummary(b.Id, b.Name))
            .ToListAsync(cancellationToken);

        return variants;
    }
}
