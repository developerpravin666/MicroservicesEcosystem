using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkCreateProducts;

public sealed record BulkCreateProductsCommand(IReadOnlyList<CreateProductCommand> Products)
    : IRequest<IReadOnlyList<CreateProductResult>>;
