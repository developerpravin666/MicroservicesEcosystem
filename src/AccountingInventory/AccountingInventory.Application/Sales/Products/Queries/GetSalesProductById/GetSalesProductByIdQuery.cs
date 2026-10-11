using MediatR;

namespace AccountingInventory.Application.Sales.Products.Queries.GetSalesProductById;

public sealed record GetSalesProductByIdQuery(Guid Id) : IRequest<SalesProductSummary>;
