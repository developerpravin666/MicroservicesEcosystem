using System.Text.Json;
using AccountingInventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Infrastructure.Persistence;

internal static class InvoiceSnapshotCapture
{
    public static async Task CaptureAsync(AccountingInventoryDbContext db, CancellationToken ct)
    {
        var products = db.ChangeTracker.Entries<Product>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        var sales = db.ChangeTracker.Entries<SalesProduct>().Where(x => x.State == EntityState.Added).Select(x => x.Entity).ToArray();
        if (products.Length + sales.Length == 0) return;
        async Task<string> ProductName(Product p)
        {
            var brand = db.Brands.Local.FirstOrDefault(x => x.Id == p.BrandId)?.Name
                ?? await db.Brands.Where(x => x.Id == p.BrandId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            var model = db.ProductModels.Local.FirstOrDefault(x => x.Id == p.ProductModelId)?.Name
                ?? await db.ProductModels.Where(x => x.Id == p.ProductModelId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            var variant = db.Variants.Local.FirstOrDefault(x => x.Id == p.VariantId)?.Name
                ?? await db.Variants.Where(x => x.Id == p.VariantId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            var color = db.Colors.Local.FirstOrDefault(x => x.Id == p.ColorId)?.Name
                ?? await db.Colors.Where(x => x.Id == p.ColorId).Select(x => x.Name).FirstOrDefaultAsync(ct);
            return string.Join(" - ", new[] { brand, model, variant, color }.Where(x => !string.IsNullOrWhiteSpace(x))) + $" — {p.SerialNumber}";
        }
        foreach (var p in products)
        {
            if (db.InvoiceSnapshots.Local.Any(x => x.Kind == "Purchase" && x.SourceId == p.Id)) continue;
            var vendor = db.Vendors.Local.FirstOrDefault(x => x.Id == p.VendorId)
                ?? await db.Vendors.FindAsync([p.VendorId], ct);
            if (vendor is null) continue;
            db.InvoiceSnapshots.Add(InvoiceSnapshot.Capture("Purchase", p.Id, vendor.Name, vendor.Mobile,
                vendor.Address ?? "", vendor.Email, await ProductName(p), p.SerialNumber, JsonSerializer.Serialize(new {
                    p.BillNumber, p.PurchaseDate, p.DueDate, p.PurchasePrice, p.Discount, p.Cgst, p.Sgst, p.TotalAmount })));
        }
        foreach (var s in sales)
        {
            if (db.InvoiceSnapshots.Local.Any(x => x.Kind == "Sale" && x.SourceId == s.Id)) continue;
            var customer = db.Customers.Local.FirstOrDefault(x => x.Id == s.CustomerId)
                ?? await db.Customers.FindAsync([s.CustomerId], ct);
            var productId = Guid.Parse(s.ProductId);
            var product = db.Products.Local.FirstOrDefault(x => x.Id == productId) ?? await db.Products.FindAsync([productId], ct);
            if (customer is null || product is null) continue;
            var seller = await db.CustomerBillSettings.AsNoTracking().FirstOrDefaultAsync(ct);
            db.InvoiceSnapshots.Add(InvoiceSnapshot.Capture("Sale", s.Id, customer.Name, customer.Mobile, customer.Address,
                customer.Email, await ProductName(product), product.SerialNumber, JsonSerializer.Serialize(new {
                    s.BillNumber, s.SaleDate, s.DueDate, s.SellingPrice, s.Discount, s.CgstRate, s.SgstRate,
                    s.CgstAmount, s.SgstAmount, s.TotalAmount, Seller = seller }, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        }
    }
}
