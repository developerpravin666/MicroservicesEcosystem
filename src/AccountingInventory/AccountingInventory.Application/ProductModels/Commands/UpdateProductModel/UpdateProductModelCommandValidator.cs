using FluentValidation;

namespace AccountingInventory.Application.ProductModels.Commands.UpdateProductModelCommand;

public sealed class UpdateProductModelCommandValidator : AbstractValidator<UpdateProductModelCommand>
{
    public UpdateProductModelCommandValidator()
    {
        RuleFor(x => x.BrandId).NotEqual(Guid.Empty);
        RuleFor(x => x.ProductTypeId).NotEqual(Guid.Empty);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Description).MaximumLength(500);
    }
}


