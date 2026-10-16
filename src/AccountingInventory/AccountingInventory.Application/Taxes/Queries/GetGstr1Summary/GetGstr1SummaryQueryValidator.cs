using FluentValidation;

namespace AccountingInventory.Application.Taxes.Queries.GetGstr1Summary;

public sealed class GetGstr1SummaryQueryValidator : AbstractValidator<GetGstr1SummaryQuery>
{
    public GetGstr1SummaryQueryValidator()
    {
        RuleFor(q => q.FromDate)
            .Must((query, fromDate) => !fromDate.HasValue || !query.ToDate.HasValue || fromDate.Value <= query.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

