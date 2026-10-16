using FluentValidation;

namespace AccountingInventory.Application.GeneralLedger.Commands.AutoMatchBankReconciliation;

public sealed class AutoMatchBankReconciliationCommandValidator : AbstractValidator<AutoMatchBankReconciliationCommand>
{
    public AutoMatchBankReconciliationCommandValidator()
    {
        RuleFor(x => x.ReconciliationId).NotEmpty().WithMessage("Reconciliation ID is required.");
    }
}

