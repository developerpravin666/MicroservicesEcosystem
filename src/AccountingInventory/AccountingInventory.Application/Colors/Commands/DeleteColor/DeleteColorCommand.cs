using MediatR;

namespace AccountingInventory.Application.Colors.Commands.DeleteColor;

/// <summary>Self-service color delete — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record DeleteColorCommand(Guid Id) : IRequest;
