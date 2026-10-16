using FluentValidation;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Json;

public sealed class ExportGstr1JsonQueryValidator : AbstractValidator<ExportGstr1JsonQuery>
{
    public ExportGstr1JsonQueryValidator()
    {
        RuleFor(q => q.FromDate)
            .Must((query, fromDate) => !fromDate.HasValue || !query.ToDate.HasValue || fromDate.Value <= query.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

