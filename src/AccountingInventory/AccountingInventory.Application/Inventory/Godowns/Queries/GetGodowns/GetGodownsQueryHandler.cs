using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Godowns.Queries.GetGodowns;

public sealed class GetGodownsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetGodownsQuery, IReadOnlyList<GodownDto>>
{
    public async Task<IReadOnlyList<GodownDto>> Handle(GetGodownsQuery request, CancellationToken ct)
    {
        var godowns = await db.Godowns.AsNoTracking().OrderByDescending(g => g.IsPrimary).ThenBy(g => g.Name).ToListAsync(ct);

        if (godowns.Count == 0)
        {
            var defaultGodown = Godown.Create("Main Store / Warehouse", "MAIN", "Default Primary Godown", isPrimary: true);
            db.Godowns.Add(defaultGodown);
            await db.SaveChangesAsync(ct);
            godowns = new List<Godown> { defaultGodown };
        }

        var primaryGodownId = godowns.FirstOrDefault(g => g.IsPrimary)?.Id ?? godowns[0].Id;

        // Fetch active unsold products
        var products = await db.Products.AsNoTracking()
            .Where(p => !p.IsSold && p.IsActive)
            .Select(p => new { p.Id, p.GodownId, p.PurchasePrice })
            .ToListAsync(ct);

        // Fetch SKU balances
        var skuBalances = await db.StockBalances.AsNoTracking()
            .Where(b => b.ItemType == "Sku" && b.IsActive)
            .Select(b => new { b.GodownId, b.SkuId, b.Quantity })
            .ToListAsync(ct);

        var skuUnitCosts = await db.StockSkus.AsNoTracking()
            .Select(s => new { s.Id, UnitCost = s.Quantity > 0 ? (s.InventoryValue / s.Quantity) : 0m })
            .ToDictionaryAsync(s => s.Id, s => s.UnitCost, ct);

        var result = new List<GodownDto>();

        foreach (var g in godowns)
        {
            // Products in this godown (null GodownId maps to Primary Godown)
            var godownProducts = products.Where(p => (p.GodownId == g.Id) || (p.GodownId == null && g.Id == primaryGodownId)).ToList();
            var serializedCount = godownProducts.Count;
            var serializedValue = godownProducts.Sum(p => p.PurchasePrice);

            // SKUs in this godown
            var godownSkus = skuBalances.Where(b => b.GodownId == g.Id).ToList();
            var skuQty = godownSkus.Sum(b => b.Quantity);
            var skuVal = godownSkus.Sum(b => b.Quantity * (b.SkuId.HasValue && skuUnitCosts.TryGetValue(b.SkuId.Value, out var cost) ? cost : 0m));

            result.Add(new GodownDto(
                g.Id,
                g.Name,
                g.Code,
                g.Address,
                g.IsPrimary,
                g.IsActive,
                serializedCount,
                skuQty,
                serializedValue + skuVal));
        }

        return result;
    }
}

