using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products.Commands.DeleteSalesProduct;

public sealed class DeleteSalesProductCommandHandler(IAccountingInventoryDbContext db) : IRequestHandler<DeleteSalesProductCommand>
{
    public async Task Handle(DeleteSalesProductCommand request, CancellationToken cancellationToken)
    {
        var sale = await db.SalesProducts.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Sales product '{request.Id}' was not found.");
        if (await db.JournalEntries.AnyAsync(entry => entry.SourceType == "Sale" && entry.SourceId == sale.Id.ToString(), cancellationToken))
            throw new ConflictException("This sale has been posted to the ledger and cannot be deleted. Record a reversal instead.");
        if (Guid.TryParse(sale.ProductId, out var productId))
        {
            var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
            product?.MarkAvailable();
        }
        sale.Delete();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed while the sale was being deleted. Refresh and try again.");
        }
    }
}
