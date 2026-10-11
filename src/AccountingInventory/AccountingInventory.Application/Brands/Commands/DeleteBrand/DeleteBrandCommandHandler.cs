using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Brands.Commands.DeleteBrand;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.DeleteVendor;

public sealed class DeleteBrandCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteBrandCommandHandler> logger)
    : IRequestHandler<DeleteBrandCommand>
{
    public async Task Handle(DeleteBrandCommand request, CancellationToken cancellationToken)
    {
        var brand = await accountingInventoryDbContext.Brands
            .SingleOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A brand with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "Brand", request.Id, cancellationToken);
        brand.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Brand {BrandId} ({BrandName}) deleted.", brand.Id, brand.Name);
    }
}
