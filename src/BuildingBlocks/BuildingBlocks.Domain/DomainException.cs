namespace BuildingBlocks.Domain;

/// <summary>
/// Base type for every domain-invariant violation across every service (e.g.
/// "quantity cannot go negative", "username cannot be empty"). Each service still
/// throws its own specifically-named exception for readability at the call site
/// (InventoryDomainException, IdentityDomainException, TenantDomainException), but
/// because they all derive from this common base, the shared GlobalExceptionHandler
/// in BuildingBlocks.WebDefaults can map ANY of them to a 400 ProblemDetails response
/// with one pattern-match arm, instead of every service needing its own copy of that
/// mapping logic.
/// </summary>
public abstract class DomainException(string message) : Exception(message);
