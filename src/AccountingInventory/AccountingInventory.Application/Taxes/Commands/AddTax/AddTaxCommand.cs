using AccountingInventory.Application.Taxes;
using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.AddTax;

public sealed record AddTaxCommand(decimal Cgst, decimal Sgst, string? Name = null, DateOnly? EffectiveFrom = null, Guid? ProfileId = null, string Treatment = "Taxable") : IRequest<TaxSummary>;
