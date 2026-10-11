using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Colors.Queries.GetColorsForDDL;

public sealed class GetColorsForDDLQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetColorsForDDL, IEnumerable<GetColorsForDDLSummary>>
{
    public async Task<IEnumerable<GetColorsForDDLSummary>> Handle(GetColorsForDDL request, CancellationToken cancellationToken)
    {
        var colors = await accountingInventoryDbContext.Colors
            .AsNoTracking()
            .Where(b => b.IsActive)
            .Select(b => new GetColorsForDDLSummary(b.Id, b.Name))
            .ToListAsync(cancellationToken);

        return colors;
    }
}
