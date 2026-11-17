using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Domain.Entities;
using MediatR;

namespace AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveRequests;

public sealed record GetLeaveRequestsQuery(Guid? EmployeeId = null, LeaveRequestStatus? Status = null) : IRequest<IReadOnlyList<LeaveRequestDto>>;
