using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductTypes.Commands.AddProductType;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

/// <summary>
/// The entire ProductType add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddProductTypeCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddProductTypeCommandHandler> logger)
    : IRequestHandler<AddProductTypeCommand, AddProductTypeResult>
{

    public async Task<AddProductTypeResult> Handle(AddProductTypeCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var productTypeTaken = await accountingInventoryDbContext.ProductTypes.AnyAsync(p => p.Name == request.Name, cancellationToken);

        if (productTypeTaken)
        {
            logger.LogWarning("ProductType registration rejected: name {Name} already in use.", request.Name);
            throw new ConflictException($"A ProductType with name '{request.Name}' already exists.");
        }

        var productType = ProductType.Create( request.Name, request.Description);

        productType.Activate();
        accountingInventoryDbContext.ProductTypes.Add(productType);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProductType {ProductTypeId} - ({Name}) added successfully.",
            productType.Id, productType.Name);

        return new AddProductTypeResult(productType.Id, productType.Name, productType.Description!);
    }
}

