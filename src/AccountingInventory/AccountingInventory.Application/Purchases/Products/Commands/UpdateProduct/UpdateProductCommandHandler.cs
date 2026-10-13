using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandHandler(
    IAccountingInventoryDbContext dbContext,
    ILogger<UpdateProductCommandHandler> logger) : IRequestHandler<UpdateProductCommand, UpdateProductResult>
{
    public async Task<UpdateProductResult> Handle(UpdateProductCommand request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"A Product with ID '{request.Id}' was not found.");
        await PurchaseIntegrity.ValidateAsync(dbContext, [new PurchaseReference(request.VendorId, request.BrandId, request.ProductTypeId, request.ProductModelId,
            request.VariantId, request.ColorId, request.BillNumber, request.PurchaseDate ?? product.PurchaseDate, request.PaymentTermsDays)], cancellationToken);

        if (await dbContext.JournalEntries.AnyAsync(entry => entry.SourceType == "PurchaseProduct"
                && entry.SourceId == product.Id.ToString(), cancellationToken))
            throw new ConflictException("This purchased product is posted to the ledger and cannot be edited. Record a purchase adjustment instead.");

        if (product.IsSold)
            throw new ConflictException("Sold inventory cannot be edited.");

        if (product.BillNumber is { } paidBill && await dbContext.PurchasePayments.AnyAsync(payment =>
                payment.VendorId == product.VendorId && payment.BillNumber.ToLower() == paidBill.ToLower(), cancellationToken))
            throw new ConflictException("Products on a bill with recorded payments cannot be edited.");

        var serialNumber = request.SerialNumber.Trim().ToLowerInvariant();
        var serialNumber1 = request.SerialNumber1!.Trim().ToLowerInvariant();
        if (string.Equals(serialNumber, serialNumber1, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Serial Number and Serial Number 1 must be different.");
        if (await dbContext.Products.AnyAsync(x => x.Id != request.Id
            && (x.SerialNumber.ToLower() == serialNumber || x.SerialNumber.ToLower() == serialNumber1
                || (x.SerialNumber1 != null && x.SerialNumber1.ToLower() == serialNumber) || (x.SerialNumber1 != null && x.SerialNumber1.ToLower() == serialNumber1)), cancellationToken))
            throw new ConflictException("A serial number already exists on another product.");

        product.Update(request.VendorId, request.BrandId, request.ProductTypeId, request.ProductModelId,
            request.VariantId, request.ColorId, request.SerialNumber, request.SerialNumber1, request.BillNumber,
            request.PurchasePrice, request.Discount, request.Cgst, request.Sgst, request.Tax, request.PurchaseDate,
            request.PaymentTermsDays, request.HsnSac ?? (request.ProductTypeId == product.ProductTypeId ? product.HsnSac : await dbContext.ProductTypes.Where(t => t.Id == request.ProductTypeId).Select(t => t.HsnSac).FirstOrDefaultAsync(cancellationToken)));

        if (request.IsActive.HasValue)
        {
            product.SetActive(request.IsActive.Value);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Product {ProductId} ({SerialNumber}) updated successfully.", product.Id, product.SerialNumber);
        return new UpdateProductResult(product.Id, product.VendorId, product.BrandId, product.ProductTypeId,
            product.ProductModelId, product.VariantId, product.ColorId, product.SerialNumber, product.SerialNumber1,
            product.PurchasePrice, product.TotalAmount, product.Discount, product.Cgst, product.Sgst,
            product.Tax, product.IsActive, product.IsSold, product.BillNumber, product.HsnSac);
    }
}
