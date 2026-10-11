using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    IAccountingInventoryDbContext dbContext,
    ILogger<CreateProductCommandHandler> logger) : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    public async Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken cancellationToken)
    {
        await PurchaseIntegrity.ValidateAsync(dbContext, [new PurchaseReference(request.VendorId, request.BrandId, request.ProductTypeId, request.ProductModelId,
            request.VariantId, request.ColorId, request.BillNumber, request.PurchaseDate, request.PaymentTermsDays)], cancellationToken);
        var serialNumber = request.SerialNumber.Trim().ToLowerInvariant();
        var serialNumber1 = request.SerialNumber1!.Trim().ToLowerInvariant();
        if (string.Equals(serialNumber, serialNumber1, StringComparison.OrdinalIgnoreCase))
            throw new ConflictException("Serial Number and Serial Number 1 must be different.");
        if (await dbContext.Products.AnyAsync(x => x.SerialNumber.ToLower() == serialNumber || x.SerialNumber.ToLower() == serialNumber1
            || (x.SerialNumber1 != null && x.SerialNumber1.ToLower() == serialNumber) || (x.SerialNumber1 != null && x.SerialNumber1.ToLower() == serialNumber1), cancellationToken))
            throw new ConflictException("A serial number already exists on another product.");

        var product = Product.Create(request.VendorId, request.BrandId, request.ProductTypeId, request.ProductModelId,
            request.VariantId, request.ColorId, request.SerialNumber, request.SerialNumber1, request.BillNumber,
            request.PurchasePrice, request.Discount, request.Cgst, request.Sgst, request.Tax, request.PurchaseDate,
            request.PaymentTermsDays);
        dbContext.Products.Add(product);
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(dbContext, cancellationToken);
        LedgerPosting.Add(dbContext, LedgerPosting.ForPurchase(product, ledgerAccounts));
        await LedgerPosting.EnsurePeriodOpenAsync(dbContext, product.PurchaseDate, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Product {ProductId} ({SerialNumber}) added successfully.", product.Id, product.SerialNumber);
        return new CreateProductResult(product.Id, product.VendorId, product.BrandId, product.ProductTypeId,
            product.ProductModelId, product.VariantId, product.ColorId, product.SerialNumber, product.SerialNumber1,
            product.PurchasePrice, product.TotalAmount, product.Discount, product.Cgst, product.Sgst,
            product.Tax, product.IsActive, product.IsSold, product.BillNumber);
    }
}
