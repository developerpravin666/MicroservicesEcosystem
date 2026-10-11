using MediatR;

namespace AccountingInventory.Application.Variants.Commands.UpdateVariantCommand;

/// <summary>Self-service variant update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateVariantCommand(Guid Id, string Name, string Description, bool IsActive) : IRequest<UpdateVariantResult>;

public sealed record UpdateVariantResult(Guid Id, string Name, string Description, bool IsActive);
