using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoiceJson;

public sealed class GetEInvoiceJsonQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetEInvoiceJsonQuery, string>
{
    public async Task<string> Handle(GetEInvoiceJsonQuery request, CancellationToken ct)
    {
        var e = await db.EInvoices.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == request.Id, ct)
            ?? throw new NotFoundException($"E-Invoice with ID '{request.Id}' was not found.");

        return e.PayloadJson;
    }
}

