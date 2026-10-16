using FluentValidation;

namespace AccountingInventory.Application.Compliance.Rcm.Commands.CancelRcmSelfInvoice;

public sealed class CancelRcmSelfInvoiceCommandValidator : AbstractValidator<CancelRcmSelfInvoiceCommand>
{
    public CancelRcmSelfInvoiceCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("RCM Self-Invoice ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(300).WithMessage("Cancellation reason cannot exceed 300 characters.");
    }
}

