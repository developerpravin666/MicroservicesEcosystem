namespace BuildingBlocks.Security;

public static class TenantClaimTypes
{
    /// <summary>JWT claim carrying the tenant's Guid id.</summary>
    public const string TenantId = "tenant_id";

    /// <summary>
    /// JWT claim carrying the tenant's PostgreSQL schema name (e.g. "tenant_acme").
    /// Set once by AccountingInventory at login/refresh time (it already had to resolve
    /// the schema to authenticate the user against the right schema in the first
    /// place — see LoginCommandHandler) and trusted by every downstream service for
    /// the lifetime of the token, so a request never needs a network round-trip just
    /// to find out which schema to query.
    /// </summary>
    public const string TenantSchema = "tenant_schema";
}

public static class PermissionClaimTypes
{
    /// <summary>
    /// JWT claim type carrying one permission code the token bearer is allowed to
    /// exercise (e.g. "Inventory.StockItems.Create"). A token has one claim of this
    /// type per granted permission — the union of every permission granted by every
    /// role assigned to the user, computed by AccountingInventory directly from the
    /// database-managed Role → Permission mapping at login/refresh time. This is what
    /// makes "which roles can access which controller" a database-driven decision
    /// even though enforcement itself is a fast, no-extra-network-hop claim check.
    /// </summary>
    public const string Permission = "permission";
}
