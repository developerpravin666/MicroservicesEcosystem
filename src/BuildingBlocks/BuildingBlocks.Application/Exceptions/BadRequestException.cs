namespace BuildingBlocks.Application.Exceptions;

/// <summary>Thrown when a client request contains invalid parameters, missing data, or malformed payloads (400).</summary>
public sealed class BadRequestException(string message) : Exception(message);

