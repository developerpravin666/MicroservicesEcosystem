using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Transfers.Commands.CreateStockTransfer;

public sealed class CreateStockTransferCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateStockTransferCommand, Guid>
{
    public async Task<Guid> Handle(CreateStockTransferCommand request, CancellationToken ct)
    {
        await LedgerPosting.EnsurePeriodOpenAsync(db, request.TransferDate, ct);

        var fromGodown = await db.Godowns.SingleOrDefaultAsync(g => g.Id == request.FromGodownId && g.IsActive, ct)
            ?? throw new NotFoundException("Source godown not found or is inactive.");

        var toGodown = await db.Godowns.SingleOrDefaultAsync(g => g.Id == request.ToGodownId && g.IsActive, ct)
            ?? throw new NotFoundException("Destination godown not found or is inactive.");

        var transferCount = await db.StockTransferJournals.CountAsync(ct);
        var transferNumber = $"STJ-{request.TransferDate:yyyyMM}-{transferCount + 1:D4}";

        var transferLines = new List<StockTransferLine>();

        foreach (var lineInput in request.Lines)
        {
            if (lineInput.ItemType == "Serialized")
            {
                if (!lineInput.ProductId.HasValue)
                    throw new ConflictException($"Line '{lineInput.ItemName}' is marked Serialized but has no Product ID.");

                var product = await db.Products.SingleOrDefaultAsync(p => p.Id == lineInput.ProductId.Value && !p.IsSold && p.IsActive, ct)
                    ?? throw new NotFoundException($"Active unsold product '{lineInput.ItemName}' was not found.");

                var inSource = (product.GodownId == request.FromGodownId) || (product.GodownId == null && fromGodown.IsPrimary);
                if (!inSource)
                    throw new ConflictException($"Product '{lineInput.ItemName}' (S/N: {product.SerialNumber}) is not in source godown '{fromGodown.Name}'.");

                product.AssignGodown(request.ToGodownId);

                // Update or create StockBalance for serialized product
                var balance = await db.StockBalances
                    .FirstOrDefaultAsync(b => b.ProductId == product.Id && b.IsActive, ct);

                if (balance != null)
                {
                    balance.UpdateGodown(request.ToGodownId, request.TransferDate);
                }
                else
                {
                    balance = StockBalance.CreateSerialized(request.ToGodownId, product.Id, product.SerialNumber, request.TransferDate);
                    db.StockBalances.Add(balance);
                }

                transferLines.Add(StockTransferLine.Create(
                    "Serialized",
                    product.Id,
                    null,
                    lineInput.ItemName,
                    product.SerialNumber,
                    1m,
                    product.PurchasePrice));
            }
            else // "Sku"
            {
                if (!lineInput.SkuId.HasValue)
                    throw new ConflictException($"Line '{lineInput.ItemName}' is marked Sku but has no SKU ID.");

                var sku = await db.StockSkus.SingleOrDefaultAsync(s => s.Id == lineInput.SkuId.Value && s.IsActive, ct)
                    ?? throw new NotFoundException($"SKU '{lineInput.ItemName}' was not found.");

                var fromBalance = await db.StockBalances
                    .FirstOrDefaultAsync(b => b.GodownId == request.FromGodownId && b.SkuId == sku.Id && b.ItemType == "Sku" && b.IsActive, ct);

                // If source balance doesn't exist yet, but source is Primary, seed source balance from SKU available quantity
                if ((fromBalance == null || fromBalance.Quantity < lineInput.Quantity) && fromGodown.IsPrimary)
                {
                    var allocatedElsewhere = await db.StockBalances
                        .Where(b => b.SkuId == sku.Id && b.GodownId != request.FromGodownId && b.ItemType == "Sku" && b.IsActive)
                        .SumAsync(b => b.Quantity, ct);

                    var availableInPrimary = sku.Quantity - allocatedElsewhere;
                    if (availableInPrimary >= lineInput.Quantity)
                    {
                        if (fromBalance == null)
                        {
                            fromBalance = StockBalance.CreateSku(request.FromGodownId, sku.Id, availableInPrimary, request.TransferDate);
                            db.StockBalances.Add(fromBalance);
                        }
                        else
                        {
                            fromBalance.SetQuantity(availableInPrimary, request.TransferDate);
                        }
                    }
                }

                if (fromBalance == null || fromBalance.Quantity < lineInput.Quantity)
                {
                    var available = fromBalance?.Quantity ?? 0m;
                    throw new ConflictException($"Insufficient stock for SKU '{sku.Name}'. Available in '{fromGodown.Name}': {available}, Requested: {lineInput.Quantity}.");
                }

                fromBalance.DeductQuantity(lineInput.Quantity, request.TransferDate);

                // Add to destination godown
                var toBalance = await db.StockBalances
                    .FirstOrDefaultAsync(b => b.GodownId == request.ToGodownId && b.SkuId == sku.Id && b.ItemType == "Sku" && b.IsActive, ct);

                if (toBalance == null)
                {
                    toBalance = StockBalance.CreateSku(request.ToGodownId, sku.Id, lineInput.Quantity, request.TransferDate);
                    db.StockBalances.Add(toBalance);
                }
                else
                {
                    toBalance.AddQuantity(lineInput.Quantity, request.TransferDate);
                }

                var unitCost = sku.Quantity > 0 ? (sku.InventoryValue / sku.Quantity) : lineInput.UnitCost;
                transferLines.Add(StockTransferLine.Create(
                    "Sku",
                    null,
                    sku.Id,
                    sku.Name,
                    null,
                    lineInput.Quantity,
                    decimal.Round(unitCost, 2, MidpointRounding.AwayFromZero)));
            }
        }

        var journal = StockTransferJournal.Create(
            transferNumber,
            request.TransferDate,
            request.FromGodownId,
            request.ToGodownId,
            request.VehicleNumber,
            request.DispatchNote,
            request.TransporterName,
            request.Narration,
            transferLines);

        db.StockTransferJournals.Add(journal);
        await db.SaveChangesAsync(ct);

        return journal.Id;
    }
}

