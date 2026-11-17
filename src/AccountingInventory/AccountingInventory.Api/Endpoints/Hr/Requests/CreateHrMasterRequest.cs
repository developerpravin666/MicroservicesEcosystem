namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record CreateHrMasterRequest(
    string Category,
    string Name,
    string Code,
    string? Description = null,
    int SortOrder = 0,
    bool IsDefault = false);
