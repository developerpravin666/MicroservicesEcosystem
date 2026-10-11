using FluentValidation;

namespace AccountingInventory.Application.Purchases.Accounting;

public sealed class RecordPurchasePaymentCommandValidator : AbstractValidator<RecordPurchasePaymentCommand>
{
    private static readonly string[] PaymentModes = ["Cash", "UPI", "OnlineTransfer", "Cheque", "Other"];

    public RecordPurchasePaymentCommandValidator()
    {
        RuleFor(command => command.VendorId).NotEmpty();
        RuleFor(command => command.BillNumber).NotEmpty().MaximumLength(100);
        RuleFor(command => command.Amount).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(command => command.PaymentMode).Must(mode => PaymentModes.Contains(mode))
            .WithMessage("Choose Cash, UPI, Online Transfer, Cheque, or Other.");
        RuleFor(command => command.PaymentDate).NotEmpty();
        RuleFor(command => command.ReferenceNumber).MaximumLength(100)
            .NotEmpty().When(command => command.PaymentMode is "UPI" or "OnlineTransfer" or "Cheque");
        RuleFor(command => command.Note).MaximumLength(500);
    }
}
