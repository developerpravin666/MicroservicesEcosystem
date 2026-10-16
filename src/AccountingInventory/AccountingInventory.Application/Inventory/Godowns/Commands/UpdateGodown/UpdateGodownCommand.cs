using MediatR;

namespace AccountingInventory.Application.Inventory.Godowns.Commands.UpdateGodown;

public sealed record UpdateGodownCommand(
    Guid Id,
    string Name,
    string Code,
    string? Address = null,
    bool IsPrimary = false,
    bool IsActive = true) : IRequest;

