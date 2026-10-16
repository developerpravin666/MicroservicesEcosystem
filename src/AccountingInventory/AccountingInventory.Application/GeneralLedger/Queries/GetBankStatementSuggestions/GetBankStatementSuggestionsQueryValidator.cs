using FluentValidation;

namespace AccountingInventory.Application.GeneralLedger.Queries.GetBankStatementSuggestions;

public sealed class GetBankStatementSuggestionsQueryValidator : AbstractValidator<GetBankStatementSuggestionsQuery>
{
    public GetBankStatementSuggestionsQueryValidator()
    {
        RuleFor(x => x.ReconciliationId).NotEmpty().WithMessage("Reconciliation ID is required.");
    }
}

