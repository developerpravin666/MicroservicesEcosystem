using MediatR;

namespace AccountingInventory.Application.Inventory.Transfers.Commands.CreateStockTransfer;

public sealed record StockTransferLineInput(
    string ItemType, // "Serialized" or "Sku"
    Guid? ProductId,
    Guid? SkuId,
    string ItemName,
    string? SerialNumber,
    decimal Quantity,
    decimal UnitCost);

public sealed record CreateStockTransferCommand(
    DateOnly TransferDate,
    Guid FromGodownId,
    Guid ToGodownId,
    string? VehicleNumber,
    string? DispatchNote,
    string? TransporterName,
    string? Narration,
    IReadOnlyCollection<StockTransferLineInput> Lines) : IRequest<Guid>;

