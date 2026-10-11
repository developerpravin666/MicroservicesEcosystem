using AccountingInventory.Application.Roles.Commands.CreateRole;
using MediatR;

namespace AccountingInventory.Application.Roles.Commands.UpdateRole;

/// <summary>Replaces a role's permission set wholesale (a PUT-style update — the
/// simplest, least error-prone way to let an admin UI show "these are the current
/// permissions, check the ones you want" and save the result in one call).</summary>
public sealed record UpdateRoleCommand(Guid RoleId, IReadOnlyList<string> PermissionCodes) : IRequest<RoleResult>;
