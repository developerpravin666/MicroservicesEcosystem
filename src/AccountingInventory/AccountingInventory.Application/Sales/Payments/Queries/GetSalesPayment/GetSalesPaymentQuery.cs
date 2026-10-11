using MediatR;

namespace AccountingInventory.Application.Sales.Payments.Queries.GetSalesPayment;

public sealed record GetSalesPaymentQuery(Guid SalesProductId) : IRequest<SalesPaymentSummary?>;
