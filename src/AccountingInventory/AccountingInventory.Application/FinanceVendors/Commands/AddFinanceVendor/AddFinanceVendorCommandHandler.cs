using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Commands.AddFinanceVendor;

public sealed class AddFinanceVendorCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<AddFinanceVendorCommand, FinanceVendorSummary>
{
    public async Task<FinanceVendorSummary> Handle(AddFinanceVendorCommand request, CancellationToken cancellationToken)
    {
        var code = request.Code.Trim();
        var email = request.Email.Trim();
        var mobile = request.Mobile.Trim();
        if (await db.FinanceVendors.AnyAsync(x => x.Code == code || x.Email == email || x.Mobile == mobile, cancellationToken))
            throw new ConflictException("A finance vendor with the same code, email, or mobile already exists.");

        var vendor = FinanceVendor.Create(request.Name, code, mobile, email, request.ContactName, request.ContactMobile, request.Description);
        db.FinanceVendors.Add(vendor);
        await db.SaveChangesAsync(cancellationToken);
        return new(vendor.Id, vendor.Name, vendor.Code, vendor.Mobile, vendor.Email, vendor.ContactName,
            vendor.ContactMobile, vendor.Description, vendor.IsActive);
    }
}
