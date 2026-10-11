using FluentValidation;

namespace AccountingInventory.Application.Variants.Commands.AddVariant;

public sealed class AddVariantCommandValidator : AbstractValidator<AddVariantCommand>
{
    public AddVariantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
    
}
