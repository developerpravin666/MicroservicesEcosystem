using FluentValidation;

namespace AccountingInventory.Application.Inventory.Valuation.Queries.GetTradingProfitLossValuation;

public sealed class GetTradingProfitLossValuationQueryValidator : AbstractValidator<GetTradingProfitLossValuationQuery>
{
    public GetTradingProfitLossValuationQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => !x.FromDate.HasValue || !x.ToDate.HasValue || x.ToDate.Value >= x.FromDate.Value)
            .WithMessage("ToDate must be greater than or equal to FromDate.");
    }
}

