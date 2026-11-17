namespace AccountingInventory.Api.Endpoints.Hr.Requests;

public sealed record MarkPaidRequest(
    string? PaymentMode,
    string? PaymentReference);
