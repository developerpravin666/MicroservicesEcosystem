using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.CancelEInvoice;

public sealed class CancelEInvoiceCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CancelEInvoiceCommand>
{
    public async Task Handle(CancelEInvoiceCommand request, CancellationToken ct)
    {
        var eInvoice = await db.EInvoices
            .SingleOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"E-Invoice with ID '{request.Id}' was not found.");

        eInvoice.Cancel(request.Reason);
        await db.SaveChangesAsync(ct);
    }
}

