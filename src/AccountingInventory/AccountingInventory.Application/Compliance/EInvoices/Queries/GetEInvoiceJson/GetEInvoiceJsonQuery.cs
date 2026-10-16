using MediatR;

namespace AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoiceJson;

public sealed record GetEInvoiceJsonQuery(Guid Id) : IRequest<string>;

