using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductTypes.Queries.GetProductTypeById;

public sealed class GetProductTypeByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductTypeByIdQuery, ProductTypeSummary>
{
    public async Task<ProductTypeSummary> Handle(GetProductTypeByIdQuery request, CancellationToken cancellationToken)
    {
        var productType = await accountingInventoryDbContext.ProductTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"No ProductType exists.");

        return new ProductTypeSummary(productType.Id, productType.Name, productType.Description!, productType.IsActive);
    }

}

