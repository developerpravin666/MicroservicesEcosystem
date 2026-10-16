using AccountingInventory.Application.Compliance.EWayBills.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.EWayBills.Queries.GetEWayBillByInvoiceId;

public sealed record GetEWayBillByInvoiceIdQuery(Guid SalesInvoiceId) : IRequest<EWayBillDto?>;

