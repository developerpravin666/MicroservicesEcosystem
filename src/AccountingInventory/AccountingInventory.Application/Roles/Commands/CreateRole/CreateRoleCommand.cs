using MediatR;

namespace AccountingInventory.Application.Roles.Commands.CreateRole;

public sealed record CreateRoleCommand(string Name, IReadOnlyList<string> PermissionCodes) : IRequest<RoleResult>;

public sealed record RoleResult(Guid RoleId, string Name, bool IsSystemDefined, IReadOnlyList<string> PermissionCodes);
