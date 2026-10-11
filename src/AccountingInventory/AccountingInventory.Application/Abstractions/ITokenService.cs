namespace AccountingInventory.Application.Abstractions;

public sealed record TokenPair(string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAtUtc);

/// <summary>
/// Dependency-inversion seam for JWT issuance. Implemented in Infrastructure so the
/// Application layer never references System.IdentityModel.Tokens.Jwt directly.
/// The token embeds the tenant (id + schema), every role name assigned to the user,
/// and the union of every permission code granted by those roles — see
/// LoginCommandHandler / RefreshTokenCommandHandler for how those values are computed
/// from the database immediately before a token is minted.
/// </summary>
public interface ITokenService
{
    TokenPair GenerateTokenPair(
        Guid userId,
        string userName,
        Guid tenantId,
        string tenantSchema,
        IEnumerable<string> roleNames,
        IEnumerable<string> permissionCodes);

    string HashRefreshToken(string refreshToken);
    string CreatePasswordResetToken(Guid userId, Guid tenantId, TimeSpan lifetime);
    PasswordResetPrincipal? ValidatePasswordResetToken(string token);

    /// <summary>
    /// Validates an expired-but-otherwise-legitimate access token (signature, issuer,
    /// audience all still checked — only the lifetime check is relaxed) and extracts
    /// the identity/tenant it was issued for. Refresh doesn't need the caller to
    /// re-supply a tenant slug because the OLD token already carries tenant_id and
    /// tenant_schema claims from when it was originally issued.
    /// </summary>
    ExpiredTokenPrincipal? GetPrincipalFromExpiredToken(string expiredAccessToken);
}

public sealed record ExpiredTokenPrincipal(Guid UserId, Guid TenantId, string TenantSchema);
public sealed record PasswordResetPrincipal(Guid UserId, Guid TenantId);
