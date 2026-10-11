namespace AccountingInventory.Application.Sales.Payments;

public sealed record SalesPaymentSummary(
    Guid Id,
    Guid SalesProductId,
    string PaymentMode,
    Guid? FinanceVendorId,
    string? FinanceVendorName,
    decimal? DownPayment,
    int? NumberOfEmi,
    decimal? EmiAmount,
    bool? HasInsurance,
    decimal? InsuranceAmount,
    DateOnly? FirstInstallmentDate);
