using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.DeleteVendor;

public sealed class DeleteVendorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<DeleteVendorCommandHandler> logger)
    : IRequestHandler<DeleteVendorCommand>
{
    public async Task Handle(DeleteVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await accountingInventoryDbContext.Vendors
            .SingleOrDefaultAsync(v => v.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A vendor with ID '{request.Id}' was not found.");

        await MasterReferenceIntegrity.EnsureDeletableAsync(accountingInventoryDbContext, "Vendor", request.Id, cancellationToken);
        vendor.Delete();
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Vendor {VendorId} ({VendorName}) deleted.", vendor.Id, vendor.Name);
    }
}
