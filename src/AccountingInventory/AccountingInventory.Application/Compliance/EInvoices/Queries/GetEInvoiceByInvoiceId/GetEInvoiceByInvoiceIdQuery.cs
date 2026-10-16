using AccountingInventory.Application.Compliance.EInvoices.Models;
using MediatR;

namespace AccountingInventory.Application.Compliance.EInvoices.Queries.GetEInvoiceByInvoiceId;

public sealed record GetEInvoiceByInvoiceIdQuery(Guid SalesInvoiceId) : IRequest<EInvoiceDto?>;

