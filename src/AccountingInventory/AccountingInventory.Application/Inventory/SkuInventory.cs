using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
namespace AccountingInventory.Application.Inventory;
public sealed record SkuSummary(Guid Id, string Code, string Name, string HsnSac, string UnitOfMeasure, decimal Quantity, decimal InventoryValue, bool IsActive);
public sealed record SkuPurchaseSummary(Guid Id,string BillNumber,string Description,decimal Quantity,DateOnly Date);
public sealed record GetSkuPurchasesQuery : IRequest<IReadOnlyList<SkuPurchaseSummary>>;
public sealed record GetStockSkusQuery : IRequest<IReadOnlyList<SkuSummary>>;
public sealed record CreateStockSkuCommand(string Code, string Name, string HsnSac, string UnitOfMeasure) : IRequest<Guid>, IBusinessPermissionRequest
{ public string BusinessPermission => "catalog.manage"; }
public sealed record ReceiveSkuStockCommand(Guid SkuId, Guid VendorId, string BillNumber, DateOnly Date, decimal Quantity, decimal UnitCost,
    Guid? TaxId = null, bool InterState = false, int PaymentTermsDays = 0) : IRequest<Guid>, IBusinessPermissionRequest
{ public string BusinessPermission => "purchases.manage"; }
public sealed record StageOpeningSkuStockCommand(Guid SkuId,DateOnly Date,decimal Quantity,decimal UnitCost) : IRequest<Guid>,IBusinessPermissionRequest
{ public string BusinessPermission => "accounting.manage"; }
public sealed record WriteOffSkuStockCommand(Guid SkuId, DateOnly Date, decimal Quantity, string Reason, Guid? ApprovalId = null) : IRequest<Guid>;
public sealed class SkuInventoryHandler(IAccountingInventoryDbContext db) : IRequestHandler<GetStockSkusQuery, IReadOnlyList<SkuSummary>>,
    IRequestHandler<StageOpeningSkuStockCommand,Guid>, IRequestHandler<GetSkuPurchasesQuery, IReadOnlyList<SkuPurchaseSummary>>, IRequestHandler<CreateStockSkuCommand, Guid>, IRequestHandler<ReceiveSkuStockCommand, Guid>, IRequestHandler<WriteOffSkuStockCommand, Guid>
{
    public async Task<IReadOnlyList<SkuPurchaseSummary>> Handle(GetSkuPurchasesQuery q,CancellationToken ct)
        => await db.SkuMovements.Where(m => m.Kind == "Purchase" && !db.InvoiceCorrections.Any(n => n.Kind == "Purchase" && n.SourceId == m.Id))
            .OrderByDescending(m => m.MovementDate).Select(m => new SkuPurchaseSummary(m.Id,m.BillNumber!,m.Description,m.Quantity,m.MovementDate)).ToListAsync(ct);
    public async Task<IReadOnlyList<SkuSummary>> Handle(GetStockSkusQuery q, CancellationToken ct)
        => await db.StockSkus.AsNoTracking().OrderBy(s => s.Code).Select(s => new SkuSummary(s.Id,s.Code,s.Name,s.HsnSac,s.UnitOfMeasure,s.Quantity,s.InventoryValue,s.IsActive)).ToListAsync(ct);
    public async Task<Guid> Handle(CreateStockSkuCommand q, CancellationToken ct)
    {
        var sku = StockSku.Create(q.Code,q.Name,q.HsnSac,q.UnitOfMeasure);
        if (await db.StockSkus.AnyAsync(s => s.Code == sku.Code,ct)) throw new ConflictException("SKU code already exists.");
        db.StockSkus.Add(sku); await db.SaveChangesAsync(ct); return sku.Id;
    }
    public async Task<Guid> Handle(ReceiveSkuStockCommand q, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db,q.Date,ct);
        if (string.IsNullOrWhiteSpace(q.BillNumber) || q.BillNumber.Length > 100 || q.UnitCost < 0 || decimal.Round(q.UnitCost,2) != q.UnitCost
            || q.PaymentTermsDays < 0 || q.PaymentTermsDays > 3650) throw new ArgumentException("Valid supplier bill, cost and payment terms are required.");
        var vendor = await db.Vendors.SingleOrDefaultAsync(v => v.Id == q.VendorId && v.IsActive,ct) ?? throw new NotFoundException("Active vendor not found.");
        var sku = await db.StockSkus.SingleOrDefaultAsync(s => s.Id == q.SkuId,ct) ?? throw new NotFoundException("SKU not found.");
        var bill = q.BillNumber.Trim();
        if (await db.PurchasePayments.AnyAsync(p => p.VendorId == q.VendorId && p.BillNumber.ToLower() == bill.ToLower(),ct)
            || await db.InvoiceCorrections.AnyAsync(n => n.Kind == "Purchase" && n.PartyId == q.VendorId && n.BillNumber.ToLower() == bill.ToLower(),ct))
            throw new ConflictException("Cannot add stock to a supplier bill that already has payments or returns.");
        if(await AccountingInventory.Application.Purchases.Accounting.PurchaseStockRows.Query(db).AnyAsync(p => p.VendorId == q.VendorId && p.BillNumber != null
            && p.BillNumber.ToLower() == bill.ToLower() && (p.PurchaseDate != q.Date || p.PaymentTermsDays != q.PaymentTermsDays),ct))
            throw new ConflictException("All items on a supplier bill require the same date and payment terms.");
        Tax? tax = null;
        if (q.TaxId.HasValue) tax = await db.Taxes.SingleOrDefaultAsync(t => t.Id == q.TaxId && t.IsActive,ct) ?? throw new NotFoundException("Active tax not found.");
        var value = decimal.Round(q.Quantity*q.UnitCost,2,MidpointRounding.AwayFromZero);
        sku.Receive(q.Quantity,value,q.Date);
        var movement = SkuMovement.Create(sku.Id,q.Date,"Purchase",Guid.NewGuid(),q.Quantity,value,sku.Name,q.VendorId,bill,q.PaymentTermsDays,
            q.InterState ? 0 : tax?.Cgst ?? 0,q.InterState ? 0 : tax?.Sgst ?? 0,q.InterState ? (tax?.Cgst ?? 0)+(tax?.Sgst ?? 0) : 0);
        db.SkuMovements.Add(movement);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db,ct);
        var lines = new List<(Guid,decimal,decimal,string?)>();
        if(value>0) lines.Add((accounts["1200"],value,0,"Accessory inventory"));
        var gst=movement.CgstAmount+movement.SgstAmount+movement.IgstAmount;
        if(gst>0) lines.Add((accounts["2200"],gst,0,"Input GST"));
        if(movement.TotalAmount>0) { lines.Add((accounts["2000"],0,movement.TotalAmount,"Supplier payable"));
            LedgerPosting.Add(db,JournalEntry.Post(q.Date,$"Accessory purchase {bill}","SkuPurchase",movement.Id.ToString(),lines)); }
        db.InvoiceSnapshots.Add(InvoiceSnapshot.Capture("Purchase",movement.Id,vendor.Name,vendor.Mobile,vendor.Address ?? "",vendor.Email,sku.Name,sku.Code,System.Text.Json.JsonSerializer.Serialize(new {sku.Code,sku.Name,sku.HsnSac,sku.UnitOfMeasure,q.Quantity,q.UnitCost,bill,movement.CgstAmount,movement.SgstAmount,movement.IgstAmount,movement.TotalAmount})));
        await db.SaveChangesAsync(ct); return movement.Id;
    }
    public async Task<Guid> Handle(StageOpeningSkuStockCommand q,CancellationToken ct)
    {
        if(await db.JournalEntries.AnyAsync(e => e.SourceType == "OpeningBalances",ct)) throw new ConflictException("Stage opening SKU stock before the cutover journal.");
        if(q.UnitCost<0 || decimal.Round(q.UnitCost,2)!=q.UnitCost) throw new ArgumentException("Opening unit cost must have two decimals.");
        var sku=await db.StockSkus.SingleOrDefaultAsync(s => s.Id == q.SkuId,ct) ?? throw new NotFoundException("SKU not found.");
        if(await db.SkuMovements.AnyAsync(m => m.SkuId == sku.Id,ct)) throw new ConflictException("Opening staging requires a new SKU with no movements.");
        var value=decimal.Round(q.Quantity*q.UnitCost,2,MidpointRounding.AwayFromZero);sku.Receive(q.Quantity,value,q.Date);sku.StageOpening();
        var movement=SkuMovement.Create(sku.Id,q.Date,"OpeningStock",Guid.NewGuid(),q.Quantity,value,"Staged opening accessory stock");
        db.SkuMovements.Add(movement);await db.SaveChangesAsync(ct);return movement.Id;
    }
    public async Task<Guid> Handle(WriteOffSkuStockCommand q, CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(q.Reason) || q.Reason.Length>300) throw new ArgumentException("Write-off reason is required.");
        await ApprovalGate.ValidateAsync(db,q.ApprovalId,ApprovalAction.InventoryWriteOff,q.SkuId.ToString(),new {skuId=q.SkuId,date=q.Date,quantity=q.Quantity,reason=q.Reason},ct);
        await LedgerPosting.EnsurePeriodOpenAsync(db,q.Date,ct);
        var sku = await db.StockSkus.SingleOrDefaultAsync(s => s.Id == q.SkuId,ct) ?? throw new NotFoundException("SKU not found.");
        var cost=sku.Issue(q.Quantity,q.Date); var movement=SkuMovement.Create(sku.Id,q.Date,"WriteOff",Guid.NewGuid(),-q.Quantity,-cost,q.Reason);
        db.SkuMovements.Add(movement); var accounts=await LedgerPosting.EnsureSystemAccountsAsync(db,ct);
        if(cost>0) LedgerPosting.Add(db,JournalEntry.Post(q.Date,q.Reason,"SkuWriteOff",movement.Id.ToString(),[(accounts["5100"],cost,0m,(string?)q.Reason),(accounts["1200"],0m,cost,(string?)"Accessory write-off")]));
        await db.SaveChangesAsync(ct);return movement.Id;
    }
}
