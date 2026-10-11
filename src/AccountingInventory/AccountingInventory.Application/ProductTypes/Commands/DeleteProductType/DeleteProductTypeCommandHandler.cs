using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductTypes.Commands.DeleteProductType;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.DeleteVendor;

public sealed class DeleteProductTypeCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteProductTypeCommandHandler> logger)
    : IRequestHandler<DeleteProductTypeCommand>
{
    public async Task Handle(DeleteProductTypeCommand request, CancellationToken cancellationToken)
    {
        var ProductType = await accountingInventoryDbContext.ProductTypes
            .SingleOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A ProductType with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "ProductType", request.Id, cancellationToken);
        ProductType.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("ProductType {ProductTypeId} ({ProductTypeName}) deleted.", ProductType.Id, ProductType.Name);
    }
}

