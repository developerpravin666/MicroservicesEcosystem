using BuildingBlocks.Application.Exceptions;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Users.Commands.AssignRole;

public sealed class AssignRoleCommandHandler(IAccountingInventoryDbContext db, ILogger<AssignRoleCommandHandler> logger)
    : IRequestHandler<AssignRoleCommand>
{
    public async Task Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            ?? throw new NotFoundException($"No user exists with id '{request.UserId}'.");

        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken)
            ?? throw new NotFoundException($"No role exists with id '{request.RoleId}'.");

        user.AssignRole(role.Id);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Role {RoleId} ({RoleName}) assigned to user {UserId}.", role.Id, role.Name, user.Id);
    }
}
