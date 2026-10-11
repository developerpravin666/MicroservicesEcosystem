using MediatR;

namespace AccountingInventory.Application.Sales.Products.Commands.DeleteSalesProduct;

public sealed record DeleteSalesProductCommand(Guid Id) : IRequest;
