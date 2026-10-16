using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Transfers.Queries.GetStockTransferById;

public sealed class GetStockTransferByIdQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetStockTransferByIdQuery, StockTransferDetailDto>
{
    public async Task<StockTransferDetailDto> Handle(GetStockTransferByIdQuery request, CancellationToken ct)
    {
        var transfer = await db.StockTransferJournals.AsNoTracking()
            .Include(t => t.Lines)
            .SingleOrDefaultAsync(t => t.Id == request.Id, ct)
            ?? throw new NotFoundException($"Stock transfer with ID '{request.Id}' was not found.");

        var fromGodown = await db.Godowns.AsNoTracking().SingleOrDefaultAsync(g => g.Id == transfer.FromGodownId, ct);
        var toGodown = await db.Godowns.AsNoTracking().SingleOrDefaultAsync(g => g.Id == transfer.ToGodownId, ct);

        var lines = transfer.Lines.Select(l => new StockTransferLineDto(
            l.Id,
            l.ItemType,
            l.ProductId,
            l.SkuId,
            l.ItemName,
            l.SerialNumber,
            l.Quantity,
            l.UnitCost,
            l.TotalValue
        )).ToList();

        return new StockTransferDetailDto(
            transfer.Id,
            transfer.TransferNumber,
            transfer.TransferDate,
            transfer.FromGodownId,
            fromGodown?.Name ?? "Unknown Godown",
            transfer.ToGodownId,
            toGodown?.Name ?? "Unknown Godown",
            transfer.VehicleNumber,
            transfer.DispatchNote,
            transfer.TransporterName,
            transfer.Narration,
            transfer.Status,
            transfer.TotalQuantity,
            transfer.TotalValue,
            lines
        );
    }
}

