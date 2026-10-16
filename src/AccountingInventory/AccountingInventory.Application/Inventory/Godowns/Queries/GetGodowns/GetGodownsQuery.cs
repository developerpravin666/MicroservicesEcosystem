using MediatR;

namespace AccountingInventory.Application.Inventory.Godowns.Queries.GetGodowns;

public sealed record GodownDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    bool IsPrimary,
    bool IsActive,
    int SerializedItemCount,
    decimal SkuItemQuantity,
    decimal TotalStockValue);

public sealed record GetGodownsQuery : IRequest<IReadOnlyList<GodownDto>>;

