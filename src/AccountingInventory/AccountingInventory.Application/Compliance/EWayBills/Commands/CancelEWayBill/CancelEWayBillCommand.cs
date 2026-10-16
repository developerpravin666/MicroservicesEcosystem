using MediatR;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.CancelEWayBill;

public sealed record CancelEWayBillCommand(Guid Id, string Reason) : IRequest;

