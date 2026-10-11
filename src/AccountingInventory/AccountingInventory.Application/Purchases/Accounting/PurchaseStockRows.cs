using AccountingInventory.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
namespace AccountingInventory.Application.Purchases.Accounting;
public sealed class PurchaseStockRow
{
    public bool IsDeleted { get; set; }
    public Guid BrandId { get; set; }
    public Guid ProductModelId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid Id { get; set; }
    public Guid VendorId { get; set; }
    public string? BillNumber { get; set; }
    public DateOnly PurchaseDate { get; set; }
    public DateOnly DueDate { get; set; }
    public int PaymentTermsDays { get; set; }
    public bool IsOpeningStock { get; set; }
    public decimal TotalAmount { get; set; }
}
public static class PurchaseStockRows
{
    public static IQueryable<PurchaseStockRow> Query(IAccountingInventoryDbContext db)
        => db.Products.AsNoTracking().Select(p => new PurchaseStockRow {IsDeleted=false,BrandId=p.BrandId,ProductModelId=p.ProductModelId,CreatedAt=p.CreatedAt,Id=p.Id,VendorId=p.VendorId,BillNumber=p.BillNumber,PurchaseDate=p.PurchaseDate,
            DueDate=p.DueDate,PaymentTermsDays=p.PaymentTermsDays,IsOpeningStock=p.IsOpeningStock,TotalAmount=p.TotalAmount})
        .Concat(db.SkuMovements.AsNoTracking().Where(m => m.Kind == "Purchase").Select(m => new PurchaseStockRow {IsDeleted=false,BrandId=Guid.Empty,ProductModelId=Guid.Empty,CreatedAt=m.CreatedAt,Id=m.Id,VendorId=m.VendorId!.Value,
            BillNumber=m.BillNumber,PurchaseDate=m.MovementDate,DueDate=m.DueDate,PaymentTermsDays=m.PaymentTermsDays,IsOpeningStock=false,TotalAmount=m.TotalAmount}));
}
