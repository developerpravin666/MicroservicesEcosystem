using BuildingBlocks.Domain;
using AccountingInventory.Domain.Exceptions;

namespace AccountingInventory.Domain.Entities;

/// <summary>
/// Aggregate root for a tenant's user. Lives inside that tenant's PostgreSQL schema
/// (physical isolation — see IdentityDbContext / TenantSchemaConnectionInterceptor),
/// so two different tenants can both have a user named "admin" without collision; the
/// TenantId column here is kept purely for informational/audit purposes (e.g. cross-
/// schema reporting tooling), not as a query filter.
///
/// Role assignment is by reference (RoleIds) rather than embedding role names
/// directly, because roles — and the permissions they grant — are database-managed
/// and can change independently of any given user (see the Role aggregate).
/// </summary>
public sealed class User : AggregateRoot
{
    //private readonly List<Guid> _roleIds = [];
    public required string UserName { get; init; }
    public required string Mobile { get; init; }
    public required string Email { get; init; }
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsOwner { get; private set; }
    public bool EmailVerified { get; private set; }
    public string? InvitationHash { get; private set; }
    public DateTimeOffset? InvitationExpiresAt { get; private set; }
    public void SetOwner(bool owner) => IsOwner = owner;
    public void Invite(string hash, DateTimeOffset expires) { InvitationHash = hash; InvitationExpiresAt = expires; IsActive = false; }
    public void AcceptInvitation() { EmailVerified = true; InvitationHash = null; InvitationExpiresAt = null; IsActive = true; }
    //public IReadOnlyCollection<Guid> RoleIds => _roleIds.AsReadOnly();
    public string? RefreshTokenHash { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }

    public static User Create(string userName, string mobile, string email)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new AccountingInventoryDomainException("Username cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(mobile))
        {
            throw new AccountingInventoryDomainException("Mobile cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AccountingInventoryDomainException("Email cannot be empty.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            UserName = userName,
            IsActive = true,
            Mobile = mobile,
            Email = email,
        };
    }

    public void SetPasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new AccountingInventoryDomainException("Password hash cannot be empty.");
        }

        PasswordHash = passwordHash;
    }

    public void Activate()
    {
        IsActive = true;
    }
    public void DeActivate()
    {
        IsActive = false;
    }
    public void Delete()
    {
        IsDeleted = true;
    }

    //public void AssignRole(Guid roleId)
    //{
    //    if (!_roleIds.Contains(roleId))
    //    {
    //        _roleIds.Add(roleId);
    //    }
    //}

    //public void RemoveRole(Guid roleId) => _roleIds.Remove(roleId);

    public void SetRefreshToken(string refreshTokenHash, DateTimeOffset expiresAtUtc)
    {
        RefreshTokenHash = refreshTokenHash;
        RefreshTokenExpiresAtUtc = expiresAtUtc;
    }

    public bool IsRefreshTokenValid(string refreshTokenHash, DateTimeOffset nowUtc)
        => RefreshTokenHash == refreshTokenHash &&
           RefreshTokenExpiresAtUtc is not null &&
           RefreshTokenExpiresAtUtc > nowUtc;

    public void RevokeRefreshToken()
    {
        RefreshTokenHash = null;
        RefreshTokenExpiresAtUtc = null;
    }

    private User() { }
}
