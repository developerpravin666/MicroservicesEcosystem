using FluentValidation;

namespace AccountingInventory.Application.ProductModels.Commands.AddProductModel;

public sealed class AddProductModelCommandValidator : AbstractValidator<AddProductModelCommand>
{
    public AddProductModelCommandValidator()
    {
        RuleFor(x => x.BrandId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
    
}


