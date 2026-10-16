using FluentValidation;

namespace AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;

public sealed class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.HsnSac).Must(code => string.IsNullOrWhiteSpace(code) || System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), @"^(?:[0-9]{4}|[0-9]{6}|[0-9]{8})$")).WithMessage("HSN/SAC must contain 4, 6 or 8 digits.");
        RuleFor(x => x.VendorId).NotEmpty();
        RuleFor(x => x.BrandId).NotEmpty();
        RuleFor(x => x.ProductTypeId).NotEmpty();
        RuleFor(x => x.ProductModelId).NotEmpty();
        RuleFor(x => x.VariantId).NotEmpty();
        RuleFor(x => x.ColorId).NotEmpty();
        RuleFor(x => x.SerialNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SerialNumber1).MaximumLength(100);
        RuleFor(x => x.BillNumber).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        RuleFor(x => x.Discount).GreaterThanOrEqualTo(0).PrecisionScale(18, 2, ignoreTrailingZeros: true).LessThanOrEqualTo(x => x.PurchasePrice);
        RuleFor(x => x.Cgst).InclusiveBetween(0, 100);
        RuleFor(x => x.Sgst).InclusiveBetween(0, 100);
        RuleFor(x => x.Tax).InclusiveBetween(0, 100);
        RuleFor(x => x.Tax).Equal(x => x.Cgst + x.Sgst).WithMessage("Tax must equal CGST plus SGST.");
        RuleFor(x => x.PaymentTermsDays).InclusiveBetween(0, 3650);
    }
}
