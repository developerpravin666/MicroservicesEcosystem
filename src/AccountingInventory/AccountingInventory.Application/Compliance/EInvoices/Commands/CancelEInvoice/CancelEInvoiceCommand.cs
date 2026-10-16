using MediatR;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.CancelEInvoice;

public sealed record CancelEInvoiceCommand(Guid Id, string Reason) : IRequest;

