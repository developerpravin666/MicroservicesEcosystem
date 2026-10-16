using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Transfers.Queries.GetStockTransfers;

public sealed class GetStockTransfersQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockTransfersQuery, IReadOnlyList<StockTransferSummaryDto>>
{
    public async Task<IReadOnlyList<StockTransferSummaryDto>> Handle(GetStockTransfersQuery request, CancellationToken ct)
    {
        var godowns = await db.Godowns.AsNoTracking().ToDictionaryAsync(g => g.Id, g => g.Name, ct);

        var query = db.StockTransferJournals.AsNoTracking();

        if (request.GodownId.HasValue)
        {
            var gid = request.GodownId.Value;
            query = query.Where(t => t.FromGodownId == gid || t.ToGodownId == gid);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(t => t.TransferDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(t => t.TransferDate <= request.ToDate.Value);
        }

        var transfers = await query
            .OrderByDescending(t => t.TransferDate)
            .ThenByDescending(t => t.CreatedAt)
            .Select(t => new
            {
                t.Id,
                t.TransferNumber,
                t.TransferDate,
                t.FromGodownId,
                t.ToGodownId,
                t.VehicleNumber,
                t.DispatchNote,
                t.TransporterName,
                t.Narration,
                t.Status,
                t.TotalQuantity,
                t.TotalValue,
                ItemCount = t.Lines.Count
            })
            .ToListAsync(ct);

        return transfers.Select(t => new StockTransferSummaryDto(
            t.Id,
            t.TransferNumber,
            t.TransferDate,
            t.FromGodownId,
            godowns.TryGetValue(t.FromGodownId, out var fromName) ? fromName : "Unknown Godown",
            t.ToGodownId,
            godowns.TryGetValue(t.ToGodownId, out var toName) ? toName : "Unknown Godown",
            t.VehicleNumber,
            t.DispatchNote,
            t.TransporterName,
            t.Narration,
            t.Status,
            t.TotalQuantity,
            t.TotalValue,
            t.ItemCount
        )).ToList();
    }
}

