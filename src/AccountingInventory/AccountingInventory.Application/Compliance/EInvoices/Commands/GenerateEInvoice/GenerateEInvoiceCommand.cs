using MediatR;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.GenerateEInvoice;

public sealed record GenerateEInvoiceCommand(
    Guid SalesInvoiceId,
    string? CustomIrn = null,
    string? CustomAckNo = null) : IRequest<Guid>;

