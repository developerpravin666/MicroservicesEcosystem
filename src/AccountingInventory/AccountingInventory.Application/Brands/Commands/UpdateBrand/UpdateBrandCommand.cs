using MediatR;

namespace AccountingInventory.Application.Brands.Commands.UpdateBrandCommand;

/// <summary>Self-service brand update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateBrandCommand(Guid Id, string Name, string Description, bool IsActive) : IRequest<UpdateBrandResult>;

public sealed record UpdateBrandResult(Guid Id, string Name, string Description, bool IsActive);
