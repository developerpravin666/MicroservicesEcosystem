namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record DisbursePayrollRequest(
    DateOnly PaymentDate,
    string? Notes = null);
