using FluentValidation;

namespace AccountingInventory.Application.Brands.Commands.AddBrand;

public sealed class AddBrandCommandValidator : AbstractValidator<AddBrandCommand>
{
    public AddBrandCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(500);
    }
    
}
