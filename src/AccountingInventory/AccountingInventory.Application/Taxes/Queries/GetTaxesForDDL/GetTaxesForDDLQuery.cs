using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxesForDDL;

public sealed record GetTaxesForDDLQuery : IRequest<IEnumerable<TaxRateSummary>>;

public sealed record TaxRateSummary(Guid Id, decimal Cgst, decimal Sgst, decimal TotalTax);
