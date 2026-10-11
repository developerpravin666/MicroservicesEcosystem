using MediatR;

namespace AccountingInventory.Application.ProductModels.Commands.UpdateProductModelCommand;

/// <summary>Self-service ProductModel update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateProductModelCommand(Guid Id, Guid BrandId, Guid ProductTypeId, string Code, string Name, string Description, bool IsActive) : IRequest<UpdateProductModelResult>;

public sealed record UpdateProductModelResult(Guid Id, Guid BrandId, Guid ProductTypeId, string Code, string Name, string Description, bool IsActive);


