using MediatR;

namespace AccountingInventory.Application.Inventory.Transfers.Queries.GetStockTransferById;

public sealed record StockTransferLineDto(
    Guid Id,
    string ItemType,
    Guid? ProductId,
    Guid? SkuId,
    string ItemName,
    string? SerialNumber,
    decimal Quantity,
    decimal UnitCost,
    decimal TotalValue);

public sealed record StockTransferDetailDto(
    Guid Id,
    string TransferNumber,
    DateOnly TransferDate,
    Guid FromGodownId,
    string FromGodownName,
    Guid ToGodownId,
    string ToGodownName,
    string? VehicleNumber,
    string? DispatchNote,
    string? TransporterName,
    string? Narration,
    string Status,
    decimal TotalQuantity,
    decimal TotalValue,
    IReadOnlyList<StockTransferLineDto> Lines);

public sealed record GetStockTransferByIdQuery(Guid Id) : IRequest<StockTransferDetailDto>;

