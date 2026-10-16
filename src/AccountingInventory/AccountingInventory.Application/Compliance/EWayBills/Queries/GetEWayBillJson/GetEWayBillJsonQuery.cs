using MediatR;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillJson;

public sealed record GetEWayBillJsonQuery(Guid Id) : IRequest<string>;

