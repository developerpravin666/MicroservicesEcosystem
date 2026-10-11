using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Roles.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler(IAccountingInventoryDbContext db, ILogger<DeleteRoleCommandHandler> logger)
    : IRequestHandler<DeleteRoleCommand>
{
    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"No role exists with id '{request.RoleId}'.");

        // RoleIds is stored as a converted CSV column (see UserConfiguration), which
        // EF Core cannot translate a LINQ .Contains() over into SQL — so this check is
        // necessarily client-side. Acceptable for the tenant-sized user bases this
        // reference architecture targets; a high-scale deployment should replace the
        // CSV conversion with a proper UserRole join table and do this as a SQL EXISTS.
        var users = await db.Users.AsNoTracking().ToListAsync(cancellationToken);
        var inUse = users.Any(u => u.RoleIds.Contains(role.Id));

        if (inUse)
        {
            throw new ConflictException($"Role '{role.Name}' is still assigned to one or more users and cannot be deleted.");
        }

        role.Delete();
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Role {RoleId} ({RoleName}) deleted.", role.Id, role.Name);
    }
}
