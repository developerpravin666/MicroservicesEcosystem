namespace BuildingBlocks.Application.Exceptions;

/// <summary>Thrown when a requested resource does not exist (404).</summary>
public sealed class NotFoundException(string message) : Exception(message);
