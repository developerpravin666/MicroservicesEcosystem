using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Brands.Commands.AddBrand;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

/// <summary>
/// The entire brand add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddBrandCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddBrandCommandHandler> logger)
    : IRequestHandler<AddBrandCommand, AddBrandResult>
{

    public async Task<AddBrandResult> Handle(AddBrandCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Name.Trim().ToLowerInvariant();

        var brandTaken = await accountingInventoryDbContext.Brands.AnyAsync(b => b.Name == request.Name, cancellationToken);

        if (brandTaken)
        {
            logger.LogWarning("Brand registration rejected: name {Name} already in use.", request.Name);
            throw new ConflictException($"A brand with name '{request.Name}' already exists.");
        }

        var brand = Brand.Create (request.Name, request.Description);

        brand.Activate();
        accountingInventoryDbContext.Brands.Add(brand);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Brand {BrandId} - ({Name}) added successfully.",
            brand.Id, brand.Name);

        return new AddBrandResult(brand.Id, brand.Name, brand.Description!);
    }
}
