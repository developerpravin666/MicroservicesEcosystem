using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Users.Commands.Refresh;

/// <summary>
/// Issues a new access/refresh token pair without requiring the caller to know their
/// tenant slug again — the expired access token already carries tenant_id and
/// claims from when it was first minted. The current registry is checked before
/// resolving the schema and issuing another token.
/// </summary>
public sealed class RefreshTokenCommandHandler(
    IAccountingInventoryDbContext db,
    ITokenService tokenService,
    ITenantDirectoryContext tenantDirectory,
    ITenantContextAccessor tenantContextAccessor,
    ILogger<RefreshTokenCommandHandler> logger)
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResult>
{
    public async Task<RefreshTokenResult> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = tokenService.GetPrincipalFromExpiredToken(request.AccessToken);

        if (principal is null)
        {
            logger.LogWarning("Token refresh failed: expired access token was malformed or invalid.");
            throw new UnauthorizedException("The access token is malformed or invalid.");
        }

        var tenant = await tenantDirectory.Tenants.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == principal.TenantId, cancellationToken);
        if (tenant is null || !tenant.IsActive || tenant.Status != TenantStatus.Active)
            throw new UnauthorizedException("This tenant is not currently active.");
        tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
        await db.ResetConnectionAsync(cancellationToken);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == principal.UserId && u.IsActive, cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "Token refresh failed: no user {UserId} in tenant {TenantId}.", principal.UserId, principal.TenantId);
            throw new UnauthorizedException("Refresh token is invalid or expired.");
        }

        var hashedIncoming = tokenService.HashRefreshToken(request.RefreshToken);

        if (!user.IsRefreshTokenValid(hashedIncoming, DateTimeOffset.UtcNow))
        {
            logger.LogWarning(
                "Token refresh failed: refresh token invalid or expired for user {UserId}.", user.Id);
            throw new UnauthorizedException("Refresh token is invalid or expired.");
        }

        // Re-read roles/permissions from the database on every refresh — this is the
        // mechanism by which a role or permission change made in the database (e.g.
        // an admin revoking a permission from a role) reaches an already-logged-in
        // user: within one access-token lifetime (15 minutes by default).
        //var roles = await db.Roles
        //    .Where(r => user.RoleIds.Contains(r.Id))
        //    .AsNoTracking()
        //    .ToListAsync(cancellationToken);

        var roleNames = new string[] { };
        var permissionCodes = new string[] { };

        var pair = tokenService.GenerateTokenPair(
            user.Id, user.UserName, tenant.Id, tenant.SchemaName, roleNames, permissionCodes);

        user.SetRefreshToken(tokenService.HashRefreshToken(pair.RefreshToken), DateTimeOffset.UtcNow.AddDays(7));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Access token refreshed for user {UserId} ({UserName}).", user.Id, user.UserName);

        return new RefreshTokenResult(pair.AccessToken, pair.RefreshToken, pair.AccessTokenExpiresAtUtc);
    }
}
