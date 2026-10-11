using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductTypes.Commands.UpdateProductTypeCommand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.ProductTypes.Commands.UpdateProductTypeCommand;

/// <summary>
/// The entire ProductType update flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateProductTypeCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateProductTypeCommandHandler> logger)
    : IRequestHandler<UpdateProductTypeCommand, UpdateProductTypeResult>
{

    public async Task<UpdateProductTypeResult> Handle(UpdateProductTypeCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var ProductTypeTaken = await accountingInventoryDbContext.ProductTypes.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!ProductTypeTaken)
        {
            logger.LogWarning("ProductType not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A ProductType with ID '{request.Id}' was not found.");
        }

        var productType = ProductType.Update(request.Id, request.Name, request.Description, request.IsActive);

        accountingInventoryDbContext.ProductTypes.Update(productType);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProductType {ProductTypeId} - ({Name}) - {Description} update successfully.",
            productType.Id, productType.Name, productType.Description);

        return new UpdateProductTypeResult(productType.Id, productType.Name, productType.Description!, productType.IsActive);
    }
}

