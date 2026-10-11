using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Brands.Commands.UpdateBrandCommand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Brands.Commands.UpdateBrandCommand;

/// <summary>
/// The entire brand update flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateBrandCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateBrandCommandHandler> logger)
    : IRequestHandler<UpdateBrandCommand, UpdateBrandResult>
{

    public async Task<UpdateBrandResult> Handle(UpdateBrandCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var brandTaken = await accountingInventoryDbContext.Brands.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!brandTaken)
        {
            logger.LogWarning("Brand not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A brand with ID '{request.Id}' was not found.");
        }

        var brand = Brand.Update(request.Id, request.Name, request.Description, request.IsActive);

        accountingInventoryDbContext.Brands.Update(brand);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Brand {BrandId} - ({Name}) - {Description} update successfully.",
            brand.Id, brand.Name, brand.Description);

        return new UpdateBrandResult(brand.Id, brand.Name, brand.Description!, brand.IsActive);
    }
}
