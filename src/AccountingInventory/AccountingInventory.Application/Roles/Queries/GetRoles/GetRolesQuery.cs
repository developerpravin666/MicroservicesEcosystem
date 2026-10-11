using AccountingInventory.Application.Roles.Commands.CreateRole;
using MediatR;

namespace AccountingInventory.Application.Roles.Queries.GetRoles;

public sealed record GetRolesQuery : IRequest<IReadOnlyList<RoleResult>>;
