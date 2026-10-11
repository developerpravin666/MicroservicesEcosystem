using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Exceptions;
using AccountingInventory.Application.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxById;

public sealed class GetTaxByIdQueryHandler(IAccountingInventoryDbContext db) : IRequestHandler<GetTaxByIdQuery, TaxSummary>
{
    public async Task<TaxSummary> Handle(GetTaxByIdQuery request, CancellationToken cancellationToken)
        => await db.Taxes.AsNoTracking().Where(x => x.Id == request.Id).Select(x => new TaxSummary(
            x.Id, x.Cgst, x.Sgst, x.TotalTax, x.IsActive, x.CreatedAt, x.CreatedBy, x.ModifiedAt, x.ModifiedBy)).SingleOrDefaultAsync(cancellationToken)
           ?? throw new NotFoundException($"Tax '{request.Id}' was not found.");
}
