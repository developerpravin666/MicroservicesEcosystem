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
        if (await db.Vendors.CountAsync(row => vendorIds.Contains(row.Id) && row.IsActive, ct) != vendorIds.Length
            || await db.Brands.CountAsync(row => brandIds.Contains(row.Id) && row.IsActive, ct) != brandIds.Length
            || await db.ProductTypes.CountAsync(row => typeIds.Contains(row.Id) && row.IsActive, ct) != typeIds.Length
            || await db.Variants.CountAsync(row => variantIds.Contains(row.Id) && row.IsActive, ct) != variantIds.Length
            || await db.Colors.CountAsync(row => colorIds.Contains(row.Id) && row.IsActive, ct) != colorIds.Length)
            throw new ConflictException("Every purchase reference must be an active record in this tenant.");
        var models = await db.ProductModels.AsNoTracking().Where(row => modelIds.Contains(row.Id) && row.IsActive)
            .ToDictionaryAsync(row => row.Id, ct);
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
