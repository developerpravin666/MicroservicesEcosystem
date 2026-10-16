using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.CancelEWayBill;

public sealed class CancelEWayBillCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CancelEWayBillCommand>
{
    public async Task Handle(CancelEWayBillCommand request, CancellationToken ct)
    {
        var ewb = await db.EWayBills
            .SingleOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"E-Way Bill with ID '{request.Id}' was not found.");

        ewb.Cancel(request.Reason);
        await db.SaveChangesAsync(ct);
    }
}

