using AccountingInventory.Application.Abstractions;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.UpdateGodown;

public sealed class UpdateGodownCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<UpdateGodownCommand>
{
    public async Task Handle(UpdateGodownCommand request, CancellationToken ct)
    {
        var godown = await db.Godowns.SingleOrDefaultAsync(g => g.Id == request.Id, ct)
            ?? throw new NotFoundException($"Godown with ID '{request.Id}' was not found.");

        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Godowns.AnyAsync(g => g.Code == code && g.Id != request.Id, ct))
            throw new ConflictException($"Godown with code '{code}' already exists.");

        if (request.IsPrimary && !godown.IsPrimary)
        {
            var existingPrimary = await db.Godowns.Where(g => g.IsPrimary && g.Id != request.Id).ToListAsync(ct);
            foreach (var p in existingPrimary)
            {
                p.SetPrimary(false);
            }
        }

        godown.UpdateDetails(request.Name, code, request.Address, request.IsPrimary);
        godown.SetActive(request.IsActive);

        await db.SaveChangesAsync(ct);
    }
}

