using FluentValidation;

namespace AccountingInventory.Application.Compliance.EInvoices.Commands.CancelEInvoice;

public sealed class CancelEInvoiceCommandValidator : AbstractValidator<CancelEInvoiceCommand>
{
    public CancelEInvoiceCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("E-Invoice ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(500).WithMessage("Cancellation reason cannot exceed 500 characters.");
    }
}

