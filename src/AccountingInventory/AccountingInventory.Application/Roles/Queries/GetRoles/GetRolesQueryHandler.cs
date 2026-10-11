using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Roles.Commands.CreateRole;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Roles.Queries.GetRoles;

public sealed class GetRolesQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetRolesQuery, IReadOnlyList<RoleResult>>
{
    public async Task<IReadOnlyList<RoleResult>> Handle(GetRolesQuery request, CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(cancellationToken);

        return [.. roles.Select(r => new RoleResult(r.Id, r.Name, r.IsSystemDefined, [.. r.PermissionCodes]))];
    }
}
