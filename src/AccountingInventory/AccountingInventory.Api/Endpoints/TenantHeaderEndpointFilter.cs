using AccountingInventory.Application.Abstractions;
using System.Security.Claims;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Domain.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Api.Endpoints;

/// <summary>
/// Establishes the tenant context for tenant-scoped endpoints from the required
/// <c>X-Tenant-Id</c> request header. The schema is read only from the control-plane
/// tenant registry, never from a client header.
/// </summary>
public sealed class TenantHeaderEndpointFilter(bool allowBootstrapRegistration = false, bool allowInvitationAcceptance = false) : IEndpointFilter
{
    public const string TenantIdHeader = "X-Tenant-Id";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var userPrincipal = context.HttpContext.User;
        var isAuthenticated = userPrincipal.Identity?.IsAuthenticated == true;
        var isProductOwner = isAuthenticated && (userPrincipal.IsInRole("ProductOwner") || userPrincipal.IsInRole("SuperAdmin"));

        var values = context.HttpContext.Request.Headers[TenantIdHeader];
        Guid tenantId;
        if (values.Count == 1 && Guid.TryParse(values[0], out var parsedId) && parsedId != Guid.Empty)
        {
            tenantId = parsedId;
        }
        else if (isProductOwner)
        {
            // Product Owner / SuperAdmin without an explicit X-Tenant-Id header:
            // Auto-resolve the first active tenant from directory so requests don't fail
            var directory = context.HttpContext.RequestServices.GetRequiredService<ITenantDirectoryContext>();
            var defaultTenant = await directory.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active && t.IsActive)
                .OrderBy(t => t.CreatedAt)
                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

            if (defaultTenant is not null)
            {
                tenantId = defaultTenant.Id;
            }
            else
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "No active tenant found to operate on.");
            }
        }
        else
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Request header '{TenantIdHeader}' must contain one valid tenant id.");
        }

        if (!allowBootstrapRegistration && !allowInvitationAcceptance && !isAuthenticated)
            return Results.Unauthorized();

        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (isAuthenticated && !isProductOwner)
        {
            if (tenantContext.TenantId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "The access token does not identify a tenant.");
            if (tenantContext.TenantId is { } authenticatedTenantId && authenticatedTenantId != tenantId)
            {
                return Results.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    title: "The tenant header does not match the authenticated user's tenant.");
            }
        }

        var tenantDirectory = context.HttpContext.RequestServices.GetRequiredService<ITenantDirectoryContext>();
        var tenant = await tenantDirectory.Tenants
            .AsNoTracking()
            .SingleOrDefaultAsync(t => t.Id == tenantId, context.HttpContext.RequestAborted);

        if (tenant is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Tenant was not found.");
        }

        if (tenant.Status != TenantStatus.Active || !tenant.IsActive)
        {
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Tenant is not active.");
        }

        context.HttpContext.RequestServices.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(tenant.Id, tenant.SchemaName);

        var db = context.HttpContext.RequestServices.GetRequiredService<IAccountingInventoryDbContext>();
        if (isAuthenticated)
        {
            if (!isProductOwner)
            {
                if (!Guid.TryParse(userPrincipal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                    || !await db.Users.AnyAsync(user => user.Id == userId && user.IsActive, context.HttpContext.RequestAborted))
                    return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "The user account is inactive or does not belong to this tenant.");
            }
        }
        else if (allowBootstrapRegistration && await db.Users.AnyAsync(context.HttpContext.RequestAborted))
            return Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "This tenant is already registered. Its owner must create additional accounts.");

        return await next(context);
    }
}
