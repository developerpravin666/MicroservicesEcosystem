using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Commands.UpdateFinanceVendor;

public sealed class UpdateFinanceVendorCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<UpdateFinanceVendorCommand, FinanceVendorSummary>
{
    public async Task<FinanceVendorSummary> Handle(UpdateFinanceVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await db.FinanceVendors.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Finance vendor '{request.Id}' was not found.");
        var code = request.Code.Trim();
        var email = request.Email.Trim();
        var mobile = request.Mobile.Trim();
        if (await db.FinanceVendors.AnyAsync(x => x.Id != request.Id &&
            (x.Code == code || x.Email == email || x.Mobile == mobile), cancellationToken))
            throw new ConflictException("A finance vendor with the same code, email, or mobile already exists.");

        vendor.Update(request.Name, code, mobile, email, request.ContactName, request.ContactMobile, request.Description, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return new(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email, vendor.ContactName,
            vendor.ContactMobile, vendor.Description, vendor.IsActive);
    }
}
