using MediatR;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CancelRcmSelfInvoice;

public sealed record CancelRcmSelfInvoiceCommand(Guid Id, string Reason) : IRequest;

