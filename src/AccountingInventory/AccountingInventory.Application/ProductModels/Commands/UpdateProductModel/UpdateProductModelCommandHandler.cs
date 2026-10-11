using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.ProductModels.Commands.UpdateProductModelCommand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.ProductModels.Commands.UpdateProductModelCommand;

/// <summary>
/// The entire ProductModel update flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateProductModelCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateProductModelCommandHandler> logger)
    : IRequestHandler<UpdateProductModelCommand, UpdateProductModelResult>
{

    public async Task<UpdateProductModelResult> Handle(UpdateProductModelCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var ProductModelTaken = await accountingInventoryDbContext.ProductModels.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!ProductModelTaken)
        {
            logger.LogWarning("ProductModel not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A ProductModel with ID '{request.Id}' was not found.");
        }

        var existing = await accountingInventoryDbContext.ProductModels.AsNoTracking().SingleAsync(row => row.Id == request.Id, cancellationToken);
        if (!await accountingInventoryDbContext.Brands.AnyAsync(row => row.Id == request.BrandId && row.IsActive, cancellationToken)
            || !await accountingInventoryDbContext.ProductTypes.AnyAsync(row => row.Id == request.ProductTypeId && row.IsActive, cancellationToken))
            throw new ConflictException("Select an active brand and product type from this tenant.");
        if ((existing.BrandId != request.BrandId || existing.ProductTypeId != request.ProductTypeId)
            && await accountingInventoryDbContext.Products.IgnoreQueryFilters().AnyAsync(row => row.ProductModelId == request.Id, cancellationToken))
            throw new ConflictException("A model referenced by inventory cannot change its brand or product type.");
        if (await accountingInventoryDbContext.ProductModels.AnyAsync(row => row.Id != request.Id
            && row.BrandId == request.BrandId && row.ProductTypeId == request.ProductTypeId
            && row.Code.ToLower() == request.Code.Trim().ToLower(), cancellationToken))
            throw new ConflictException("A model with this code already exists for this brand and product type.");
        var productModel = ProductModel.Update(request.Id, request.BrandId, request.ProductTypeId, request.Code, request.Name, request.Description, request.IsActive);

        productModel.CreatedAt = existing.CreatedAt;
        productModel.CreatedBy = existing.CreatedBy;
        accountingInventoryDbContext.ProductModels.Update(productModel);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "ProductModel {ProductModelId} - ({Name}) - {Description} update successfully.",
            productModel.Id, productModel.Name, productModel.Description);

        return new UpdateProductModelResult(productModel.Id, productModel.BrandId, productModel.ProductTypeId, productModel.Code, productModel.Name, productModel.Description!, productModel.IsActive);
    }
}


