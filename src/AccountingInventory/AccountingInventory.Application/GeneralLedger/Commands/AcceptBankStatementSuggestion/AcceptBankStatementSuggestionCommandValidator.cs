using FluentValidation;

namespace AccountingInventory.Application.GeneralLedger.Commands.AcceptBankStatementSuggestion;

public sealed class AcceptBankStatementSuggestionCommandValidator : AbstractValidator<AcceptBankStatementSuggestionCommand>
{
    public AcceptBankStatementSuggestionCommandValidator()
    {
        RuleFor(x => x.ReconciliationId).NotEmpty().WithMessage("Reconciliation ID is required.");
        RuleFor(x => x.StatementLineId).NotEmpty().WithMessage("Statement Line ID is required.");
        RuleFor(x => x.SuggestionType).NotEmpty().Must(t => t == "CustomerReceipt" || t == "VendorPayment")
            .WithMessage("Suggestion type must be 'CustomerReceipt' or 'VendorPayment'.");
        RuleFor(x => x.EntityId).NotEmpty().WithMessage("Entity ID is required.");
    }
}

