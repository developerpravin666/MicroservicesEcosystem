using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.FinanceVendors.Commands.DeleteFinanceVendor;

public sealed class DeleteFinanceVendorCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<DeleteFinanceVendorCommand>
{
    public async Task Handle(DeleteFinanceVendorCommand request, CancellationToken cancellationToken)
    {
        var vendor = await db.FinanceVendors.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Finance vendor '{request.Id}' was not found.");
        await MasterReferenceIntegrity.EnsureDeletableAsync(db, "FinanceVendor", request.Id, cancellationToken);
        vendor.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
