using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Adjustments.Queries.GetPhysicalStockAudit;

public sealed class GetPhysicalStockAuditQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetPhysicalStockAuditQuery, PhysicalStockAuditSummaryDto>
{
    public async Task<PhysicalStockAuditSummaryDto> Handle(GetPhysicalStockAuditQuery request, CancellationToken ct)
    {
        var godown = await db.Godowns.AsNoTracking().SingleOrDefaultAsync(g => g.Id == request.GodownId, ct)
            ?? throw new NotFoundException($"Godown with ID '{request.GodownId}' not found.");

        var isPrimary = godown.IsPrimary;

        // Query serialized products in this godown
        var productsQuery = db.Products.AsNoTracking().Where(p => !p.IsSold && p.IsActive);
        if (isPrimary)
        {
            productsQuery = productsQuery.Where(p => p.GodownId == request.GodownId || p.GodownId == null);
        }
        else
        {
            productsQuery = productsQuery.Where(p => p.GodownId == request.GodownId);
        }

        var products = await (
            from p in productsQuery
            join brand in db.Brands.AsNoTracking() on p.BrandId equals brand.Id into bJoin
            from b in bJoin.DefaultIfEmpty()
            join model in db.ProductModels.AsNoTracking() on p.ProductModelId equals model.Id into mJoin
            from m in mJoin.DefaultIfEmpty()
            join variant in db.Variants.AsNoTracking() on p.VariantId equals variant.Id into vJoin
            from v in vJoin.DefaultIfEmpty()
            select new
            {
                p.Id,
                BrandName = b != null ? b.Name : "",
                ModelName = m != null ? m.Name : "",
                VariantName = v != null ? v.Name : "",
                p.SerialNumber,
                p.SerialNumber1,
                p.PurchasePrice
            }
        ).ToListAsync(ct);

        var items = products.Select(p => new StockAuditItemDto(
            p.Id,
            null,
            "Serialized",
            $"{p.BrandName} {p.ModelName} {p.VariantName}".Trim(),
            p.SerialNumber,
            p.SerialNumber1,
            1m,
            p.PurchasePrice,
            p.PurchasePrice
        )).ToList();

        // Query SKU items
        var skuBalances = await (
            from b in db.StockBalances.AsNoTracking()
            where b.GodownId == request.GodownId && b.ItemType == "Sku" && b.Quantity > 0 && b.IsActive
            join s in db.StockSkus.AsNoTracking() on b.SkuId equals s.Id
            select new
            {
                SkuId = s.Id,
                s.Name,
                s.Code,
                b.Quantity,
                UnitCost = s.Quantity > 0 ? (s.InventoryValue / s.Quantity) : 0m
            }
        ).ToListAsync(ct);

        foreach (var s in skuBalances)
        {
            var unitCost = decimal.Round(s.UnitCost, 2, MidpointRounding.AwayFromZero);
            items.Add(new StockAuditItemDto(
                null,
                s.SkuId,
                "Sku",
                $"{s.Name} ({s.Code})",
                null,
                null,
                s.Quantity,
                unitCost,
                decimal.Round(s.Quantity * unitCost, 2, MidpointRounding.AwayFromZero)
            ));
        }

        var totalQty = items.Sum(i => i.BookQuantity);
        var totalVal = items.Sum(i => i.TotalBookValue);

        return new PhysicalStockAuditSummaryDto(
            godown.Id,
            godown.Name,
            godown.Code,
            godown.IsPrimary,
            items.Count,
            totalQty,
            totalVal,
            items
        );
    }
}

