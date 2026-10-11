using MediatR;

namespace AccountingInventory.Application.ProductTypes.Commands.DeleteProductType;

/// <summary>Self-service ProductType delete — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteProductTypeCommand(Guid Id) : IRequest;

