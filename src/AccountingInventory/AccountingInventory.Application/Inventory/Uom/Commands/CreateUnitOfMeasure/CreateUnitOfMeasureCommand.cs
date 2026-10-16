using MediatR;

namespace AccountingInventory.Application.Inventory.Uom.Commands.CreateUnitOfMeasure;

public sealed record CreateUnitOfMeasureCommand(
    string Code,
    string Name,
    string Symbol,
    bool IsBaseUnit = true,
    Guid? BaseUnitId = null,
    decimal? ConversionRate = null,
    string? Description = null) : IRequest<Guid>;

