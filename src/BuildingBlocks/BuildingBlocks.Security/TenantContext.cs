using BuildingBlocks.Domain;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.AspNetCore.Http;

namespace BuildingBlocks.Security;

/// <summary>
/// Concrete, scoped implementation of ITenantContext / ITenantContextAccessor (both
/// defined in the framework-agnostic BuildingBlocks.Domain). Registered once by
/// AddPlatformSecurity and resolved as both interfaces pointing at the same instance.
/// </summary>
internal sealed class TenantContext : ITenantContext, ITenantContextAccessor
{
    public Guid? TenantId { get; private set; }
    public string? SchemaName { get; private set; }

    public void SetTenant(Guid tenantId, string schemaName)
    {
        TenantId = tenantId;
        SchemaName = TenantSchemaNameValidator.EnsureValid(schemaName);
    }

    public void Clear()
    {
        TenantId = null;
        SchemaName = null;
    }
}

/// <summary>
/// Resolves the current tenant from the authenticated user's JWT (the "tenant_id" /
/// "tenant_schema" claims set by AccountingInventory at login) and makes it available for
/// the rest of the request via the scoped ITenantContextAccessor. Must run after
/// UseAuthentication so HttpContext.User is already populated — see
/// BuildingBlocks.Security.DependencyInjection.UsePlatformSecurity for the correct
/// middleware ordering. Endpoints that are legitimately pre-authentication (Login,
/// Register, tenant self-service signup) simply never see this middleware set
/// anything, and resolve tenant context themselves — see ITenantContextAccessor.
/// </summary>
public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContextAccessor tenantContextAccessor)
    {
        var tenantIdClaim = context.User.FindFirst(TenantClaimTypes.TenantId)?.Value;
        var schemaClaim = context.User.FindFirst(TenantClaimTypes.TenantSchema)?.Value;

        if (Guid.TryParse(tenantIdClaim, out var tenantId) &&
            TenantSchemaNameValidator.IsValid(schemaClaim))
        {
            tenantContextAccessor.SetTenant(tenantId, schemaClaim!);
        }

        await next(context);
    }
}
