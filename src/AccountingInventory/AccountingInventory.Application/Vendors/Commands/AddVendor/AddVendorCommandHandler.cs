using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Vendors.Commands.AddVendor;

/// <summary>
/// The entire vendor add flow, in one handler, entirely inside AccountingInventory:
/// </summary>
public sealed class AddVendorCommandHandler(
    IAccountingInventoryDbContext accountingInventoryDbContext,
    ILogger<AddVendorCommandHandler> logger)
    : IRequestHandler<AddVendorCommand, AddVendorResult>
{

    public async Task<AddVendorResult> Handle(AddVendorCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.Code.Trim().ToLowerInvariant();

        var vendorTaken = await accountingInventoryDbContext.Vendors.AnyAsync(t => t.Code == normalizedCode || t.Name == request.Name, cancellationToken);

        if (vendorTaken)
        {
            logger.LogWarning("Vendor registration rejected: code {Code} already in use.", normalizedCode);
            throw new ConflictException($"A vendor with code '{normalizedCode}' already exists.");
        }

        var vendor = Vendor.Create(request.Name, request.Code, request.Mobile, request.Email, request.Description, request.Address);

        vendor.Activate();
        accountingInventoryDbContext.Vendors.Add(vendor);
        await accountingInventoryDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Vendor {VendorId} - ({Name}) - {Code} added successfully.",
            vendor.Id, vendor.Name, vendor.Code);

       
        //await schemaProvisioner.ProvisionAsync(tenant.Id, tenant.SchemaName, cancellationToken);       
        //await db.SaveChangesAsync(cancellationToken);

        //tenant.Activate();
        //await db.SaveChangesAsync(cancellationToken);

        //logger.LogInformation(
        //    "Vendor {VendorId} ({Name}) fully provisioned and activated: schema {SchemaName}, default roles Admin/User seeded.",
        //    tenant.Id, tenant.Name, tenant.SchemaName);

        return new AddVendorResult(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email, vendor.Description!, vendor.Address!);
    }
}
