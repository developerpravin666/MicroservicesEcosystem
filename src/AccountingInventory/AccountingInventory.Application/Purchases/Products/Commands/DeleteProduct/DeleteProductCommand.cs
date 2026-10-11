using MediatR;

namespace AccountingInventory.Application.Purchases.Products.Commands.DeleteProduct;

public sealed record DeleteProductCommand(Guid Id) : IRequest;
