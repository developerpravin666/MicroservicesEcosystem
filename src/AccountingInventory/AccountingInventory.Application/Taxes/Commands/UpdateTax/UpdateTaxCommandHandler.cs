using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Exceptions;
using AccountingInventory.Application.Taxes;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Taxes.Commands.UpdateTax;

public sealed class UpdateTaxCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<UpdateTaxCommand, TaxSummary>
{
    public async Task<TaxSummary> Handle(UpdateTaxCommand request, CancellationToken cancellationToken)
    {
        var tax = await db.Taxes.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Tax '{request.Id}' was not found.");
        tax.Update(request.Cgst, request.Sgst, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
        return new(tax.Id, tax.Cgst, tax.Sgst, tax.TotalTax, tax.IsActive, tax.CreatedAt, tax.CreatedBy, tax.ModifiedAt, tax.ModifiedBy);
    }
}
