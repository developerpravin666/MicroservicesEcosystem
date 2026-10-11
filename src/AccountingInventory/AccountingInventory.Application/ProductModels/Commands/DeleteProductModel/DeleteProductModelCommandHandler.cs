using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductModels.Commands.DeleteProductModel;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.DeleteVendor;

public sealed class DeleteProductModelCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteProductModelCommandHandler> logger)
    : IRequestHandler<DeleteProductModelCommand>
{
    public async Task Handle(DeleteProductModelCommand request, CancellationToken cancellationToken)
    {
        var productModel = await accountingInventoryDbContext.ProductModels
            .SingleOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A ProductModel with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "ProductModel", request.Id, cancellationToken);
        productModel.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductModel {ProductModelId} ({ProductModelName}) deleted.", productModel.Id, productModel.Name);
    }
}


