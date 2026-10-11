using MediatR;

namespace AccountingInventory.Application.ProductModels.Commands.DeleteProductModel;

/// <summary>Self-service ProductModel delete — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteProductModelCommand(Guid Id) : IRequest;


