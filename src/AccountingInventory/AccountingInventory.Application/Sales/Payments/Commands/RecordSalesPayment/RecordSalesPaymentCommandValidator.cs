using FluentValidation;

namespace AccountingInventory.Application.Sales.Payments.Commands.RecordSalesPayment;

public sealed class RecordSalesPaymentCommandValidator : AbstractValidator<RecordSalesPaymentCommand>
{
    public RecordSalesPaymentCommandValidator()
    {
        RuleFor(command => command.SalesProductId).NotEmpty();
        RuleFor(command => command.PaymentMode).Must(mode => mode is "Cash" or "Finance")
            .WithMessage("Payment mode must be Cash or Finance.");

        When(command => command.PaymentMode == "Finance", () =>
        {
            RuleFor(command => command.FinanceVendorId).NotEmpty();
            RuleFor(command => command.DownPayment)
                .NotNull().Must(amount => amount.HasValue && amount.Value >= 0);
            RuleFor(command => command.NumberOfEmi)
                .NotNull().Must(number => number.HasValue && number.Value > 0);
            RuleFor(command => command.EmiAmount)
                .NotNull().Must(amount => amount.HasValue && amount.Value > 0);
            RuleFor(command => command.HasInsurance).NotNull();
            RuleFor(command => command.InsuranceAmount)
                .NotNull().Must(amount => amount.HasValue && amount.Value > 0)
                .When(command => command.HasInsurance == true);
            RuleFor(command => command.FirstInstallmentDate).NotNull();
        });
    }
}
