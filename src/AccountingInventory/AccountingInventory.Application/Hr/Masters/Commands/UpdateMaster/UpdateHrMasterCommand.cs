using AccountingInventory.Application.Hr.Masters.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Masters.Commands.UpdateMaster;

public sealed record UpdateHrMasterCommand(
    Guid Id,
    string Name,
    string? Description = null,
    bool IsDefault = false,
    bool IsActive = true) : IRequest<HrMasterItemDto>;
