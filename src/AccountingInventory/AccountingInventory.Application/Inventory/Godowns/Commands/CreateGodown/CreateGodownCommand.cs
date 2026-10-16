using MediatR;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.CreateGodown;

public sealed record CreateGodownCommand(
    string Name,
    string Code,
    string? Address = null,
    bool IsPrimary = false) : IRequest<Guid>;

