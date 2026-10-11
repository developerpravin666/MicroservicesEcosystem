using MediatR;

namespace AccountingInventory.Application.Users.Commands.Register;

public sealed record RegisterCommand(
    string UserName,
    string Email,
    string Mobile,
    string Password) : IRequest<RegisterResult>;

public sealed record RegisterResult(
    Guid UserId,
    Guid TenantId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);
