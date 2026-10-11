using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.GeneralLedger;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkCreateProducts;

public sealed class BulkCreateProductsCommandHandler(IAccountingInventoryDbContext dbContext, ILogger<BulkCreateProductsCommandHandler> logger)
    : IRequestHandler<BulkCreateProductsCommand, IReadOnlyList<CreateProductResult>>
{
    public async Task<IReadOnlyList<CreateProductResult>> Handle(
        BulkCreateProductsCommand request, CancellationToken cancellationToken)
    {
        await PurchaseIntegrity.ValidateAsync(dbContext, request.Products.Select(item => new PurchaseReference(item.VendorId, item.BrandId, item.ProductTypeId, item.ProductModelId,
            item.VariantId, item.ColorId, item.BillNumber, item.PurchaseDate, item.PaymentTermsDays)), cancellationToken);
        var products = request.Products;
        var serials = products.SelectMany(x => new[] { x.SerialNumber.Trim().ToLowerInvariant(), x.SerialNumber1!.Trim().ToLowerInvariant() }).ToArray();
        var duplicate = serials.GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate is not null)
            throw new ConflictException($"Serial number '{duplicate}' appears more than once in this purchase.");

        var existing = await dbContext.Products
            .Where(x => serials.Contains(x.SerialNumber.ToLower()) || (x.SerialNumber1 != null && serials.Contains(x.SerialNumber1.ToLower())))
            .Select(x => serials.Contains(x.SerialNumber.ToLower()) ? x.SerialNumber : x.SerialNumber1!)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Serial number '{existing}' is already assigned to another product.");

        var entities = products.Select(item => Product.Create(item.VendorId, item.BrandId,
            item.ProductTypeId, item.ProductModelId, item.VariantId, item.ColorId,
            item.SerialNumber, item.SerialNumber1, item.BillNumber, item.PurchasePrice,
            item.Discount, item.Cgst, item.Sgst, item.Tax, item.PurchaseDate, item.PaymentTermsDays)).ToArray();
        dbContext.Products.AddRange(entities);
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(dbContext, cancellationToken);
        foreach (var product in entities)
            LedgerPosting.Add(dbContext, LedgerPosting.ForPurchase(product, ledgerAccounts));
        await LedgerPosting.EnsurePeriodsOpenAsync(dbContext, entities.Select(product => product.PurchaseDate), cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Added {ProductCount} products with serial numbers {SerialNumbers}.",
            entities.Length, string.Join(", ", entities.Select(product => product.SerialNumber)));

        return entities.Select(product => new CreateProductResult(product.Id, product.VendorId,
            product.BrandId, product.ProductTypeId, product.ProductModelId, product.VariantId,
            product.ColorId, product.SerialNumber, product.SerialNumber1,
            product.PurchasePrice, product.TotalAmount, product.Discount, product.Cgst,
            product.Sgst, product.Tax, product.IsActive, product.IsSold, product.BillNumber)).ToArray();
    }
}
