using FluentValidation;

namespace AccountingInventory.Application.ProductTypes.Commands.UpdateProductTypeCommand;

public sealed class UpdateProductTypeCommandValidator : AbstractValidator<UpdateProductTypeCommand>
{
    public UpdateProductTypeCommandValidator()
    {
        RuleFor(x => x.HsnSac).Must(code => string.IsNullOrWhiteSpace(code) || System.Text.RegularExpressions.Regex.IsMatch(code.Trim(), @"^(?:[0-9]{4}|[0-9]{6}|[0-9]{8})$")).WithMessage("HSN/SAC must contain 4, 6 or 8 digits.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}

