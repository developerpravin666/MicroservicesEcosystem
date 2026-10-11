using MediatR;

namespace AccountingInventory.Application.Variants.Commands.DeleteVariant;

/// <summary>Self-service variant delete — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteVariantCommand(Guid Id) : IRequest;
