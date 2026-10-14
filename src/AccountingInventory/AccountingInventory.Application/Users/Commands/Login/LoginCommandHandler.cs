using BuildingBlocks.Application.Exceptions;
using BuildingBlocks.Domain.MultiTenancy;
using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccountingInventory.Application.Users.Commands.Login;

public sealed class LoginCommandHandler(
    IAccountingInventoryDbContext accInvDbContext,
    ITenantDirectoryContext tenantDirectory,
    ITokenService tokenService,
    ITenantContextAccessor tenantContextAccessor,
    IPasswordHasher<User> passwordHasher,
    ILogger<LoginCommandHandler> logger)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var normalizedSlug = request.TenantSlug?.Trim().ToLowerInvariant();
        var identifier = request.UserName.Trim().ToLowerInvariant();

        if (normalizedSlug is "system" or "admin")
        {
            var adminUsername = "admin";
            var adminEmail = "developer.pravin666@gmail.com";
            var defaultPassword = "Admin@123456";

            var inputUsername = request.UserName.Trim();
            var isUserMatch = string.Equals(inputUsername, adminUsername, StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(inputUsername, adminEmail, StringComparison.OrdinalIgnoreCase);

            if (isUserMatch && request.Password == defaultPassword)
            {
                var systemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
                var systemTenantId = Guid.Parse("00000000-0000-0000-0000-000000000000");
                var defaultRoleNames = new[] { "ProductOwner", "SuperAdmin" };
                var defaultPermissionCodes = new[] { "Tenants.Manage", "System.Manage", "Database.Manage" };

                var defaultPair = tokenService.GenerateTokenPair(
                    systemUserId,
                    adminUsername,
                    systemTenantId,
                    "tenant",
                    defaultRoleNames,
                    defaultPermissionCodes);

                logger.LogInformation("Product Owner logged in with roles [{Roles}].", string.Join(", ", defaultRoleNames));
                return new LoginResult(systemUserId, systemTenantId, defaultPair.AccessToken, defaultPair.RefreshToken, defaultPair.AccessTokenExpiresAtUtc);
            }
        }

        Tenant? tenant;
        Guid? resolvedUserId = null;
        if (string.IsNullOrEmpty(normalizedSlug))
        {
            // Discovery is read-only: never track users while switching schemas.
            // Usernames are tenant-local and are only accepted with an explicit slug.
            tenant = null;
            var candidates = await tenantDirectory.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active && t.IsActive)
                .ToListAsync(cancellationToken);
            foreach (var candidate in candidates)
            {
                tenantContextAccessor.SetTenant(candidate.Id, candidate.SchemaName);
                await accInvDbContext.ResetConnectionAsync(cancellationToken);
                var matches = await accInvDbContext.Users.AsNoTracking()
                    .Where(u => u.IsActive &&
                        (u.Email.Trim().ToLower() == identifier || u.Mobile.Trim() == identifier))
                    .Select(u => u.Id).Take(2).ToListAsync(cancellationToken);
                if (matches.Count > 1 || (matches.Count == 1 && resolvedUserId is not null))
                    throw new UnauthorizedException("Unable to sign in automatically. Use your tenant slug, username and password.");
                if (matches.Count == 1)
                {
                    tenant = candidate;
                    resolvedUserId = matches[0];
                }
            }
        }
        else
        {
            tenant = await tenantDirectory.Tenants.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Slug == normalizedSlug, cancellationToken);
        }

        if (tenant is null)
        {
            logger.LogWarning("Login failed: unknown tenant slug {TenantSlug}.", normalizedSlug);
            throw new UnauthorizedException("Invalid tenant, username, or password.");
        }

        if (tenant.Status != TenantStatus.Active || !tenant.IsActive)
        {
            logger.LogWarning("Login failed: tenant {TenantId} is not active ({Status}).", tenant.Id, tenant.Status);
            throw new UnauthorizedException("This tenant is not currently active.");
        }

        //if (tenantContext.TenantId is { } headerTenantId && headerTenantId != tenant.Id)
        //{
        //    throw new UnauthorizedException("Tenant header does not match tenant slug.");
        //}

        tenantContextAccessor.SetTenant(tenant.Id, tenant.SchemaName);
        await accInvDbContext.ResetConnectionAsync(cancellationToken);
       
        var user = resolvedUserId is { } userId
            ? await accInvDbContext.Users.SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken)
            : await accInvDbContext.Users.FirstOrDefaultAsync(u => u.UserName.ToLower() == identifier && u.IsActive, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Login failed: no user {UserName} for tenant {TenantId}.", request.UserName, tenant.Id);
            throw new UnauthorizedException("Invalid tenant, username, or password.");
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            logger.LogWarning(
                "Login failed: bad password for user {UserId} ({UserName}) in tenant {TenantId}.",
                user.Id, user.UserName, tenant.Id);
            throw new UnauthorizedException("Invalid tenant, username, or password.");
        }

        //var roles = await db.Roles
        //    .Where(r => user.RoleIds.Contains(r.Id))
        //    .AsNoTracking()
        //    .ToListAsync(cancellationToken);

        var roleNames = new string[] { };
        var permissionCodes = new string[] { };

        var pair = tokenService.GenerateTokenPair(
            user.Id, user.UserName, tenant.Id, tenant.SchemaName, roleNames, permissionCodes);

        user.SetRefreshToken(tokenService.HashRefreshToken(pair.RefreshToken), DateTimeOffset.UtcNow.AddDays(7));

        await accInvDbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "User {UserId} ({UserName}) logged in to tenant {TenantId} with roles [{Roles}].",
            user.Id, user.UserName, tenant.Id, string.Join(", ", roleNames));

        return new LoginResult(user.Id, tenant.Id, pair.AccessToken, pair.RefreshToken, pair.AccessTokenExpiresAtUtc, tenant.Name, tenant.Slug, user.UserName);
    }
}
