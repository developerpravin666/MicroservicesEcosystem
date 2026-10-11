using MediatR;

namespace AccountingInventory.Application.ProductModels.Commands.AddProductModel;

/// <summary>Self-service ProductModel add — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record AddProductModelCommand(Guid BrandId, Guid ProductTypeId, string Code, string Name, string Description) : IRequest<AddProductModelResult>;

public sealed record AddProductModelResult(Guid Id, Guid BrandId, Guid ProductTypeId, string Code, string Name, string Description);


