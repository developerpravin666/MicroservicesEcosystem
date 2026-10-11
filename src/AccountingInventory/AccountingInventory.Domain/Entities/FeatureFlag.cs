namespace AccountingInventory.Domain.Entities;

/// <summary>
/// Represents a system or tenant feature flag stored persistently in the database.
/// Used to dynamically enable or disable platform capabilities (such as voice typing).
/// </summary>
public sealed class FeatureFlag
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string Category { get; set; } = "General";
    public Guid? TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ModifiedAt { get; set; }

    public static FeatureFlag Create(
        string key,
        string name,
        string? description = null,
        bool isEnabled = true,
        string category = "General",
        Guid? tenantId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            Key = key.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Description = description?.Trim(),
            IsEnabled = isEnabled,
            Category = category.Trim(),
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow
        };
}

