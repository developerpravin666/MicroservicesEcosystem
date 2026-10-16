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
    : IRequestHandler<BulkCreateProductsCommand, BulkCreateProductsResult>
{
    public async Task<BulkCreateProductsResult> Handle(
        BulkCreateProductsCommand request, CancellationToken cancellationToken)
    {
        var resolver = await AccountingInventory.Application.Taxes.TaxRateResolver.LoadAsync(dbContext, cancellationToken);
        var defaults = await dbContext.ProductTypes.ToDictionaryAsync(t => t.Id, t => t.DefaultTaxId, cancellationToken);
        request = request with { Products = request.Products.Select(item => {
            var rate = resolver.Resolve(item.TaxId ?? defaults.GetValueOrDefault(item.ProductTypeId), item.PurchaseDate ?? DateOnly.FromDateTime(DateTime.UtcNow));
            return rate == null ? item : item with { TaxId = rate.Id, Cgst = rate.Cgst, Sgst = rate.Sgst, Tax = rate.TotalTax, TaxTreatment = rate.Treatment };
        }).ToArray() };

        var products = request.Products;

        // Helper to extract non-empty, trimmed serial numbers
        static List<string> GetNonEmptySerials(CreateProductCommand p)
        {
            var list = new List<string>(2);
            if (!string.IsNullOrWhiteSpace(p.SerialNumber))
                list.Add(p.SerialNumber.Trim());
            if (!string.IsNullOrWhiteSpace(p.SerialNumber1))
                list.Add(p.SerialNumber1.Trim());
            return list;
        }

        // Collect all non-empty incoming serials
        var allIncomingSerials = products
            .SelectMany(GetNonEmptySerials)
            .Select(s => s.ToLowerInvariant())
            .Distinct()
            .ToList();

        // Query existing serial numbers in DB matching incoming serials
        var existingDbRows = await dbContext.Products
            .Where(x => allIncomingSerials.Contains(x.SerialNumber.ToLower()) ||
                        (x.SerialNumber1 != null && allIncomingSerials.Contains(x.SerialNumber1.ToLower())))
            .Select(x => new { SerialNumber = x.SerialNumber.ToLower(), SerialNumber1 = x.SerialNumber1 != null ? x.SerialNumber1.ToLower() : null })
            .ToListAsync(cancellationToken);

        var existingDbSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in existingDbRows)
        {
            if (!string.IsNullOrWhiteSpace(r.SerialNumber)) existingDbSet.Add(r.SerialNumber);
            if (!string.IsNullOrWhiteSpace(r.SerialNumber1)) existingDbSet.Add(r.SerialNumber1);
        }

        // Partition into valid and duplicate entries
        var seenBatchSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validProducts = new List<CreateProductCommand>();
        var duplicates = new List<DuplicateProductDto>();

        foreach (var item in products)
        {
            var itemSerials = GetNonEmptySerials(item);
            var dbConflict = itemSerials.FirstOrDefault(s => existingDbSet.Contains(s));
            var batchConflict = itemSerials.FirstOrDefault(s => seenBatchSerials.Contains(s));

            if (dbConflict != null)
            {
                duplicates.Add(new DuplicateProductDto(
                    item.SerialNumber,
                    item.SerialNumber1,
                    item.BillNumber,
                    $"Serial number '{dbConflict}' already exists in inventory database."
                ));
            }
            else if (batchConflict != null)
            {
                duplicates.Add(new DuplicateProductDto(
                    item.SerialNumber,
                    item.SerialNumber1,
                    item.BillNumber,
                    $"Serial number '{batchConflict}' appears more than once in this invoice purchase."
                ));
            }
            else
            {
                // Serial is unique: track in batch and queue for insertion
                foreach (var s in itemSerials)
                {
                    seenBatchSerials.Add(s);
                }
                validProducts.Add(item);
            }
        }

        var insertedResults = new List<CreateProductResult>();

        // Insert only the valid products into database
        if (validProducts.Count > 0)
        {
            await PurchaseIntegrity.ValidateAsync(dbContext, validProducts.Select(item => new PurchaseReference(
                item.VendorId, item.BrandId, item.ProductTypeId, item.ProductModelId,
                item.VariantId, item.ColorId, item.BillNumber, item.PurchaseDate, item.PaymentTermsDays)), cancellationToken);

            var typeCodes = await dbContext.ProductTypes.ToDictionaryAsync(t => t.Id, t => t.HsnSac, cancellationToken);
            var entities = validProducts.Select(item => Product.Create(item.VendorId, item.BrandId,
                item.ProductTypeId, item.ProductModelId, item.VariantId, item.ColorId,
                item.SerialNumber, item.SerialNumber1, item.BillNumber, item.PurchasePrice,
                item.Discount, item.Cgst, item.Sgst, item.Tax, item.PurchaseDate, item.PaymentTermsDays, item.HsnSac ?? typeCodes.GetValueOrDefault(item.ProductTypeId))).ToArray();

            for (var i = 0; i < entities.Length; i++)
                entities[i].SetTaxDetails(validProducts[i].TaxId == Guid.Empty ? null : validProducts[i].TaxId, validProducts[i].TaxTreatment);

            dbContext.Products.AddRange(entities);
            var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(dbContext, cancellationToken);
            foreach (var product in entities)
                LedgerPosting.Add(dbContext, LedgerPosting.ForPurchase(product, ledgerAccounts));

            await LedgerPosting.EnsurePeriodsOpenAsync(dbContext, entities.Select(product => product.PurchaseDate), cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Bulk purchase: Inserted {ProductCount} valid products with serials {SerialNumbers}; Skipped {DuplicateCount} duplicates.",
                entities.Length, string.Join(", ", entities.Select(product => product.SerialNumber)), duplicates.Count);

            insertedResults = entities.Select(product => new CreateProductResult(product.Id, product.VendorId,
                product.BrandId, product.ProductTypeId, product.ProductModelId, product.VariantId,
                product.ColorId, product.SerialNumber, product.SerialNumber1,
                product.PurchasePrice, product.TotalAmount, product.Discount, product.Cgst,
                product.Sgst, product.Tax, product.IsActive, product.IsSold, product.BillNumber, product.HsnSac, product.TaxId, product.TaxTreatment)).ToList();
        }
        else
        {
            logger.LogWarning("Bulk purchase: All {Count} incoming products had duplicate serial numbers. None inserted.", products.Count);
        }

        return new BulkCreateProductsResult(
            insertedResults,
            duplicates,
            insertedResults.Count,
            duplicates.Count
        );
    }
}
