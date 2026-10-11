using MediatR;

namespace AccountingInventory.Application.Colors.Commands.AddColor;

/// <summary>Self-service color add — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record AddColorCommand(string Name, string Description) : IRequest<AddColorResult>;

public sealed record AddColorResult(Guid Id, string Name, string Description);
