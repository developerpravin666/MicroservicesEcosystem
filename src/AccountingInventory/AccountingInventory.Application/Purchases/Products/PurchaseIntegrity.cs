using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Purchases.Products;

public sealed record PurchaseReference(Guid VendorId, Guid BrandId, Guid ProductTypeId, Guid ProductModelId,
    Guid VariantId, Guid ColorId, string? BillNumber, DateOnly? PurchaseDate, int PaymentTermsDays);

public static class PurchaseIntegrity
{
    public static async Task ValidateAsync(IAccountingInventoryDbContext db, IEnumerable<PurchaseReference> references,
        CancellationToken ct)
    {
        var rows = references.ToArray();
        var vendorIds = rows.Select(row => row.VendorId).Distinct().ToArray();
        var brandIds = rows.Select(row => row.BrandId).Distinct().ToArray();
        var typeIds = rows.Select(row => row.ProductTypeId).Distinct().ToArray();
        var modelIds = rows.Select(row => row.ProductModelId).Distinct().ToArray();
        var variantIds = rows.Select(row => row.VariantId).Distinct().ToArray();
        var colorIds = rows.Select(row => row.ColorId).Distinct().ToArray();
        var activeVendorIds = await db.Vendors.Where(row => vendorIds.Contains(row.Id) && row.IsActive).Select(r => r.Id).ToListAsync(ct);
        var missingVendors = vendorIds.Except(activeVendorIds).ToArray();
        if (missingVendors.Length > 0)
            throw new ConflictException($"Vendor reference ({string.Join(", ", missingVendors)}) is not an active record in this tenant.");

        var activeBrandIds = await db.Brands.Where(row => brandIds.Contains(row.Id) && row.IsActive).Select(r => r.Id).ToListAsync(ct);
        var missingBrands = brandIds.Except(activeBrandIds).ToArray();
        if (missingBrands.Length > 0)
            throw new ConflictException($"Brand reference ({string.Join(", ", missingBrands)}) is not an active record in this tenant.");

        var activeTypeIds = await db.ProductTypes.Where(row => typeIds.Contains(row.Id) && row.IsActive).Select(r => r.Id).ToListAsync(ct);
        var missingTypes = typeIds.Except(activeTypeIds).ToArray();
        if (missingTypes.Length > 0)
            throw new ConflictException($"Product type reference ({string.Join(", ", missingTypes)}) is not an active record in this tenant.");

        var activeVariantIds = await db.Variants.Where(row => variantIds.Contains(row.Id) && row.IsActive).Select(r => r.Id).ToListAsync(ct);
        var missingVariants = variantIds.Except(activeVariantIds).ToArray();
        if (missingVariants.Length > 0)
            throw new ConflictException($"Variant reference ({string.Join(", ", missingVariants)}) is not an active record in this tenant.");

        var activeColorIds = await db.Colors.Where(row => colorIds.Contains(row.Id) && row.IsActive).Select(r => r.Id).ToListAsync(ct);
        var missingColors = colorIds.Except(activeColorIds).ToArray();
        if (missingColors.Length > 0)
            throw new ConflictException($"Color reference ({string.Join(", ", missingColors)}) is not an active record in this tenant.");

        var models = await db.ProductModels.AsNoTracking().Where(row => modelIds.Contains(row.Id) && row.IsActive)
            .ToDictionaryAsync(row => row.Id, ct);
        var missingModels = modelIds.Except(models.Keys).ToArray();
        if (missingModels.Length > 0)
            throw new ConflictException($"Product model reference ({string.Join(", ", missingModels)}) is not an active record in this tenant.");

        if (rows.Any(row => !models.TryGetValue(row.ProductModelId, out var model)
            || model.BrandId != row.BrandId || model.ProductTypeId != row.ProductTypeId))
            throw new ConflictException("The product model must belong to the selected brand and product type.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var invoices = rows.GroupBy(row => (row.VendorId, Bill: row.BillNumber?.Trim().ToLowerInvariant() ?? ""));
        foreach (var invoice in invoices)
        {
            if (invoice.Key.Bill.Length == 0) throw new ConflictException("A vendor bill number is required.");
            var first = invoice.First();
            var date = first.PurchaseDate ?? today;
            if (invoice.Any(row => (row.PurchaseDate ?? today) != date || row.PaymentTermsDays != first.PaymentTermsDays))
                throw new ConflictException("All units on a vendor bill must have the same invoice date and payment terms.");
            if (await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db).AnyAsync(row => row.VendorId == first.VendorId && row.BillNumber != null
                    && row.BillNumber.ToLower() == invoice.Key.Bill
                    && (row.PurchaseDate != date || row.PaymentTermsDays != first.PaymentTermsDays), ct))
                throw new ConflictException("The invoice date and payment terms differ from this vendor's existing bill.");
            if (await db.PurchasePayments.AnyAsync(row => row.VendorId == first.VendorId
                && row.BillNumber.ToLower() == invoice.Key.Bill, ct))
                throw new ConflictException("Units on a bill with recorded payments cannot be added or edited. Use a separate bill or an accounting adjustment.");
        }
    }
}
