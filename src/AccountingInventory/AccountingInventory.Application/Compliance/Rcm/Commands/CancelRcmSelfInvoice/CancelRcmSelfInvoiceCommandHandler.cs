using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CancelRcmSelfInvoice;

public sealed class CancelRcmSelfInvoiceCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CancelRcmSelfInvoiceCommand>
{
    public async Task Handle(CancelRcmSelfInvoiceCommand request, CancellationToken ct)
    {
        var rcm = await db.RcmSelfInvoices
            .SingleOrDefaultAsync(r => r.Id == request.Id, ct)
            ?? throw new NotFoundException($"RCM Self-Invoice with ID '{request.Id}' was not found.");

        rcm.Cancel(request.Reason);
        await db.SaveChangesAsync(ct);
    }
}

