using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Commands.DeleteLeaveRequest;

public sealed record DeleteLeaveRequestCommand(Guid Id) : IRequest<Unit>;
