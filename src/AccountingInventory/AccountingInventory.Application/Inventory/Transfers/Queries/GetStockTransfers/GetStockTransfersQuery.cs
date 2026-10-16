using MediatR;

namespace AccountingInventory.Application.Inventory.Transfers.Queries.GetStockTransfers;

public sealed record StockTransferSummaryDto(
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
    int ItemCount);

public sealed record GetStockTransfersQuery(
    Guid? GodownId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IRequest<IReadOnlyList<StockTransferSummaryDto>>;

