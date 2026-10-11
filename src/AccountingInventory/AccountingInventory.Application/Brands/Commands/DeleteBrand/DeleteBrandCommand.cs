using MediatR;

namespace AccountingInventory.Application.Brands.Commands.DeleteBrand;

/// <summary>Self-service brand delete — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteBrandCommand(Guid Id) : IRequest;
