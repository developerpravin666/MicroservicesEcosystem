using FluentValidation;

namespace AccountingInventory.Application.Sales.Products.Commands.UpdateSalesProduct;

public sealed class UpdateSalesProductCommandValidator : AbstractValidator<UpdateSalesProductCommand>
{
    public UpdateSalesProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.CustomerName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CustomerMobile).NotEmpty().MaximumLength(20);
        RuleFor(x => x.CustomerAddress).NotEmpty().MaximumLength(500);
        RuleFor(x => x.CustomerEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.CustomerEmail));
        RuleFor(x => x.SaleDate).NotEmpty();
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 3650);
        RuleFor(x => x.ProductPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.SellingPrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Discount).LessThanOrEqualTo(x => x.SellingPrice);
    }
}
