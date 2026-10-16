using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Adjustments.Commands.CreateStockAdjustment;

public sealed class CreateStockAdjustmentCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateStockAdjustmentCommand, Guid>
{
    public async Task<Guid> Handle(CreateStockAdjustmentCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.AdjustmentDate, ct);

        var godown = await db.Godowns.SingleOrDefaultAsync(g => g.Id == request.GodownId && g.IsActive, ct)
            ?? throw new NotFoundException($"Godown with ID '{request.GodownId}' not found or inactive.");

        var adjCount = await db.StockAdjustments.CountAsync(ct);
        var adjustmentNumber = $"ADJ-{request.AdjustmentDate:yyyyMM}-{adjCount + 1:D4}";

        var discrepancy = request.PhysicalQuantity - request.BookQuantity;
        if (discrepancy == 0)
            throw new ConflictException("Physical count matches book stock; no discrepancy adjustment required.");

        var totalValue = decimal.Round(Math.Abs(discrepancy) * request.UnitCost, 2, MidpointRounding.AwayFromZero);
        var accounts = await LedgerPosting.EnsureSystemAccountsAsync(db, ct);
        JournalEntry? journal = null;

        if (request.AdjustmentType == "Shortage")
        {
            if (discrepancy > 0)
                throw new ConflictException("Shortage adjustment requires physical quantity to be less than book quantity.");

            var shortageQty = Math.Abs(discrepancy);

            if (request.ItemType == "Serialized")
            {
                if (!request.ProductId.HasValue)
                    throw new ConflictException("Serialized adjustment requires a Product ID.");

                var product = await db.Products.SingleOrDefaultAsync(p => p.Id == request.ProductId.Value && !p.IsSold && p.IsActive, ct)
                    ?? throw new NotFoundException($"Product '{request.ItemName}' not found or already sold/inactive.");

                product.WriteOff();

                var balance = await db.StockBalances.FirstOrDefaultAsync(b => b.ProductId == product.Id && b.IsActive, ct);
                if (balance != null)
                {
                    balance.DeductQuantity(1m, request.AdjustmentDate);
                }

                db.InventoryAdjustments.Add(InventoryAdjustment.WriteOff(product.Id, request.AdjustmentDate, request.Reason, product.PurchasePrice));
            }
            else // Sku
            {
                if (!request.SkuId.HasValue)
                    throw new ConflictException("SKU adjustment requires a SKU ID.");

                var sku = await db.StockSkus.SingleOrDefaultAsync(s => s.Id == request.SkuId.Value && s.IsActive, ct)
                    ?? throw new NotFoundException($"SKU '{request.ItemName}' not found.");

                var balance = await db.StockBalances.FirstOrDefaultAsync(b => b.GodownId == request.GodownId && b.SkuId == sku.Id && b.IsActive, ct);
                if (balance != null)
                {
                    var deductQty = Math.Min(balance.Quantity, shortageQty);
                    balance.DeductQuantity(deductQty, request.AdjustmentDate);
                }

                if (shortageQty <= sku.Quantity)
                {
                    sku.Issue(shortageQty, request.AdjustmentDate);
                }
            }

            // Post General Ledger Journal for Shortage / Write-off: Dr 5100 (Shrinkage/Expense), Cr 1200 (Inventory)
            if (totalValue > 0)
            {
                journal = JournalEntry.Post(
                    request.AdjustmentDate,
                    $"Stock shortage write-off: {request.ItemName} ({request.Reason})",
                    "StockAdjustment",
                    adjustmentNumber,
                    [
                        (accounts["5100"], totalValue, 0m, $"Stock Shortage: {request.Reason}"),
                        (accounts["1200"], 0m, totalValue, "Inventory write-off")
                    ]
                );
                db.JournalEntries.Add(journal);
                db.JournalLines.AddRange(journal.Lines);
            }
        }
        else // "Surplus"
        {
            if (discrepancy < 0)
                throw new ConflictException("Surplus adjustment requires physical quantity to be greater than book quantity.");

            var surplusQty = discrepancy;

            if (request.ItemType == "Sku")
            {
                if (!request.SkuId.HasValue)
                    throw new ConflictException("SKU adjustment requires a SKU ID.");

                var sku = await db.StockSkus.SingleOrDefaultAsync(s => s.Id == request.SkuId.Value && s.IsActive, ct)
                    ?? throw new NotFoundException($"SKU '{request.ItemName}' not found.");

                var balance = await db.StockBalances.FirstOrDefaultAsync(b => b.GodownId == request.GodownId && b.SkuId == sku.Id && b.IsActive, ct);
                if (balance == null)
                {
                    balance = StockBalance.CreateSku(request.GodownId, sku.Id, surplusQty, request.AdjustmentDate);
                    db.StockBalances.Add(balance);
                }
                else
                {
                    balance.AddQuantity(surplusQty, request.AdjustmentDate);
                }

                sku.Receive(surplusQty, totalValue, request.AdjustmentDate);
            }

            // Post General Ledger Journal for Surplus: Dr 1200 (Inventory), Cr 4900 (Gain/Other Income)
            if (totalValue > 0)
            {
                journal = JournalEntry.Post(
                    request.AdjustmentDate,
                    $"Stock surplus adjustment: {request.ItemName} ({request.Reason})",
                    "StockAdjustment",
                    adjustmentNumber,
                    [
                        (accounts["1200"], totalValue, 0m, "Inventory gain"),
                        (accounts["4900"], 0m, totalValue, $"Stock Surplus: {request.Reason}")
                    ]
                );
                db.JournalEntries.Add(journal);
                db.JournalLines.AddRange(journal.Lines);
            }
        }

        var stockAdj = StockAdjustment.Create(
            adjustmentNumber,
            request.AdjustmentDate,
            request.GodownId,
            request.AdjustmentType,
            request.Reason,
            request.ItemType,
            request.ProductId,
            request.SkuId,
            request.ItemName,
            request.SerialNumber,
            request.BookQuantity,
            request.PhysicalQuantity,
            request.UnitCost,
            journal?.Id
        );

        db.StockAdjustments.Add(stockAdj);
        await db.SaveChangesAsync(ct);

        return stockAdj.Id;
    }
}

