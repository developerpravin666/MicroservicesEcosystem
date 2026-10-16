using FluentValidation;

namespace AccountingInventory.Application.Taxes.Queries.ExportGstr1Excel;

public sealed class ExportGstr1ExcelQueryValidator : AbstractValidator<ExportGstr1ExcelQuery>
{
    public ExportGstr1ExcelQueryValidator()
    {
        RuleFor(q => q.FromDate)
            .Must((query, fromDate) => !fromDate.HasValue || !query.ToDate.HasValue || fromDate.Value <= query.ToDate.Value)
            .WithMessage("From date must be on or before To date.");
    }
}

