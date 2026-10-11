using MediatR;

namespace AccountingInventory.Application.Colors.Commands.UpdateColorCommand;

/// <summary>Self-service color update — deliberately anonymous at the API layer
/// this in a production deployment.</summary>
public sealed record UpdateColorCommand(Guid Id, string Name, string Description, bool IsActive) : IRequest<UpdateColorResult>;

public sealed record UpdateColorResult(Guid Id, string Name, string Description, bool IsActive);
