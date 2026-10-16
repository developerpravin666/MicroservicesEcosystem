using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Uom.Queries.GetUnitOfMeasures;

public sealed class GetUnitOfMeasuresQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetUnitOfMeasuresQuery, IReadOnlyList<UnitOfMeasureDto>>
{
    public async Task<IReadOnlyList<UnitOfMeasureDto>> Handle(GetUnitOfMeasuresQuery request, CancellationToken ct)
    {
        var query = db.UnitOfMeasures.AsNoTracking();
        if (request.ActiveOnly)
        {
            query = query.Where(u => u.IsActive);
        }

        var units = await query.OrderBy(u => u.IsBaseUnit ? 0 : 1).ThenBy(u => u.Code).ToListAsync(ct);
        var baseUnitLookup = units.Where(u => u.IsBaseUnit).ToDictionary(u => u.Id, u => u.Code);

        return units.Select(u => new UnitOfMeasureDto(
            u.Id,
            u.Code,
            u.Name,
            u.Symbol,
            u.IsBaseUnit,
            u.BaseUnitId,
            u.BaseUnitId.HasValue && baseUnitLookup.TryGetValue(u.BaseUnitId.Value, out var baseCode) ? baseCode : null,
            u.ConversionRate,
            u.Description,
            u.IsActive
        )).ToList();
    }
}

