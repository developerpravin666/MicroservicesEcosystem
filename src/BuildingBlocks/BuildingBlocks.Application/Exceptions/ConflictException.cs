namespace BuildingBlocks.Application.Exceptions;

/// <summary>Thrown when a command would violate a uniqueness constraint (409).</summary>
public sealed class ConflictException(string message) : Exception(message);
