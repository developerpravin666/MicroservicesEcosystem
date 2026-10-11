using MediatR;

namespace AccountingInventory.Application.Sales.Payments.Commands.RecordSalesPayment;

public sealed record RecordSalesPaymentCommand(
    Guid SalesProductId,
    string PaymentMode,
    Guid? FinanceVendorId,
    decimal? DownPayment,
    int? NumberOfEmi,
    decimal? EmiAmount,
    bool? HasInsurance,
    decimal? InsuranceAmount,
    DateOnly? FirstInstallmentDate) : IRequest<SalesPaymentSummary>;
