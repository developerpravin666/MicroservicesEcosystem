using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using AccountingInventory.Domain.Entities;
using AccountingInventory.Application.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Roles.Commands.CreateRole;

/// <summary>
/// Creates a new, tenant-scoped, fully database-managed role. This — together with
/// UpdateRole/DeleteRole and the permission claims embedded at login — is the whole
/// mechanism behind "which roles can access which controller/screen is managed from
/// the database": an operator calls this endpoint (or a future admin UI built on top
/// of it) to define a role and the exact permission codes it grants, with no code
/// change or deployment required anywhere in the system.
/// </summary>
public sealed class CreateRoleCommandHandler(
    IAccountingInventoryDbContext db, ITenantContext tenantContext, ILogger<CreateRoleCommandHandler> logger)
    : IRequestHandler<CreateRoleCommand, RoleResult>
{
    public async Task<RoleResult> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var tenantId = tenantContext.TenantId
            ?? throw new UnauthorizedException("No tenant context is available for this request.");

        var exists = await db.Roles.AnyAsync(r => r.Name == request.Name, cancellationToken);

        if (exists)
        {
            throw new ConflictException($"A role named '{request.Name}' already exists for this tenant.");
        }

        var role = Role.Create(tenantId, request.Name);

        foreach (var code in request.PermissionCodes)
        {
            role.GrantPermission(code);
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Role {RoleId} ({RoleName}) created for tenant {TenantId} with permissions [{Permissions}].",
            role.Id, role.Name, tenantId, string.Join(", ", role.PermissionCodes));

        return new RoleResult(role.Id, role.Name, role.IsSystemDefined, [.. role.PermissionCodes]);
    }
}
