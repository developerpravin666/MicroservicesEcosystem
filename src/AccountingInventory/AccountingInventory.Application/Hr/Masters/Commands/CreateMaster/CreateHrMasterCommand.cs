using AccountingInventory.Application.Hr.Masters.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Masters.Commands.CreateMaster;

public sealed record CreateHrMasterCommand(
    string Category,
    string Name,
    string Code,
    string? Description = null,
    int SortOrder = 0,
    bool IsDefault = false) : IRequest<HrMasterItemDto>;
