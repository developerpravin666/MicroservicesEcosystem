using MediatR;

namespace AccountingInventory.Application.Hr.Masters.Commands.DeleteMaster;

public sealed record DeleteHrMasterCommand(Guid Id) : IRequest<Unit>;
