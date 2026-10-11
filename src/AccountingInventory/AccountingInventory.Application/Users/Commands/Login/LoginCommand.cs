using MediatR;

namespace AccountingInventory.Application.Users.Commands.Login;

public sealed record LoginCommand(string TenantSlug, string UserName, string Password) : IRequest<LoginResult>;

public sealed record LoginResult(
    Guid UserId,
    Guid TenantId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAtUtc);
