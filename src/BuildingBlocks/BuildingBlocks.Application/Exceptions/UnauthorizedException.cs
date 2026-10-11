namespace BuildingBlocks.Application.Exceptions;

/// <summary>Thrown when authentication succeeds but the credential/token presented is
/// invalid, expired, or does not belong to the claimed identity (401).</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
