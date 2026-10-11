using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Colors.Queries.GetColorById;

public sealed class GetColorByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetColorByIdQuery, ColorSummary>
{
    public async Task<ColorSummary> Handle(GetColorByIdQuery request, CancellationToken cancellationToken)
    {
        var color = await accountingInventoryDbContext.Colors
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"No color exists.");

        return new ColorSummary(color.Id, color.Name, color.Description!, color.IsActive);
    }

}
