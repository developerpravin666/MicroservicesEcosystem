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
        var ids = await db.Taxes.Where(t => t.ProfileId == tax.ProfileId).Select(t => t.Id).ToArrayAsync(cancellationToken);
        if (await db.ProductTypes.AnyAsync(t => t.DefaultTaxId == tax.ProfileId, cancellationToken)
            || await db.Products.AnyAsync(p => p.TaxId.HasValue && ids.Contains(p.TaxId.Value), cancellationToken)
            || await db.StockSkus.AnyAsync(s => s.DefaultTaxId == tax.ProfileId, cancellationToken)
            || await db.SkuMovements.AnyAsync(s => s.TaxId.HasValue && ids.Contains(s.TaxId.Value), cancellationToken)
            || await db.SalesInvoiceLines.AnyAsync(l => l.TaxId.HasValue && ids.Contains(l.TaxId.Value), cancellationToken)
            || await db.SalesProducts.AnyAsync(l => l.TaxId.HasValue && ids.Contains(l.TaxId.Value), cancellationToken))
            throw new ArgumentException("A referenced tax profile's rate history cannot be deleted. Add a new dated version.");
        tax.Delete();
        await db.SaveChangesAsync(cancellationToken);
    }
}
