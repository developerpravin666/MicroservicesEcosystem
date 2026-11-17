namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record ProcessPayrollRequest(
    int Month,
    int Year,
    string? Notes = null);
