using FluentValidation;

namespace AccountingInventory.Application.Taxes.Commands.ReconcileGstr2b;

public sealed class ReconcileGstr2bCommandValidator : AbstractValidator<ReconcileGstr2bCommand>
{
    public ReconcileGstr2bCommandValidator()
    {
        RuleFor(c => c.JsonContent)
            .NotEmpty().WithMessage("GSTR-2B JSON content is required.");

        RuleFor(c => c.FromDate)
            .Must((cmd, from) => !from.HasValue || !cmd.ToDate.HasValue || from.Value <= cmd.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

