using FluentValidation;

namespace AccountingInventory.Application.ProductTypes.Commands.AddProductType;

public sealed class AddProductTypeCommandValidator : AbstractValidator<AddProductTypeCommand>
{
    public AddProductTypeCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
    
}

