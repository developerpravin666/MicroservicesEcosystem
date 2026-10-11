using AccountingInventory.Application.Taxes;
using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.AddTax;

public sealed record AddTaxCommand(decimal Cgst, decimal Sgst) : IRequest<TaxSummary>;
