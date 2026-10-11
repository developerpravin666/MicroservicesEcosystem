using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.DeleteTax;

public sealed record DeleteTaxCommand(Guid Id) : IRequest;
