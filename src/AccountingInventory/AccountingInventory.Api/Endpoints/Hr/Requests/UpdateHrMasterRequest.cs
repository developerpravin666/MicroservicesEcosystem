namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record UpdateHrMasterRequest(
    string Name,
    string? Description = null,
    bool IsDefault = false,
    bool IsActive = true);
