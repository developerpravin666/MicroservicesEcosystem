using Microsoft.AspNetCore.Builder;

namespace BuildingBlocks.Security;

/// <summary>
/// Declares that a Minimal API endpoint (or an entire route group) requires the caller
/// to hold a specific permission — enforced by checking for a "permission" claim with
/// that exact code on the authenticated user's JWT. Because the set of permission
/// claims embedded in a user's token is computed by AccountingInventory directly from the
/// database-managed Role → Permission mapping at login/refresh time, which controllers
/// a given role can reach is entirely data-driven: changing a role's permissions in
/// the database changes what its users can call the next time they log in or refresh,
/// with no code or deployment change required in the service being protected.
///
/// Usage:
///   app.MapGroup("/api/stock-items")
///      .MapPost("/", ...).RequirePermission("Inventory.StockItems.Create");
/// </summary>
public static class PermissionEndpointExtensions
{
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
        => builder.RequireAuthorization(policy =>
            policy.RequireClaim(PermissionClaimTypes.Permission, permissionCode));
}
