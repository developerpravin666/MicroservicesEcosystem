using MediatR;

namespace AccountingInventory.Application.ProductTypes.Commands.UpdateProductTypeCommand;

/// <summary>Self-service ProductType update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateProductTypeCommand(Guid Id, string Name, string Description, bool IsActive) : IRequest<UpdateProductTypeResult>;

public sealed record UpdateProductTypeResult(Guid Id, string Name, string Description, bool IsActive);

