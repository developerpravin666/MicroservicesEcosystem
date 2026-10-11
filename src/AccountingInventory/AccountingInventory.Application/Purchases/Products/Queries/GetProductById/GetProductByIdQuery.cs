using AccountingInventory.Application.Purchases.Products.Queries.GetProducts;
using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductSummary>;
