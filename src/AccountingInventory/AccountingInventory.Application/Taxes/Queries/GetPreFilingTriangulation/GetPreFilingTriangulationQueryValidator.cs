using FluentValidation;

namespace AccountingInventory.Application.Taxes.Queries.GetPreFilingTriangulation;

public sealed class GetPreFilingTriangulationQueryValidator : AbstractValidator<GetPreFilingTriangulationQuery>
{
    public GetPreFilingTriangulationQueryValidator()
    {
        RuleFor(q => q.FromDate)
            .Must((query, from) => !from.HasValue || !query.ToDate.HasValue || from.Value <= query.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

