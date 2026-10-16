using FluentValidation;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetStockValuationSummary;

public sealed class GetStockValuationSummaryQueryValidator : AbstractValidator<GetStockValuationSummaryQuery>
{
    private static readonly string[] AllowedMethods = ["FIFO", "WEIGHTEDAVERAGE", "LASTPURCHASECOST", "ALL"];

    public GetStockValuationSummaryQueryValidator()
    {
        RuleFor(x => x.Method)
            .Must(m => string.IsNullOrWhiteSpace(m) || AllowedMethods.Contains(m.Trim().ToUpperInvariant()))
            .WithMessage("Method must be one of: FIFO, WeightedAverage, LastPurchaseCost, All.");
    }
}

