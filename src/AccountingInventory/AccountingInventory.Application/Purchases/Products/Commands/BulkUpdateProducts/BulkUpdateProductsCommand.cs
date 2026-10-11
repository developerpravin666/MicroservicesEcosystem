using AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;
using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkUpdateProducts;

public sealed record BulkUpdateProductsCommand(IReadOnlyList<UpdateProductCommand> Products)
    : IRequest<IReadOnlyList<UpdateProductResult>>;
