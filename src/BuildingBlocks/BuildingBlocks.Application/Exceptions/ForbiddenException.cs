namespace BuildingBlocks.Application.Exceptions;

/// <summary>Thrown when an authenticated caller is missing the permission/role
/// required for the operation they attempted (403).</summary>
public sealed class ForbiddenException(string message) : Exception(message);
