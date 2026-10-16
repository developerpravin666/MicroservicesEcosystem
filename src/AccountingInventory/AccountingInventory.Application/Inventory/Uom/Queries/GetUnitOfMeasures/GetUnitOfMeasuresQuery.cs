using MediatR;

namespace AccountingInventory.Application.Inventory.Uom.Queries.GetUnitOfMeasures;

public sealed record UnitOfMeasureDto(
    Guid Id,
    string Code,
    string Name,
    string Symbol,
    bool IsBaseUnit,
    Guid? BaseUnitId,
    string? BaseUnitCode,
    decimal? ConversionRate,
    string? Description,
    bool IsActive);

public sealed record GetUnitOfMeasuresQuery(bool ActiveOnly = false) : IRequest<IReadOnlyList<UnitOfMeasureDto>>;

