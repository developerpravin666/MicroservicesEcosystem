using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Common;

public static class MasterReferenceIntegrity
{
    public static async Task EnsureDeletableAsync(IAccountingInventoryDbContext db, string kind, Guid id, CancellationToken ct)
    {
        // Include archived source rows: historical invoices must keep their references.
        var products = db.Products.IgnoreQueryFilters();
        var models = db.ProductModels.IgnoreQueryFilters();
        var referenced = kind switch
        {
            "Brand" => await products.AnyAsync(row => row.BrandId == id, ct) || await models.AnyAsync(row => row.BrandId == id, ct),
            "ProductType" => await products.AnyAsync(row => row.ProductTypeId == id, ct) || await models.AnyAsync(row => row.ProductTypeId == id, ct),
            "ProductModel" => await products.AnyAsync(row => row.ProductModelId == id, ct),
            "Variant" => await products.AnyAsync(row => row.VariantId == id, ct),
            "Color" => await products.AnyAsync(row => row.ColorId == id, ct),
            "Vendor" => await products.AnyAsync(row => row.VendorId == id, ct) || await db.PurchasePayments.IgnoreQueryFilters().AnyAsync(row => row.VendorId == id, ct),
            "FinanceVendor" => await db.SalesPayments.IgnoreQueryFilters().AnyAsync(row => row.FinanceVendorId == id, ct),
            "Tax" => await db.SalesProducts.IgnoreQueryFilters().AnyAsync(row => row.TaxId == id, ct),
            "Customer" => await db.SalesProducts.IgnoreQueryFilters().AnyAsync(row => row.CustomerId == id, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        if (referenced) throw new ConflictException($"This {kind} is referenced by inventory or accounting records and cannot be deleted. Deactivate it instead.");
    }
}
