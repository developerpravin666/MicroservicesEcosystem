using MediatR;

namespace AccountingInventory.Application.Brands.Commands.AddBrand;

/// <summary>Self-service brand add — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record AddBrandCommand(string Name, string Description) : IRequest<AddBrandResult>;

public sealed record AddBrandResult(Guid Id, string Name, string Description);
