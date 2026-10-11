namespace AccountingInventory.Domain.Entities;

/// <summary>Committed result for a tenant/user-scoped money transaction retry.</summary>
public sealed class BusinessRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestKey { get; set; } = null!;
    public string RequestHash { get; set; } = null!;
    public string ResponseJson { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
