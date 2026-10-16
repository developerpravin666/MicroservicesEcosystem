using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillJson;

public sealed class GetEWayBillJsonQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEWayBillJsonQuery, string>
{
    public async Task<string> Handle(GetEWayBillJsonQuery request, CancellationToken ct)
    {
        var ewb = await db.EWayBills.AsNoTracking()
            .SingleOrDefaultAsync(e => e.Id == request.Id, ct)
            ?? throw new NotFoundException($"E-Way Bill with ID '{request.Id}' was not found.");

        return ewb.PayloadJson;
    }
}

