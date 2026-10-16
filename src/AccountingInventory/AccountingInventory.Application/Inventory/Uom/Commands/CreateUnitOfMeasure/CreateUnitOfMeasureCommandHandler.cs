using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Uom.Commands.CreateUnitOfMeasure;

public sealed class CreateUnitOfMeasureCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateUnitOfMeasureCommand, Guid>
{
    public async Task<Guid> Handle(CreateUnitOfMeasureCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.UnitOfMeasures.AnyAsync(u => u.Code == code, ct))
            throw new ConflictException($"Unit of measure with code '{code}' already exists.");

        if (!request.IsBaseUnit)
        {
            if (!request.BaseUnitId.HasValue || !await db.UnitOfMeasures.AnyAsync(u => u.Id == request.BaseUnitId.Value && u.IsBaseUnit, ct))
                throw new NotFoundException("Referenced base unit does not exist or is not marked as a base unit.");

            if (!request.ConversionRate.HasValue || request.ConversionRate.Value <= 0)
                throw new ConflictException("Alternate unit requires a positive conversion rate to its base unit.");
        }

        var uom = UnitOfMeasure.Create(
            code,
            request.Name,
            request.Symbol,
            request.IsBaseUnit,
            request.BaseUnitId,
            request.ConversionRate,
            request.Description);

        db.UnitOfMeasures.Add(uom);
        await db.SaveChangesAsync(ct);
        return uom.Id;
    }
}

