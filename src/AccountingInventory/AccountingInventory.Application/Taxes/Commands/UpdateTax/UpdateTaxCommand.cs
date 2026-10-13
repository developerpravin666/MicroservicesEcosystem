using AccountingInventory.Application.Taxes;
using MediatR;

namespace AccountingInventory.Application.Taxes.Commands.UpdateTax;

public sealed record UpdateTaxCommand(Guid Id, decimal Cgst, decimal Sgst, bool IsActive, string? Name = null, DateOnly? EffectiveFrom = null, string? Treatment = null) : IRequest<TaxSummary>;
