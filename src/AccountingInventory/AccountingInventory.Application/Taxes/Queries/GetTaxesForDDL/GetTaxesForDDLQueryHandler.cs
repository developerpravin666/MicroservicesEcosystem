using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxesForDDL;

public sealed class GetTaxesForDDLQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetTaxesForDDLQuery, IEnumerable<TaxRateSummary>>
{
    public async Task<IEnumerable<TaxRateSummary>> Handle(GetTaxesForDDLQuery request, CancellationToken cancellationToken)
        => await db.Taxes
            .AsNoTracking()
                        .OrderBy(tax => tax.Cgst)
            .ThenBy(tax => tax.Sgst)
            .Select(tax => new TaxRateSummary(tax.Id, tax.Cgst, tax.Sgst, tax.TotalTax, tax.Name, tax.ProfileId, tax.EffectiveFrom, tax.Treatment, tax.IsActive))
            .ToListAsync(cancellationToken);
}
