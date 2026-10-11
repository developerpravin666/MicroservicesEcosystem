using AccountingInventory.Application.Common;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Commands.DeleteTax;

public sealed class DeleteTaxCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<DeleteTaxCommand>
{
    public async Task Handle(DeleteTaxCommand request, CancellationToken cancellationToken)
    {
        var tax = await db.Taxes.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Tax '{request.Id}' was not found.");
        await MasterReferenceIntegrity.EnsureDeletableAsync(db, "Tax", request.Id, cancellationToken);
        tax.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
