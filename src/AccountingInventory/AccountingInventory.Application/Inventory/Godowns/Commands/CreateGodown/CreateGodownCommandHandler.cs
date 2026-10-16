using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.CreateGodown;

public sealed class CreateGodownCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateGodownCommand, Guid>
{
    public async Task<Guid> Handle(CreateGodownCommand request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Godowns.AnyAsync(g => g.Code == code, ct))
            throw new ConflictException($"Godown with code '{code}' already exists.");

        if (request.IsPrimary)
        {
            var existingPrimary = await db.Godowns.Where(g => g.IsPrimary).ToListAsync(ct);
            foreach (var p in existingPrimary)
            {
                p.SetPrimary(false);
            }
        }

        var godown = Godown.Create(request.Name, code, request.Address, request.IsPrimary);
        db.Godowns.Add(godown);
        await db.SaveChangesAsync(ct);
        return godown.Id;
    }
}

