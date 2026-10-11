using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Taxes;
using AccountingInventory.Domain.Entities;
using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.AddTax;

public sealed class AddTaxCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<AddTaxCommand, TaxSummary>
{
    public async Task<TaxSummary> Handle(AddTaxCommand request, CancellationToken cancellationToken)
    {
        var tax = Tax.Create(request.Cgst, request.Sgst);
        db.Taxes.Add(tax);
        await db.SaveChangesAsync(cancellationToken);
        return new(tax.Id, tax.Cgst, tax.Sgst, tax.TotalTax, tax.IsActive, tax.CreatedAt, tax.CreatedBy, tax.ModifiedAt, tax.ModifiedBy);
    }
}
