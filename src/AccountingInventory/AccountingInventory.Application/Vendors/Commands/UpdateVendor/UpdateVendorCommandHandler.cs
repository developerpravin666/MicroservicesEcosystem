using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.UpdateVendor;

/// <summary>
/// The entire vendor add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class UpdateVendorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<UpdateVendorCommandHandler> logger)
    : IRequestHandler<UpdateVendorCommand, UpdateVendorResult>
{

    public async Task<UpdateVendorResult> Handle(UpdateVendorCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToLowerInvariant();

        var vendorTaken = await accountingInventoryDbContext.Vendors.AnyAsync(t => t.Id == request.Id, cancellationToken);

        if (!vendorTaken)
        {
            logger.LogWarning("Vendor not found: ID {Id}.", request.Id);
            throw new NotFoundException($"A vendor with ID '{request.Id}' was not found.");
        }

        var vendor = Vendor.Update(request.Id, request.Name, request.Code, request.Mobile, request.Email, request.IsActive,
            request.Description, request.Address);

        accountingInventoryDbContext.Vendors.Update(vendor);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Vendor {VendorId} - ({Name}) - {Code} update successfully.",
            vendor.Id, vendor.Name, vendor.Code);

        return new UpdateVendorResult(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email, vendor.IsActive, vendor.Description!, vendor.Address!);
    }
}
