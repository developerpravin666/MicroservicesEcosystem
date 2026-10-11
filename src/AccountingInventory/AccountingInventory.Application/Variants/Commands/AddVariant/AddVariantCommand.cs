using MediatR;

namespace AccountingInventory.Application.Variants.Commands.AddVariant;

/// <summary>Self-service variant add — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record AddVariantCommand(string Name, string Description) : IRequest<AddVariantResult>;

public sealed record AddVariantResult(Guid Id, string Name, string Description);
