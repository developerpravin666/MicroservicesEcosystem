namespace BuildingBlocks.WebDefaults;

/// <summary>The standard wire format returned by application API endpoints.</summary>
public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    IReadOnlyDictionary<string, string>? Metadata = null);
