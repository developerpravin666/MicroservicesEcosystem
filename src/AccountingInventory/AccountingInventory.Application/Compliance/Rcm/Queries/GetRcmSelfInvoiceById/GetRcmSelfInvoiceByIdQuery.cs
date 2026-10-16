using AccountingInventory.Application.Compliance.Rcm.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.Rcm.Queries.GetRcmSelfInvoiceById;

public sealed record GetRcmSelfInvoiceByIdQuery(Guid Id) : IRequest<RcmSelfInvoiceDto>;

