using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Brands.Queries.GetBrandById;

public sealed class GetBrandByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetBrandByIdQuery, BrandSummary>
{
    public async Task<BrandSummary> Handle(GetBrandByIdQuery request, CancellationToken cancellationToken)
    {
        var brand = await accountingInventoryDbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"No brand exists.");

        return new BrandSummary(brand.Id, brand.Name, brand.Description!, brand.IsActive);
    }

}
