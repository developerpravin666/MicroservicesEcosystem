using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Purchases.Products.Commands.DeleteProduct;

public sealed class DeleteProductCommandHandler(
    IAccountingInventoryDbContext dbContext,
    ILogger<DeleteProductCommandHandler> logger) : IRequestHandler<DeleteProductCommand>
{
    public async Task Handle(DeleteProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A Product with ID '{request.Id}' was not found.");
        if (await dbContext.JournalEntries.AnyAsync(entry => entry.SourceType == "PurchaseProduct"
                && entry.SourceId == product.Id.ToString(), cancellationToken))
            throw new ConflictException("This purchased product is posted to the ledger and cannot be deleted. Record a purchase return instead.");
        if (await dbContext.SalesProducts.AnyAsync(x => x.ProductId == product.Id.ToString(), cancellationToken))
            throw new ConflictException("This product has an active sale. Delete the sale before removing the product from inventory.");
        if (product.BillNumber is { } paidBill && await dbContext.PurchasePayments.AnyAsync(payment =>
                payment.VendorId == product.VendorId && payment.BillNumber.ToLower() == paidBill.ToLower(), cancellationToken))
            throw new ConflictException("Products on a bill with recorded payments cannot be deleted.");
        product.Delete();
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Product {ProductId} deleted successfully.", product.Id);
    }
}
