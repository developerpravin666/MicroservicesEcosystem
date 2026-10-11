using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Variants.Queries.GetVariantById;

public sealed class GetVariantByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetVariantByIdQuery, VariantSummary>
{
    public async Task<VariantSummary> Handle(GetVariantByIdQuery request, CancellationToken cancellationToken)
    {
        var variant = await accountingInventoryDbContext.Variants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"No variant exists.");

        return new VariantSummary(variant.Id, variant.Name, variant.Description!, variant.IsActive);
    }

}
