using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products.Commands.UpdateSalesProduct;

public sealed class UpdateSalesProductCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<UpdateSalesProductCommand, SalesProductSummary>
{
    public async Task<SalesProductSummary> Handle(UpdateSalesProductCommand request, CancellationToken cancellationToken)
    {
        var sale = await db.SalesProducts.SingleOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Sales product '{request.Id}' was not found.");
        if (await db.JournalEntries.AnyAsync(entry => entry.SourceType == "Sale" && entry.SourceId == sale.Id.ToString(), cancellationToken))
            throw new ConflictException("This sale has been posted to the ledger and cannot be edited. Record a reversal and replacement sale instead.");
        if (!Guid.TryParse(request.ProductId, out var requestedProductId))
            throw new NotFoundException($"Product '{request.ProductId}' was not found.");
        var originalProductId = Guid.TryParse(sale.ProductId, out var existingId) ? existingId : Guid.Empty;

        if (requestedProductId != originalProductId)
        {
            var nextProduct = await db.Products.SingleOrDefaultAsync(x => x.Id == requestedProductId, cancellationToken)
                ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");
            if (!nextProduct.IsActive || nextProduct.IsSold)
                throw new ConflictException("This product has already been sold or is inactive.");
            if (await db.SalesProducts.AnyAsync(x => x.Id != request.Id
                    && x.ProductId == requestedProductId.ToString(), cancellationToken))
                throw new ConflictException($"A sales product with product ID '{request.ProductId}' already exists.");

            nextProduct.MarkSold();
            if (originalProductId != Guid.Empty)
            {
                var previousProduct = await db.Products.SingleOrDefaultAsync(x => x.Id == originalProductId, cancellationToken);
                previousProduct?.MarkAvailable();
            }
        }

        if (await db.SalesReceipts.AnyAsync(receipt => receipt.SalesProductId == sale.Id, cancellationToken))
            throw new ConflictException("A sale with recorded receipts cannot be edited.");
        var stock = await db.Products.SingleOrDefaultAsync(product => product.Id == requestedProductId, cancellationToken)
            ?? throw new NotFoundException("The sale's stock record was not found.");
        if (request.SaleDate < stock.PurchaseDate)
            throw new ConflictException("A sale cannot precede the stock purchase date.");
        var customer = await CustomerResolver.GetOrCreateAsync(db, request.CustomerName,
            request.CustomerMobile, request.CustomerAddress, request.CustomerEmail, cancellationToken);
        var tax = request.TaxId.HasValue && request.TaxId.Value != Guid.Empty
            ? await db.Taxes.SingleOrDefaultAsync(item => item.Id == request.TaxId.Value && item.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Tax rate '{request.TaxId}' was not found or is inactive.")
            : null;
        sale.Update(requestedProductId.ToString(), customer.Id, request.SaleDate, decimal.Round(stock.PurchasePrice - stock.Discount, 2, MidpointRounding.AwayFromZero),
            request.SellingPrice, request.Discount, tax?.Id, tax?.Cgst ?? 0m, tax?.Sgst ?? 0m,
            request.PaymentTermsDays);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed while the sale was being saved. Refresh and try again.");
        }
        return (await SalesProductSummaryMapper.MapAsync(db, [sale], cancellationToken))[0];
    }
}
