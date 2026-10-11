using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductModels.Commands.AddProductModel;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

/// <summary>
/// The entire ProductModel add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddProductModelCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddProductModelCommandHandler> logger)
    : IRequestHandler<AddProductModelCommand, AddProductModelResult>
{

    public async Task<AddProductModelResult> Handle(AddProductModelCommand request, CancellationToken cancellationToken)
    {
        if (!await accountingInventoryDbContext.Brands.AnyAsync(row => row.Id == request.BrandId && row.IsActive, cancellationToken)
            || !await accountingInventoryDbContext.ProductTypes.AnyAsync(row => row.Id == request.ProductTypeId && row.IsActive, cancellationToken))
            throw new ConflictException("Select an active brand and product type from this tenant.");
        var normalizedCode = request.Code.Trim().ToLowerInvariant();

        var productModelTaken = await accountingInventoryDbContext.ProductModels.AnyAsync(p => p.BrandId == request.BrandId
        && p.ProductTypeId == request.ProductTypeId
        && p.Code.ToLower() == normalizedCode, cancellationToken);

        if (productModelTaken)
        {
            logger.LogWarning("ProductModel registration rejected: code {Code} already in use.", request.Code);
            throw new ConflictException($"A ProductModel with code '{request.Code}' already exists.");
        }

        var productModel = ProductModel.Create(request.BrandId, request.ProductTypeId, request.Code, request.Name, request.Description);

        productModel.Activate();
        accountingInventoryDbContext.ProductModels.Add(productModel);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProductModel {ProductModelId} - ({Name}) added successfully.",
            productModel.Id, productModel.Name);

        return new AddProductModelResult(productModel.Id, productModel.BrandId, productModel.ProductTypeId, productModel.Code, productModel.Name, productModel.Description!);
    }
}


