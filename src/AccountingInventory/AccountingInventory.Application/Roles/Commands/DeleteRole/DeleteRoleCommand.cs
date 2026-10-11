using MediatR;

namespace AccountingInventory.Application.Roles.Commands.DeleteRole;

public sealed record DeleteRoleCommand(Guid RoleId) : IRequest;
