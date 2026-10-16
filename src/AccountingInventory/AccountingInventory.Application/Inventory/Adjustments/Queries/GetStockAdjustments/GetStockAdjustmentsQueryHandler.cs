using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Adjustments.Queries.GetStockAdjustments;

public sealed class GetStockAdjustmentsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockAdjustmentsQuery, IReadOnlyList<StockAdjustmentDto>>
{
    public async Task<IReadOnlyList<StockAdjustmentDto>> Handle(GetStockAdjustmentsQuery request, CancellationToken ct)
    {
        var godowns = await db.Godowns.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var query = db.StockAdjustments.AsNoTracking();

        if (request.GodownId.HasValue)
        {
            query = query.Where(a => a.GodownId == request.GodownId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(a => a.AdjustmentDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(a => a.AdjustmentDate <= request.ToDate.Value);
        }

        var adjustments = await query
            .OrderByDescending(a => a.AdjustmentDate)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new
            {
                a.Id,
                a.AdjustmentNumber,
                a.AdjustmentDate,
                a.GodownId,
                a.AdjustmentType,
                a.Reason,
                a.ItemType,
                a.ItemName,
                a.SerialNumber,
                a.BookQuantity,
                a.PhysicalQuantity,
                a.DiscrepancyQuantity,
                a.UnitCost,
                a.TotalValue,
                a.JournalEntryId
            })
            .ToListAsync(ct);

        return adjustments.Select(a => new StockAdjustmentDto(
            a.Id,
            a.AdjustmentNumber,
            a.AdjustmentDate,
            a.GodownId,
            godowns.TryGetValue(a.GodownId, out var gName) ? gName : "Unknown Godown",
            a.AdjustmentType,
            a.Reason,
            a.ItemType,
            a.ItemName,
            a.SerialNumber,
            a.BookQuantity,
            a.PhysicalQuantity,
            a.DiscrepancyQuantity,
            a.UnitCost,
            a.TotalValue,
            a.JournalEntryId
        )).ToList();
    }
}

