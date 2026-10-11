using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Vendors.Queries.GetVendorById;

public sealed class GetVendorByIdQueryHandler(IAccountingInventoryDbContext accountingInventoryDbContext)
    : IRequestHandler<GetVendorByIdQuery, VendorSummary>
{
    public async Task<VendorSummary> Handle(GetVendorByIdQuery request, CancellationToken cancellationToken)
    {
        var vendor = await accountingInventoryDbContext.Vendors
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id== request.Id, cancellationToken)
            ?? throw new NotFoundException($"No vendor exists.");

        return new VendorSummary(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email, vendor.Description, vendor.Address, vendor.IsActive);
    }

}
