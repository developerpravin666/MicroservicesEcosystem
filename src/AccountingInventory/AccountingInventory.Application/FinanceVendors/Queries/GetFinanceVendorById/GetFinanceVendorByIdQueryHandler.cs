using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Queries.GetFinanceVendorById;

public sealed class GetFinanceVendorByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetFinanceVendorByIdQuery, FinanceVendorSummary>
{
    public async Task<FinanceVendorSummary> Handle(GetFinanceVendorByIdQuery request, CancellationToken cancellationToken)
    {
        var vendor = await db.FinanceVendors.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Finance vendor '{request.Id}' was not found.");
        return new(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email,
            vendor.ContactName, vendor.ContactMobile, vendor.Description, vendor.IsActive);
    }
}
