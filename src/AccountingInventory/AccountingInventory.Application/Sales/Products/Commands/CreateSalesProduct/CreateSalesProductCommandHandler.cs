using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Sales.Products.Commands.CreateSalesProduct;

public sealed class CreateSalesProductCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateSalesProductCommand, SalesProductSummary>
{
    public async Task<SalesProductSummary> Handle(CreateSalesProductCommand request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProductId, out var productId))
            throw new NotFoundException($"Product '{request.ProductId}' was not found.");

        var product = await db.Products.SingleOrDefaultAsync(x => x.Id == productId, cancellationToken)
            ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");
        if (!product.IsActive || product.IsSold)
            throw new ConflictException("This product has already been sold or is inactive.");
        if (await db.SalesProducts.AnyAsync(x => x.ProductId == productId.ToString() && !x.IsReturned, cancellationToken))
            throw new ConflictException($"A sales product with product ID '{request.ProductId}' already exists.");

        if (request.SaleDate < product.PurchaseDate)
            throw new ConflictException("A sale cannot precede the stock purchase date.");
        var previousSales = await db.SalesProducts.Where(x => x.ProductId == productId.ToString()).Select(x => x.Id).ToArrayAsync(cancellationToken);
        if (await db.InvoiceCorrections.AnyAsync(x => x.Kind == "Sale" && previousSales.Contains(x.SourceId) && x.NoteDate > request.SaleDate, cancellationToken))
            throw new ConflictException("A resale cannot precede the previous customer return.");
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        var tax = request.TaxId.HasValue && request.TaxId.Value != Guid.Empty
            ? await db.Taxes.SingleOrDefaultAsync(item => item.Id == request.TaxId.Value && item.IsActive, cancellationToken)
                ?? throw new NotFoundException($"Tax rate '{request.TaxId}' was not found or is inactive.")
            : null;

        var customer = await CustomerResolver.GetOrCreateAsync(db, request.CustomerName,
            request.CustomerMobile, request.CustomerAddress, request.CustomerEmail, cancellationToken);
        var billNumber = await db.GenerateSalesBillNumberAsync(request.SaleDate.Year, cancellationToken);
        product.MarkSold();
        var sale = SalesProduct.Create(billNumber, productId.ToString(),
            customer.Id, request.SaleDate,
            decimal.Round(product.PurchasePrice - product.Discount, 2, MidpointRounding.AwayFromZero), request.SellingPrice, request.Discount,
            tax?.Id, tax?.Cgst ?? 0m, tax?.Sgst ?? 0m, request.PaymentTermsDays);
        db.SalesProducts.Add(sale);
        LedgerPosting.Add(db, LedgerPosting.ForSale(sale, product, ledgerAccounts));
        await LedgerPosting.EnsurePeriodOpenAsync(db, sale.SaleDate, cancellationToken);
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
