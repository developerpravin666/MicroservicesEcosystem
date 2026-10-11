using AccountingInventory.Application.Taxes;
using MediatR;

namespace AccountingInventory.Application.Taxes.Queries.GetTaxById;

public sealed record GetTaxByIdQuery(Guid Id) : IRequest<TaxSummary>;
