namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record CreateHrMasterRequest(
    string Category,
    string Name,
    string Code,
    string? Description = null,
    bool IsDefault = false);
