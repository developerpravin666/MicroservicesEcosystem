using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.ProductModels.Queries.GetProductModelById;

public sealed class GetProductModelByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetProductModelByIdQuery, ProductModelSummary>
{
    public async Task<ProductModelSummary> Handle(GetProductModelByIdQuery request, CancellationToken cancellationToken)
    {
        var productModel = await accountingInventoryDbContext.ProductModels
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"No ProductModel exists.");

        return new ProductModelSummary(productModel.Id, productModel.BrandId, productModel.ProductTypeId, productModel.Code, productModel.Name,
            productModel.Description!, productModel.IsActive);
    }

}


