using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkUpdateProducts;

public sealed class BulkUpdateProductsCommandHandler(IAccountingInventoryDbContext dbContext)
    : IRequestHandler<BulkUpdateProductsCommand, IReadOnlyList<UpdateProductResult>>
{
    public async Task<IReadOnlyList<UpdateProductResult>> Handle(
        BulkUpdateProductsCommand request, CancellationToken cancellationToken)
    {
        var updates = request.Products;
        var ids = updates.Select(update => update.Id).ToArray();
        if (ids.Distinct().Count() != ids.Length)
            throw new ConflictException("The bulk update contains the same product more than once.");

        var serials = updates
            .SelectMany(update => new[] { update.SerialNumber.Trim().ToLowerInvariant(), update.SerialNumber1!.Trim().ToLowerInvariant() })
            .ToArray();
        var duplicateSerial = serials.GroupBy(serial => serial, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicateSerial is not null)
            throw new ConflictException($"Serial number '{duplicateSerial}' appears more than once in the selected products.");

        var products = await dbContext.Products
            .Where(product => ids.Contains(product.Id))
            .ToListAsync(cancellationToken);
        if (products.Count != ids.Length)
            throw new NotFoundException("One or more selected products could not be found. Refresh the product list and try again.");
        await PurchaseIntegrity.ValidateAsync(dbContext, request.Products.Select(item => new PurchaseReference(item.VendorId, item.BrandId, item.ProductTypeId, item.ProductModelId,
            item.VariantId, item.ColorId, item.BillNumber, item.PurchaseDate ?? products.Single(product => product.Id == item.Id).PurchaseDate, item.PaymentTermsDays)), cancellationToken);

        if (products.Any(product => product.IsSold || !product.IsActive))
            throw new ConflictException("Sold or written-off inventory cannot be edited.");
        var postedProductIds = ids.Select(id => id.ToString()).ToArray();
        if (await dbContext.JournalEntries.AnyAsync(entry => entry.SourceType == "PurchaseProduct"
                && postedProductIds.Contains(entry.SourceId!), cancellationToken))
            throw new ConflictException("One or more purchased products are posted to the ledger and cannot be edited.");

        var existingSerial = await dbContext.Products
            .Where(product => !ids.Contains(product.Id)
                && (serials.Contains(product.SerialNumber.ToLower())
                    || (product.SerialNumber1 != null && serials.Contains(product.SerialNumber1.ToLower()))))
            .Select(product => serials.Contains(product.SerialNumber.ToLower()) ? product.SerialNumber : product.SerialNumber1!)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingSerial is not null)
            throw new ConflictException($"Serial number '{existingSerial}' is already assigned to another product.");

        var vendorIds = products.Select(product => product.VendorId).Distinct().ToArray();
        var billNumbers = products.Where(product => product.BillNumber != null)
            .Select(product => product.BillNumber!.ToLower()).Distinct().ToArray();
        var paidBills = await dbContext.PurchasePayments
            .Where(payment => vendorIds.Contains(payment.VendorId) && billNumbers.Contains(payment.BillNumber.ToLower()))
            .Select(payment => new { payment.VendorId, BillNumber = payment.BillNumber.ToLower() })
            .ToListAsync(cancellationToken);
        var paidBillKeys = paidBills.Select(payment => $"{payment.VendorId}:{payment.BillNumber}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (products.Any(product => product.BillNumber is { } bill
                && paidBillKeys.Contains($"{product.VendorId}:{bill.ToLower()}")))
            throw new ConflictException("Products on a bill with recorded payments cannot be edited.");

        var updatesById = updates.ToDictionary(update => update.Id);
        foreach (var product in products)
        {
            var update = updatesById[product.Id];
            product.Update(update.VendorId, update.BrandId, update.ProductTypeId, update.ProductModelId,
                update.VariantId, update.ColorId, update.SerialNumber, update.SerialNumber1,
                update.BillNumber, update.PurchasePrice, update.Discount, update.Cgst, update.Sgst, update.Tax, update.PurchaseDate, update.PaymentTermsDays);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return products.Select(product => new UpdateProductResult(product.Id, product.VendorId,
            product.BrandId, product.ProductTypeId, product.ProductModelId, product.VariantId,
            product.ColorId, product.SerialNumber, product.SerialNumber1,
            product.PurchasePrice, product.TotalAmount, product.Discount, product.Cgst,
            product.Sgst, product.Tax, product.IsActive, product.IsSold, product.BillNumber)).ToArray();
    }
}
