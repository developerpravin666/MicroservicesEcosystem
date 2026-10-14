namespace AccountingInventory.Application.Abstractions;

public sealed record UpdateTenantProfileRequest(string Name, string OwnerName, string OwnerEmail,
    string OwnerMobile, string? StateCode, string? Gstin, string? Address);
public sealed record ResetTenantPasswordRequest(string Password);

public interface ITenantAdministration
{
    Task UpdateProfileAsync(Guid tenantId, UpdateTenantProfileRequest request, CancellationToken ct);
    Task ResetOwnerPasswordAsync(Guid tenantId, string password, CancellationToken ct);
}
