using FluentValidation;

namespace AccountingInventory.Application.Compliance.EWayBills.Commands.CancelEWayBill;

public sealed class CancelEWayBillCommandValidator : AbstractValidator<CancelEWayBillCommand>
{
    public CancelEWayBillCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("E-Way Bill ID is required.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MaximumLength(500).WithMessage("Cancellation reason cannot exceed 500 characters.");
    }
}

