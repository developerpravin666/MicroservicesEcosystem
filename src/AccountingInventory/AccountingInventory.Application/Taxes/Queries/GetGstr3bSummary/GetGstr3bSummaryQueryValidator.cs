using FluentValidation;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr3bSummary;

public sealed class GetGstr3bSummaryQueryValidator : AbstractValidator<GetGstr3bSummaryQuery>
{
    public GetGstr3bSummaryQueryValidator()
    {
        RuleFor(q => q.FromDate)
            .Must((query, fromDate) => !fromDate.HasValue || !query.ToDate.HasValue || fromDate.Value <= query.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

